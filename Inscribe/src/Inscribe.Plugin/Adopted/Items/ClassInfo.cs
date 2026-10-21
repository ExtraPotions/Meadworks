// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>One- or two-handed, for <c>requires.hands</c>. <see cref="None"/> on items that are not held weapons.</summary>
    public enum Hands
    {
        None,
        One,
        Two,
    }

    /// <summary>Item properties an affix may require (<c>requires.traits</c>, all of).</summary>
    [Flags]
    public enum ItemTraits
    {
        None = 0,
        WearsOut = 1,
        MovementPenalty = 2,
        Builds = 4,
        Projectile = 8,
        Ammo = 16,
        CanParry = 32,
    }

    /// <summary>
    /// What an item type is, for inscriptions (classes-and-tiers.md section 1): its item class, and beside it the hands,
    /// traits and governing skills the affixes' <c>requires</c> blocks read. Computed once per <c>SharedData</c> (per
    /// item type) by <see cref="ItemClasses.Classify"/> and kept until the rules or the class registry change; immutable.
    /// </summary>
    public sealed class ClassInfo
    {
        /// <summary>No class: the item never becomes magic.</summary>
        public static readonly ClassInfo None = new ClassInfo(null, Hands.None, ItemTraits.None, Array.Empty<Skills.SkillType>());

        public ClassInfo(ItemClass? itemClass, Hands hands, ItemTraits traits, IReadOnlyList<Skills.SkillType> skills)
        {
            Class = itemClass;
            Hands = hands;
            Traits = traits;
            GoverningSkills = skills;
        }

        /// <summary>The item's class; null = none.</summary>
        public ItemClass? Class { get; }

        /// <summary>The class id, or null.</summary>
        public string? ClassId => Class?.Id;

        /// <summary>A class whose items may become magic (<c>rolls: true</c>).</summary>
        public bool Rolls => Class != null && Class.Rolls;

        /// <summary>The class's <c>damage_scale</c> (1 without a class).</summary>
        public float DamageScale => Class?.DamageScale ?? 1f;

        public Hands Hands { get; }
        public ItemTraits Traits { get; }

        /// <summary>The item's own skill, plus WoodCutting when it chops and Pickaxes when it mines.</summary>
        public IReadOnlyList<Skills.SkillType> GoverningSkills { get; }

        public bool HasTraits(ItemTraits required) => (Traits & required) == required;

        public bool IsGovernedBy(Skills.SkillType skill)
        {
            for (int i = 0; i < GoverningSkills.Count; i++)
            {
                if (GoverningSkills[i] == skill)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
