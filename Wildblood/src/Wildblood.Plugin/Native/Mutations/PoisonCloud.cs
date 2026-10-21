// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.Adopted.Visuals;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// A lingering pool of poison reconstructed from a Miasmic creature's replicated trail. Every machine spawns one to
    /// draw it; only the machine that owns the creature marks it <c>damaging</c> and lets it poison what stands in it,
    /// so two clients never double-hit and a client with effects off still takes exactly the owner's poison. It harms
    /// PLAYERS ONLY, with the game's own Poison: it applies a poison-typed hit whose strength is <c>cloud damage</c>, and
    /// RPC_Damage converts that into the vanilla SE_Poison status effect - the same debuff, icon and tick as a blob's.
    /// It carries no attacker, so it neither scales with mutations nor feeds Leeching. Strength, life and radius arrive
    /// at spawn. A dedicated server makes only the damaging copy, for a creature it owns, and never the look.
    /// </summary>
    public sealed class PoisonCloud : MonoBehaviour
    {
        private float _lifetime;
        private float _damage;
        private float _radius;
        private bool _damaging;
        private float _age;
        private float _tick;
        private readonly List<Character> _found = new List<Character>();

        public static void Spawn(Vector3 position, float lifetime, float damage, float radius, string effect, bool damaging)
        {
            bool headless = Machine.Headless;
            if (headless && !damaging)
            {
                return; // a dedicated server draws nothing, and this copy would hurt no one
            }
            GameObject holder = new GameObject("ecr_miasma_cloud");
            holder.transform.position = position;
            PoisonCloud cloud = holder.AddComponent<PoisonCloud>();
            cloud._lifetime = lifetime;
            cloud._damage = damage;
            cloud._radius = radius;
            cloud._damaging = damaging;
            if (!headless) // the poison is the server's to deal when it owns the creature; the look is never its to draw
            {
                GameObject? prefab = EffectResolver.Resolve(effect, EffectResolver.Cloud, "Miasmic cloud effect");
                LingeringVisual.Attach(holder, prefab, lifetime, radius);
            }
        }

        private void Update() => Guard.Run("PoisonCloud.Update", static self => self.Step(), this);

        private void Step()
        {
            _age += Time.deltaTime;
            if (_age >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }
            if (!_damaging) // remote clients draw the cloud but never decide its damage
            {
                return;
            }
            _tick -= Time.deltaTime;
            if (_tick <= 0f)
            {
                _tick = 1f;
                Poison();
            }
        }

        // Clouds hurt PLAYERS ONLY. Filtering to Player excludes all three cases the spec calls out as real bugs: other
        // creatures (a Miasmic pack would poison itself to death), tamed animals (killed by a fight the player never
        // chose), and the creature that made them (which would entangle Miasmic with Devouring's meals).
        private void Poison()
        {
            _found.Clear();
            Character.GetCharactersInRange(transform.position, _radius, _found);
            foreach (Character character in _found)
            {
                if (character is Player player && !player.IsDead())
                {
                    player.Damage(BuildHit());
                }
            }
        }

        private HitData BuildHit()
        {
            HitData hit = new HitData();
            hit.m_damage.m_poison = _damage;
            hit.m_point = transform.position;
            hit.m_hitType = HitData.HitType.Poisoned;
            return hit;
        }
    }
}
