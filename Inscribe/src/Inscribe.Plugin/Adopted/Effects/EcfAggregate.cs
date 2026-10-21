// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The one hidden, never-expiring status effect that carries every player-global affix stat of the local player
    /// (effects-runtime.md section 3). Exists only on the local player's own client: the game runs a player's regen,
    /// stamina, movement, skills and outgoing attacks on the machine that owns that player, so nothing is sent.
    /// <para>
    /// Stats the game already has a field for are written into the <see cref="SE_Stats"/> fields and applied by the
    /// game's own code. The rest are overrides reading <see cref="AggregateHost.Current"/>: per-skill levels and gains
    /// (SE_Stats has two skill slots only), damage by type and at night, and sneaking speed. Every override is a few
    /// float reads; nothing allocates.
    /// </para>
    /// </summary>
    public sealed class EcfAggregate : SE_Stats
    {
        /// <summary>The ScriptableObject name; the game identifies status effects by its hash (not by <c>m_name</c>).</summary>
        public const string EffectName = "ECF_Aggregate";

        public static readonly int Hash = EffectName.GetStableHashCode();

        public override void ModifySkillLevel(Skills.SkillType skill, ref float level)
        {
            level += AggregateHost.Current.SkillLevelFor(skill);
        }

        // Multiplies the gain multiplier (starts at 1), so the channel is multiplicative with other status effects
        // and additive inside itself (effects-runtime.md section 5).
        public override void ModifyRaiseSkill(Skills.SkillType skill, ref float value)
        {
            float gain = AggregateHost.Current.SkillGainFor(skill);
            if (gain != 0f)
            {
                value *= 1f + gain;
            }
        }

        // Runs on the attacker's client while the hit is built (Attack), so the raised numbers travel inside the
        // HitData to whoever owns the target. Brands are already in the weapon's damage (GetDamage postfix), so
        // "including parts added by other affixes" holds.
        public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
        {
            AggregateValues v = AggregateHost.Current;
            if (v.AnyDamage)
            {
                DamageSlots.Scale(ref hitData.m_damage, v.DamageDealt);
            }
            float night = v[EffectKind.NightDamage];
            if (night != 0f && EnvMan.IsNight())
            {
                hitData.m_damage.Modify(1f + night);
            }
            AttackBonuses.Apply(v, ref hitData);
        }

        // Base applies the native speed field (half while swimming) and swim speed; the situational channels add a
        // share of the same base, so they stack additively with the unconditional one (effects-runtime.md 5).
        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            base.ModifySpeed(baseSpeed, ref speed, character, dir);
            speed += baseSpeed * SpeedBonuses.Situational(AggregateHost.Current, character);
        }

        // The bulwark flags: the game's own Resistant step for their elements, "best wins" like a cape or a mead.
        public override void ModifyDamageMods(ref HitData.DamageModifiers modifiers)
        {
            base.ModifyDamageMods(ref modifiers);
            System.Collections.Generic.List<HitData.DamageModPair>? resist = AggregateHost.Current.ResistMods;
            if (resist != null)
            {
                modifiers.Apply(resist);
            }
        }

        /// <summary>Writes a value set into the native fields; the game's own <c>Modify*</c> code reads them live.</summary>
        internal void ApplyFields(AggregateValues v)
        {
            m_speedModifier = v[EffectKind.MoveSpeed];
            m_addMaxCarryWeight = v[EffectKind.CarryCapacity];
            m_healthRegenMultiplier = 1f + v[EffectKind.HealthRecovery];
            m_staminaRegenMultiplier = 1f + v[EffectKind.StaminaRecovery];
            m_eitrRegenMultiplier = 1f + v[EffectKind.EitrRecovery];
            m_runStaminaDrainModifier = -v[EffectKind.RunStaminaCost];
            m_jumpStaminaUseModifier = -v[EffectKind.JumpStaminaCost];
            m_attackStaminaUseModifier = -v[EffectKind.AttackStaminaCost];
            m_blockStaminaUseModifier = -v[EffectKind.BlockStaminaCost];
            m_dodgeStaminaUseModifier = -v[EffectKind.DodgeStaminaCost];
            m_homeItemStaminaUseModifier = -v[EffectKind.HomeItemStaminaCost];
            m_jumpModifier = new Vector3(0f, v[EffectKind.JumpHeight], 0f);
            m_timedBlockBonus = v[EffectKind.ParryBonus];
            ApplyPhase2Fields(v);
        }

        // Noise and stealth factors: lower is quieter / harder to see (Player.UpdateStealth, Character.RPC_AddNoise).
        // Stagger: SEMan.ModifyStagger adds base × modifier. Raven's Glide caps the fall speed and zeroes fall damage.
        private void ApplyPhase2Fields(AggregateValues v)
        {
            bool glide = v[EffectKind.SlowFall] > 0f;
            m_fallDamageModifier = glide ? -1f : -v[EffectKind.FallDamageTaken];
            m_maxMaxFallSpeed = glide ? SlowFallSpeed : 0f;
            m_noiseModifier = -v[EffectKind.NoiseMade];
            m_stealthModifier = -v[EffectKind.Stealth];
            m_staggerModifier = -v[EffectKind.StaggerTaken];
            m_swimSpeedModifier = v[EffectKind.Swimmer];
            m_swimStaminaUseModifier = -v[EffectKind.Swimmer];
        }

        /// <summary>Raven's Glide: the most a gliding wearer falls per second (judgement call; ordinary falls reach far more).</summary>
        internal const float SlowFallSpeed = 5f;
    }
}
