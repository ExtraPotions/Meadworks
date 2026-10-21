// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Hamstring: a hit from the local player carries this status effect's hash (only when the hit carries none of its
    /// own) and the slow percentage in the hit's skill-level field, which the game uses for nothing but the level of the
    /// carried status effect. The target's owner adds it through the game's own path (RPC_Damage →
    /// SEMan.AddStatusEffect(hash, ..., skillLevel) → <see cref="SetLevel"/>), so it works whoever owns the target; a
    /// repeat hit resets the timer and takes the new strength. Bosses and players are never slowed (checked by the
    /// attacker). Movement: the native speed field. Attacks: the owner plays attack animations slower while it lasts
    /// (the game resets animation speed when an attack ends).
    /// </summary>
    public sealed class EcfSlow : SE_Stats
    {
        public const string EffectName = "ECF_Hamstring";
        public const float Seconds = 2f;

        /// <summary>Judgement call: whatever the summed rolls, a hamstrung creature keeps at least 10% of its speed.</summary>
        private const float MaxSlow = 0.9f;

        public static readonly int Hash = EffectName.GetStableHashCode();

        private float _slow;

        public override void SetLevel(int itemLevel, float skillLevel)
        {
            base.SetLevel(itemLevel, skillLevel);
            _slow = Mathf.Clamp(skillLevel / 100f, 0f, MaxSlow);
            m_speedModifier = -_slow;
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (_slow > 0f && m_character.InAttack())
            {
                m_character.m_zanim.SetSpeed(1f - _slow);
            }
        }
    }

    /// <summary>Attacker side of Hamstring and the ObjectDB registration of <see cref="EcfSlow"/> (every peer).</summary>
    internal static class CreatureSlow
    {
        private static EcfSlow? _template;

        /// <summary>From the local player's outgoing hit (<see cref="OutgoingHits"/>).</summary>
        public static void Carry(AggregateValues v, Character target, HitData hit)
        {
            float slow = v[EffectKind.OnHitSlow];
            if (slow > 0f && hit.m_statusEffectHash == 0 && !target.IsBoss() && !target.IsPlayer())
            {
                hit.m_statusEffectHash = EcfSlow.Hash;
                hit.m_skillLevel = slow * 100f;
            }
        }

        /// <summary>Adds the template to an ObjectDB's status effects unless one of that name is there (the main-menu DB shares the asset's list).</summary>
        public static void Register(ObjectDB db)
        {
            List<StatusEffect> list = db.m_StatusEffects;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].NameHash() == EcfSlow.Hash)
                {
                    return;
                }
            }
            list.Add(Template);
        }

        private static EcfSlow Template
        {
            get
            {
                if (_template == null)
                {
                    _template = ScriptableObject.CreateInstance<EcfSlow>();
                    _template.name = EcfSlow.EffectName;
                    _template.hideFlags = HideFlags.HideAndDontSave;
                    _template.m_name = "$ecf_fx_hamstring";
                    _template.m_ttl = EcfSlow.Seconds;
                    _template.m_startMessage = "";
                    _template.m_stopMessage = "";
                }
                return _template;
            }
        }
    }

    [HarmonyPatch]
    internal static class SlowRegistrationPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        private static void Awake(ObjectDB __instance) => CreatureSlow.Register(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static void Copy(ObjectDB __instance) => CreatureSlow.Register(__instance);
    }
}
