// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using ExtraPotions.Bestiary.Spawning;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Adapters.Kit
{
    internal static class KitCombat
    {
        public static bool Clear(Character from, Character to)
        {
            Vector3 a = from.GetCenterPoint();
            Vector3 b = to.GetCenterPoint();
            Vector3 dir = b - a;
            float dist = dir.magnitude;
            if (dist < 0.05f) return true;
            return Open(from, a, dir / dist, dist, to);
        }

        public static bool StepClear(Character self, Vector3 dir, float step) =>
            Open(self, self.GetCenterPoint(), dir, step + 0.05f, null);

        public static void Pierce(Character attacker, Character target, float damage)
        {
            var hit = new HitData
            {
                m_point = target.GetCenterPoint(),
                m_dir = (target.transform.position - attacker.transform.position).normalized,
                m_dodgeable = true,
                m_blockable = true,
                m_ranged = true,
                m_hitType = HitData.HitType.EnemyHit,
            };
            hit.m_damage.m_pierce = StarHits.Damage(damage, attacker.GetLevel());
            hit.SetAttacker(attacker);
            target.Damage(hit);
        }

        private static bool Open(Character self, Vector3 origin, Vector3 dir, float distance, Character? other)
        {
            var hits = Physics.RaycastAll(origin, dir, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                var who = hit.collider.GetComponentInParent<Character>();
                if (who == self || who == other) continue;
                return false;
            }
            return true;
        }
    }

    /// <summary>Pulls a character toward a hook on the machine that owns that character.</summary>
    internal static class KitPull
    {
        private const float Melee = 2.8f;
        private const float Speed = 12f;
        private static readonly Dictionary<ZDOID, Pull> Pending = new Dictionary<ZDOID, Pull>();

        private struct Pull
        {
            public Vector3 Toward;
            public float Left;
            public float Until;
        }

        public static void Receive(long sender, ZDOID id, Vector3 hook, float meters)
        {
            var go = ZNetScene.instance?.FindInstance(id);
            var character = go != null ? go.GetComponent<Character>() : null;
            if (character == null || character.m_nview == null || !character.m_nview.IsOwner()) return;
            if (!OwnedHook(sender, character, hook)) return;
            Begin(id, hook, meters);
        }

        private static bool OwnedHook(long sender, Character target, Vector3 hookPoint)
        {
            float range = KitSettings.BogRange;
            foreach (var candidate in Character.GetAllCharacters())
            {
                if (candidate == null || candidate.IsDead() || candidate.name == null) continue;
                if (!candidate.name.StartsWith(KitCreatures.BogHook, StringComparison.Ordinal)) continue;
                var nview = candidate.m_nview;
                if (nview == null || !nview.IsValid()) continue;
                var zdo = nview.GetZDO();
                if (zdo == null || zdo.GetOwner() != sender) continue;
                float dist = Vector3.Distance(candidate.GetCenterPoint(), target.GetCenterPoint());
                float gap = Vector3.Distance(candidate.GetCenterPoint(), hookPoint);
                if (HookPull.Allow(true, dist, range, gap)) return true;
            }
            return false;
        }

        public static void Ask(Character target, Vector3 hook, float meters)
        {
            var nview = target.m_nview;
            if (nview == null || !nview.IsValid() || meters <= 0f) return;
            var id = nview.GetZDO().m_uid;
            if (nview.IsOwner()) Begin(id, hook, meters);
            else ZRoutedRpc.instance?.InvokeRoutedRPC(nview.GetZDO().GetOwner(), BestiaryKeys.BogHookPull, id, hook, meters);
        }

        public static void Tick()
        {
            if (Pending.Count == 0) return;
            var done = new List<ZDOID>();
            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || character.m_nview == null || !character.m_nview.IsValid() || !character.m_nview.IsOwner()) continue;
                var id = character.m_nview.GetZDO().m_uid;
                if (!Pending.TryGetValue(id, out var pull)) continue;
                if (character.IsDead() || Time.time > pull.Until) { done.Add(id); continue; }
                Vector3 flat = pull.Toward - character.transform.position;
                flat.y = 0f;
                float gap = flat.magnitude - Melee;
                if (gap <= 0.05f || pull.Left <= 0f || flat.sqrMagnitude < 0.0001f) { done.Add(id); continue; }
                float step = Mathf.Min(pull.Left, gap, Speed * Time.deltaTime);
                Vector3 dir = flat / flat.magnitude;
                if (!KitCombat.StepClear(character, dir, step)) { done.Add(id); continue; }
                Vector3 next = character.transform.position + dir * step;
                if (character.m_body != null)
                {
                    character.m_body.position = next;
                    Vector3 velocity = character.m_body.linearVelocity;
                    character.m_body.linearVelocity = new Vector3(0f, velocity.y, 0f);
                }
                else character.transform.position = next;
                pull.Left -= step;
                if (pull.Left <= 0.01f) done.Add(id);
                else Pending[id] = pull;
            }
            foreach (var id in done) Pending.Remove(id);
            if (Pending.Count == 0) return;
            var stale = new List<ZDOID>();
            foreach (var pair in Pending)
                if (Time.time > pair.Value.Until) stale.Add(pair.Key);
            foreach (var id in stale) Pending.Remove(id);
        }

        private static void Begin(ZDOID id, Vector3 hook, float meters)
        {
            meters = HookPull.Clamp(meters, KitSettings.BogPull);
            if (meters <= 0f) return;
            Pending[id] = new Pull { Toward = hook, Left = meters, Until = Time.time + 3f };
        }
    }

    /// <summary>Throws a hook, then the Draugr's own AI closes. The next throw is a world-clock stamp on the ZDO, written by the owner.</summary>
    public class BogHook : MonoBehaviour
    {
        private const float Melee = 2.8f;
        private static readonly int Ready = BestiaryKeys.HookReady.GetStableHashCode();
        private Character self = null!;
        private MonsterAI ai = null!;
        private ZNetView view = null!;

        private void Awake()
        {
            self = GetComponent<Character>();
            ai = GetComponent<MonsterAI>();
            view = GetComponent<ZNetView>();
        }

        private void Update()
        {
            if (view == null || !view.IsValid() || !view.IsOwner() || self == null || self.IsDead() || ai == null || ZNet.instance == null) return;
            var zdo = view.GetZDO();
            if (zdo == null) return;
            long now = ZNet.instance.GetTime().Ticks;
            if (!HookCooldown.Ready(now, zdo.GetLong(Ready, 0L))) return;
            var target = ai.GetTargetCreature();
            if (target == null || target.IsDead() || !BaseAI.IsEnemy(self, target)) return;
            float distance = Vector3.Distance(self.GetCenterPoint(), target.GetCenterPoint());
            if (distance <= Melee || distance > KitSettings.BogRange) return;
            if (!KitCombat.Clear(self, target)) return;
            zdo.Set(Ready, HookCooldown.Arm(now, KitSettings.BogInterval));
            KitCombat.Pierce(self, target, KitSettings.BogDamage);
            KitPull.Ask(target, self.GetCenterPoint(), KitSettings.BogPull);
        }
    }

    /// <summary>One spear, remembered on the ZDO, then the Fuling's own melee.</summary>
    public class JavelinOnce : MonoBehaviour
    {
        private const float Melee = 2.8f;
        private static readonly int Thrown = BestiaryKeys.JavelinThrown.GetStableHashCode();
        private Character self = null!;
        private MonsterAI ai = null!;
        private ZNetView view = null!;
        private bool spent;

        private void Awake()
        {
            self = GetComponent<Character>();
            ai = GetComponent<MonsterAI>();
            view = GetComponent<ZNetView>();
        }

        private void Update()
        {
            if (spent || view == null || !view.IsValid() || !view.IsOwner() || self == null || self.IsDead() || ai == null) return;
            var zdo = view.GetZDO();
            if (zdo == null) return;
            if (zdo.GetBool(Thrown)) { spent = true; return; }
            var target = ai.GetTargetCreature();
            if (target == null || target.IsDead() || !BaseAI.IsEnemy(self, target)) return;
            float distance = Vector3.Distance(self.GetCenterPoint(), target.GetCenterPoint());
            if (distance <= Melee || distance > KitSettings.SpearRange) return;
            if (!KitCombat.Clear(self, target)) return;
            zdo.Set(Thrown, true);
            spent = true;
            KitCombat.Pierce(self, target, KitSettings.SpearDamage);
        }
    }

    /// <summary>Plates on the ZDO. Fire breaks one. Physical hits are reduced and never removed by the default.</summary>
    public class KitPlates : MonoBehaviour
    {
        private static readonly int Key = BestiaryKeys.SlagPlates.GetStableHashCode();
        private static readonly int FireKey = BestiaryKeys.SlagFire.GetStableHashCode();
        private ZNetView view = null!;
        private Transform[] props = null!;
        private int drawn = int.MinValue;

        private int Max => Mathf.Clamp(KitSettings.SlagPlateCount, 0, 3);

        public int Plates
        {
            get
            {
                int max = Max;
                var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
                return Mathf.Clamp(zdo?.GetInt(Key, max) ?? max, 0, max);
            }
        }

        public float Through => SlagPlates.Through(Plates, Max, KitSettings.SlagArmored);

        private void Awake() => view = GetComponent<ZNetView>();

        private void LateUpdate()
        {
            if (!MeadworksHeadless.AllowsUi || view == null || !view.IsValid()) return;
            int left = Plates;
            if (left == drawn) return;
            drawn = left;
            if (props == null) props = PlateProps();
            for (int i = 0; i < props.Length; i++)
                if (props[i] != null) props[i].gameObject.SetActive(SlagPlates.Shown(i, left));
        }

        private Transform[] PlateProps()
        {
            // Plates hang on the chest bone, so they are not direct children of the body.
            var all = GetComponentsInChildren<Transform>(true);
            int count = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == "mw_prop") count++;
            var found = new Transform[count];
            int write = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == "mw_prop") found[write++] = all[i];
            return found;
        }

        public void Soften(HitData hit)
        {
            float through = Through;
            hit.m_damage.m_blunt *= through;
            hit.m_damage.m_slash *= through;
            hit.m_damage.m_pierce *= through;
            hit.m_damage.m_chop *= through;
            hit.m_damage.m_pickaxe *= through;
        }

        public void Melt(float fire)
        {
            if (fire <= 0f || view == null || !view.IsValid() || !view.IsOwner()) return;
            var zdo = view.GetZDO();
            if (zdo == null) return;
            float stored = zdo.GetFloat(FireKey, 0f);
            int left = SlagPlates.AfterFire(Plates, stored, fire, KitSettings.SlagFire, out stored);
            zdo.Set(Key, left);
            zdo.Set(FireKey, stored);
        }
    }

    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class KitPlateHit
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            if (hit == null || __instance == null || __instance.m_nview == null || !__instance.m_nview.IsOwner()) return;
            if (!__instance.TryGetComponent(out KitPlates plates)) return;
            float fire = SlagPlates.Counted(SlagPlates.Direct((int)hit.m_hitType), hit.m_damage.m_fire);
            SafeCall.Run("Character.RPC_Damage slag plates", static (armor, blow) => armor.Soften(blow), plates, hit);
            if (fire > 0f) SafeCall.Run("Character.RPC_Damage slag plates", static (armor, amount) => armor.Melt(amount), plates, fire);
        }
    }
}
