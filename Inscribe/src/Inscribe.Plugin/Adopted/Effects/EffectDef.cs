// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>How an effect reaches the game (effects-runtime.md section 1).</summary>
    public enum EffectRoute
    {
        /// <summary>A channel summed into the hidden aggregate status effect.</summary>
        Aggregate,
        /// <summary>A targeted prefix/postfix, or a field written on rebuild.</summary>
        Hook,
    }

    /// <summary>Whether values sum across equipped items (player-global) or apply to the item they sit on only.</summary>
    public enum EffectScope
    {
        PlayerGlobal,
        ItemLocal,
    }

    /// <summary>Which way a positive stored value moves the stat; drives the tooltip sign.</summary>
    public enum EffectPolarity
    {
        Raise,
        Lower,
    }

    /// <summary>What an affix's <c>param</c> names for this effect (affixes.md section 3).</summary>
    public enum EffectParamKind
    {
        None,
        Skill,
        DamageType,
        Element,
        CreatureFamily,
        Resource,
        Aura,
    }

    /// <summary>The affix value types an effect accepts.</summary>
    [Flags]
    public enum ValueTypes
    {
        None = 0,
        Percent = 1,
        Flat = 2,
        Flag = 4,
    }

    /// <summary>
    /// One registered effect: the code-side half of an affix. YAML affixes may only name registered ids; this record
    /// is everything validation, channel building and the tooltip need to know about it. Immutable.
    /// </summary>
    public sealed class EffectDef
    {
        public EffectDef(string id, EffectRoute route, EffectScope scope, ValueTypes values, EffectParamKind param,
            EffectPolarity polarity, float? cap, HookDifficulty hook, int phase, string description)
        {
            Id = id;
            Route = route;
            Scope = scope;
            Values = values;
            Param = param;
            Polarity = polarity;
            DefaultCap = cap;
            Hook = hook;
            Phase = phase;
            Description = description;
        }

        public string Id { get; }
        public EffectRoute Route { get; }
        public EffectScope Scope { get; }
        public ValueTypes Values { get; }
        public EffectParamKind Param { get; }
        public EffectPolarity Polarity { get; }

        /// <summary><c>higher</c> (default) or <c>lower</c>: for effects where a low roll is the good one.</summary>
        public bool BetterLower { get; set; }

        /// <summary>Default cap on the channel sum; the YAML <c>caps:</c> map overrides it. Null = uncapped.</summary>
        public float? DefaultCap { get; }

        public HookDifficulty Hook { get; }
        public int Phase { get; }

        /// <summary>What the code hooks, for <c>ecraft</c> listings and the log.</summary>
        public string Description { get; }

        public bool Accepts(AffixValueType type)
        {
            ValueTypes flag = type == AffixValueType.Percent ? ValueTypes.Percent
                : type == AffixValueType.Flat ? ValueTypes.Flat : ValueTypes.Flag;
            return (Values & flag) != 0;
        }
    }
}
