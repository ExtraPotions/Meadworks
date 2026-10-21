// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Effects;
using ExtraPotions.Inscribe.Adopted.Items;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// One affix of the running configuration, fully merged and validated (configuration.md section 6, format 2 in
    /// classes-and-tiers.md section 3). Built by the rules loader and never changed afterwards: treat every setter as
    /// loader-only.
    /// </summary>
    public sealed class AffixDef
    {
        public string Id { get; internal set; } = "";

        /// <summary>A <c>$key</c> (default <c>$ecf_affix_&lt;id&gt;</c>) or literal text shown in every language.</summary>
        public string Name { get; internal set; } = "";

        public string Effect { get; internal set; } = "";
        public EffectDef EffectDef { get; internal set; } = null!;

        /// <summary>The raw param text (<c>Swords</c>, <c>Run,Jump,Swim,Sneak</c>, <c>physical</c>), or null.</summary>
        public string? Param { get; internal set; }

        /// <summary>Parsed <c>skill</c> param; <c>Skills.SkillType.All</c> for <c>All</c>. Empty for other kinds.</summary>
        public IReadOnlyList<Skills.SkillType> ParamSkills { get; internal set; } = Array.Empty<Skills.SkillType>();

        /// <summary>Parsed <c>damage_type</c> / <c>element</c> param; None for other kinds.</summary>
        public DamageMask ParamDamage { get; internal set; }

        public AffixValueType Value { get; internal set; }
        public AffixUnit Unit { get; internal set; }

        /// <summary>YAML <c>affix</c>: prefix or suffix, counted against the rarity's limits.</summary>
        public AffixKind Kind { get; internal set; }

        /// <summary>Display grouping (snake_case), e.g. <c>weapon_damage</c>.</summary>
        public string Family { get; internal set; } = "";

        /// <summary>Item class ids it rolls on with every tier open (<c>classes.best</c>).</summary>
        public IReadOnlyList<string> BestClasses { get; internal set; } = Array.Empty<string>();

        /// <summary>Item class ids it rolls on with its top tiers closed (<c>classes.allowed</c>).</summary>
        public IReadOnlyList<string> AllowedClasses { get; internal set; } = Array.Empty<string>();

        /// <summary><c>scaled: true</c>: a rolled value is multiplied by the item class's <c>damage_scale</c>.</summary>
        public bool Scaled { get; internal set; }

        public AffixRequirements Requires { get; internal set; } = AffixRequirements.Any;
        public AffixCategory Category { get; internal set; }
        public string? ExclusionGroup { get; internal set; }
        public float Weight { get; internal set; } = 100f;
        public bool Enabled { get; internal set; } = true;
        public HookDifficulty Hook { get; internal set; }

        /// <summary>One row per tier, sorted by grade: the weakest tier first, the strongest last.</summary>
        public IReadOnlyList<AffixTierDef> Tiers { get; internal set; } = Array.Empty<AffixTierDef>();

        /// <summary>k, the number of the ladder's weakest tier (T<c>k</c>): shown tier = k + 1 - grade.</summary>
        public int TierCount { get; internal set; }

        /// <summary>Index into <see cref="AffixRules.Channels"/>; -1 when the affix is disabled.</summary>
        public int ChannelIndex { get; internal set; } = -1;

        /// <summary>How it fits an item class: best, allowed, or not at all.</summary>
        public ClassFit FitFor(string? classId)
        {
            if (classId == null)
            {
                return ClassFit.None;
            }
            if (Contains(BestClasses, classId))
            {
                return ClassFit.Best;
            }
            return Contains(AllowedClasses, classId) ? ClassFit.Allowed : ClassFit.None;
        }

        /// <summary>The row of a strength grade (1 = the weakest tier), or null when the affix does not define it.</summary>
        public AffixTierDef? TierRow(int grade)
        {
            for (int i = 0; i < Tiers.Count; i++)
            {
                if (Tiers[i].Grade == grade)
                {
                    return Tiers[i];
                }
            }
            return null;
        }

        /// <summary>The tier players see for a stored grade, counted down within this ladder (T1 strongest), at least 1.</summary>
        public int ShownTier(int grade) => Math.Max(1, TierCount + 1 - grade);

        /// <summary>The grade of a tier as players and the YAML write it (1 = strongest).</summary>
        public int GradeOf(int shown) => TierCount + 1 - shown;

        /// <summary>The grade of the weakest row, 0 without rows.</summary>
        public int WeakestGrade => Tiers.Count > 0 ? Tiers[0].Grade : 0;

        /// <summary>The grade of the strongest row, 0 without rows.</summary>
        public int StrongestGrade => Tiers.Count > 0 ? Tiers[Tiers.Count - 1].Grade : 0;

        private static bool Contains(IReadOnlyList<string> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == id)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// One tier row: the strength grade it is stored under, the tier players see, the item level that unlocks it,
    /// bounds, draw weight, and the decimals a rolled value is rounded to.
    /// </summary>
    public sealed class AffixTierDef
    {
        /// <summary>Strength grade, 1 = the weakest tier (T<c>k</c>). What item data stores.</summary>
        public int Grade { get; internal set; }

        /// <summary>The tier as shown and written in the YAML, 1 = the strongest.</summary>
        public int Shown { get; internal set; }

        /// <summary>The item level (1-8) that unlocks the tier.</summary>
        public int Level { get; internal set; } = 1;

        /// <summary>0 for flags.</summary>
        public float Min { get; internal set; }

        /// <summary>0 for flags.</summary>
        public float Max { get; internal set; }

        /// <summary>Draw weight; the tier number by default (weaker tiers roll more often).</summary>
        public float Weight { get; internal set; } = 1f;

        /// <summary>Decimals of a rolled value, at most 2.</summary>
        public int Decimals { get; internal set; }
    }

    /// <summary>The <c>requires</c> block: every present part must hold.</summary>
    public sealed class AffixRequirements
    {
        public static readonly AffixRequirements Any = new AffixRequirements();

        /// <summary>Any of these must govern the item. Empty = no skill requirement.</summary>
        public IReadOnlyList<Skills.SkillType> Skills { get; internal set; } = Array.Empty<Skills.SkillType>();

        public Hands Hands { get; internal set; }

        /// <summary>All of these.</summary>
        public ItemTraits Traits { get; internal set; }

        public bool IsAny => Skills.Count == 0 && Hands == Hands.None && Traits == ItemTraits.None;
    }
}
