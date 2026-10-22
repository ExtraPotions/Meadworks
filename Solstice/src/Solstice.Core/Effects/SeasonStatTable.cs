// Solstice - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from Seasons 1.10.6 by shudnal (https://github.com/shudnal/Seasons), commit 4054af6e8823d787921d673607e4942554872b66,
// Unlicense (public domain), SeasonSettings/SeasonStats.cs (the Stats fields and the default values). See THIRD_PARTY_NOTICES/Seasons-Unlicense.txt.
// Changed by expDARE, 2026: the default values are copied verbatim; the table is plain Unity-free data (skill and damage names are
// the game's enum names as text). The season status effect reads this table.
using System.Collections.Generic;
using ExtraPotions.Solstice.Calendar;

namespace ExtraPotions.Solstice.Effects
{
    /// <summary>The stat modifiers a season gives the player (the reference's season status effect), as data.</summary>
    public sealed class SeasonStatModifiers
    {
        public float TickInterval { get; set; }
        public float HealthPerTickMinHealthPercentage { get; set; }
        public float HealthPerTick { get; set; }
        public string HealthHitType { get; set; } = "";

        public float RunStaminaDrainModifier { get; set; }
        public float JumpStaminaUseModifier { get; set; }
        public float AttackStaminaUseModifier { get; set; }
        public float BlockStaminaUseModifier { get; set; }
        public float BlockStaminaUseFlatValue { get; set; }
        public float DodgeStaminaUseModifier { get; set; }
        public float SwimStaminaUseModifier { get; set; }
        public float HomeItemStaminaUseModifier { get; set; }
        public float SneakStaminaUseModifier { get; set; }
        public float RunStaminaUseModifier { get; set; }

        public float HealthRegenMultiplier { get; set; } = 1f;
        public float StaminaRegenMultiplier { get; set; } = 1f;
        public float EitrRegenMultiplier { get; set; } = 1f;

        /// <summary>Skill name (the game's <c>Skills.SkillType</c> name) to the skill gain multiplier.</summary>
        public Dictionary<string, float> RaiseSkills { get; } = new Dictionary<string, float>();

        /// <summary>Skill name to the skill levels added.</summary>
        public Dictionary<string, float> SkillLevels { get; } = new Dictionary<string, float>();

        public Dictionary<string, float> ModifyAttackSkills { get; } = new Dictionary<string, float>();

        /// <summary>Damage type name (<c>HitData.DamageType</c>) to damage modifier name (<c>HitData.DamageModifier</c>).</summary>
        public Dictionary<string, string> DamageModifiers { get; } = new Dictionary<string, string>();

        public float NoiseModifier { get; set; }
        public float StealthModifier { get; set; }
        public float AddMaxCarryWeight { get; set; }
        public float SpeedModifier { get; set; }
        public float SwimSpeedModifier { get; set; }
        public float MaxMaxFallSpeed { get; set; }
        public float FallDamageModifier { get; set; }
        public float AdrenalineModifier { get; set; }
        public float StaggerModifier { get; set; }
        public float TimedBlockBonus { get; set; }
    }

    public static class SeasonStatTable
    {
        /// <summary>A fresh copy of the reference defaults of a season (SeasonStats constructor).</summary>
        public static SeasonStatModifiers Defaults(Season season)
        {
            var s = new SeasonStatModifiers();
            switch (season)
            {
                case Season.Spring:
                    s.TickInterval = 5f;
                    s.HealthPerTick = 1f;
                    s.DamageModifiers["Poison"] = "Resistant";
                    foreach (var skill in new[] { "Jump", "Sneak", "Run", "Swim" })
                    {
                        s.RaiseSkills[skill] = 1.2f;
                        s.SkillLevels[skill] = 15f;
                    }
                    break;
                case Season.Summer:
                    s.RunStaminaDrainModifier = -0.1f;
                    s.JumpStaminaUseModifier = -0.1f;
                    s.HealthRegenMultiplier = 1.1f;
                    s.NoiseModifier = -0.2f;
                    s.StealthModifier = 0.2f;
                    s.SpeedModifier = 0.05f;
                    foreach (var skill in new[] { "Jump", "Sneak", "Run", "Swim" })
                    {
                        s.RaiseSkills[skill] = 1.1f;
                        s.SkillLevels[skill] = 10f;
                    }
                    break;
                case Season.Fall:
                    s.EitrRegenMultiplier = 1.1f;
                    foreach (var skill in new[] { "WoodCutting", "Fishing", "Pickaxes" })
                    {
                        s.RaiseSkills[skill] = 1.2f;
                        s.SkillLevels[skill] = 15f;
                    }
                    break;
                case Season.Winter:
                    s.StaminaRegenMultiplier = 1.1f;
                    s.NoiseModifier = 0.2f;
                    s.StealthModifier = -0.2f;
                    s.SpeedModifier = -0.05f;
                    s.FallDamageModifier = -0.3f;
                    s.DamageModifiers["Fire"] = "Resistant";
                    foreach (var skill in new[] { "WoodCutting", "Fishing", "Pickaxes" })
                    {
                        s.RaiseSkills[skill] = 1.1f;
                        s.SkillLevels[skill] = 10f;
                    }
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(season));
            }
            return s;
        }
    }
}
