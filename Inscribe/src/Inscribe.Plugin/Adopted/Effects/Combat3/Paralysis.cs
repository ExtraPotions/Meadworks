// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects.Combat3
{
    /// <summary>
    /// Numbing Blow's paralysis, on the target's owner. Its length in seconds rides in the hit's skill-level field (the
    /// game uses it only as the carried status effect's level), and the owner adds it through the game's own path:
    /// RPC_Damage → SEMan.AddStatusEffect(hash, ..., skillLevel) → <see cref="SetLevel"/>, so it works whoever owns the
    /// target. The owner clamps the length to the running rules' cap (a client cannot send more than the server allows).
    /// No chain-lock (lead decision 2026-10-05): a hit while paralysed does not extend it, and when it ends the creature
    /// is immune for 6 s, kept in its own ZDO so an owner change keeps it (<see cref="CanAdd"/>, <see cref="Stop"/>); a
    /// refused hit still deals its damage and its other effects. While it lasts the creature cannot walk or turn
    /// (speed 0), start an attack, or land the attack it was swinging (<see cref="ParalysisPatches"/>). Everyone sees it:
    /// the owner's movement is what every client is sent, and it crackles with the game's networked shock effect.
    /// </summary>
    public sealed class EcfParalyze : StatusEffect
    {
        public const string EffectName = "ECF_Paralyze";

        public static readonly int Hash = EffectName.GetStableHashCode();

        private bool _timed;

        public override bool CanAdd(Character character) => !Paralysis.Immune(character);

        // Only the hit that paralyses sets the length; a repeat hit (the game resets and re-levels a status effect the
        // target already has) changes nothing.
        public override void SetLevel(int itemLevel, float skillLevel)
        {
            base.SetLevel(itemLevel, skillLevel);
            if (_timed)
            {
                return;
            }
            _timed = true;
            // m_ttl 0 would never end: a paralysis the rules do not allow ends at once.
            m_ttl = Mathf.Max(0.01f, Mathf.Min(skillLevel, CombatCaps.ParalyzeSeconds));
            Paralysis.NoteUntil(m_ttl);
        }

        // Paralysis never restarts its clock (the game resets a status effect a repeat hit carries).
        public override void ResetTime()
        {
        }

        public override void Stop()
        {
            base.Stop();
            Paralysis.StartImmunity(m_character);
        }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            speed = 0f;
        }
    }

    /// <summary>Attacker side of Numbing Blow, the owner-side checks, and the ObjectDB registration of <see cref="EcfParalyze"/> (every peer).</summary>
    internal static class Paralysis
    {
        private const double ImmuneSeconds = 6.0;

        /// <summary>Creature ZDO: network time (ticks) until which it cannot be paralysed again; written by its owner.</summary>
        private static readonly int ImmuneKey = "ecf_para_immune".GetStableHashCode();

        private static EcfParalyze? _template;

        /// <summary>No paralysis on this peer lasts past this time: until then the checks look; after it they cost one compare.</summary>
        private static float _until;

        /// <summary>From the local player's outgoing hit (<see cref="WeaponOnHitPatch"/>). Bosses and players are never paralysed.</summary>
        public static void Carry(float seconds, Character target, HitData hit)
        {
            if (seconds <= 0f || target.IsPlayer() || target.IsBoss() || target.GetFaction() == Character.Faction.Boss)
            {
                return;
            }
            // Only a hit that carries no status effect of its own, or Hamstring's slow (paralysis is the stronger).
            if (hit.m_statusEffectHash == 0 || hit.m_statusEffectHash == EcfSlow.Hash)
            {
                hit.m_statusEffectHash = EcfParalyze.Hash;
                hit.m_skillLevel = seconds;
            }
        }

        public static void NoteUntil(float seconds) => _until = Mathf.Max(_until, Time.time + seconds);

        /// <summary>On the creature's owner, where the status effect is applied: still immune after its last paralysis.</summary>
        public static bool Immune(Character character)
        {
            ZDO? zdo = OwnedZdo(character);
            return zdo != null && ZNet.instance != null && zdo.GetLong(ImmuneKey, 0L) > ZNet.instance.GetTime().Ticks;
        }

        /// <summary>A paralysis ended (its owner): immune for the next 6 s of network time.</summary>
        public static void StartImmunity(Character? character)
        {
            ZDO? zdo = OwnedZdo(character);
            if (zdo != null && ZNet.instance != null)
            {
                zdo.Set(ImmuneKey, ZNet.instance.GetTime().AddSeconds(ImmuneSeconds).Ticks);
            }
        }

        private static ZDO? OwnedZdo(Character? character) =>
            character != null && character.m_nview != null && character.m_nview.IsValid() && character.m_nview.IsOwner()
                ? character.m_nview.GetZDO() : null;

        /// <summary>On the character's owner (the only peer holding its status effects).</summary>
        public static bool Has(Character character) =>
            Time.time <= _until && character.GetSEMan() != null && character.GetSEMan().HaveStatusEffect(EcfParalyze.Hash);

        /// <summary>Adds the template to an ObjectDB's status effects unless one of that name is there.</summary>
        public static void Register(ObjectDB db)
        {
            BorrowLook(db);
            List<StatusEffect> list = db.m_StatusEffects;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].NameHash() == EcfParalyze.Hash)
                {
                    return;
                }
            }
            list.Add(Template);
        }

        // The look of the game's own shock (the Lightning status a lightning hit gives): its start effects are networked
        // and attached to the character, so every client near the paralysed creature sees the crackle, removed on its end.
        private static void BorrowLook(ObjectDB db)
        {
            StatusEffect? shock = Template.m_startEffects.m_effectPrefabs.Length == 0 ? db.GetStatusEffect(SEMan.s_statusEffectLightning) : null;
            if (shock != null)
            {
                Template.m_startEffects = shock.m_startEffects;
            }
        }

        private static EcfParalyze Template
        {
            get
            {
                if (_template == null)
                {
                    _template = ScriptableObject.CreateInstance<EcfParalyze>();
                    _template.name = EcfParalyze.EffectName;
                    _template.hideFlags = HideFlags.HideAndDontSave;
                    _template.m_name = "$ecf_fx_paralyze";
                    _template.m_ttl = 1f;
                    _template.m_startMessage = "";
                    _template.m_stopMessage = "";
                }
                return _template;
            }
        }
    }

    [HarmonyPatch]
    internal static class ParalysisPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        private static void Awake(ObjectDB __instance) => Paralysis.Register(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static void Copy(ObjectDB __instance) => Paralysis.Register(__instance);

        // MonsterAI starts every attack here, on the creature's owner.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        private static bool StartAttack(Humanoid __instance, ref bool __result)
        {
            if (!Paralysis.Has(__instance))
            {
                return true;
            }
            __result = false;
            return false;
        }

        // The swing it was in when paralysed does not land (the animation's trigger, on the owner).
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.OnAttackTrigger))]
        private static bool AttackTrigger(Humanoid __instance) => !Paralysis.Has(__instance);
    }
}
