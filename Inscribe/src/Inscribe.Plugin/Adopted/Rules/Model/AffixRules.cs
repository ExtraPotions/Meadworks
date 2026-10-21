// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Effects;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// The affix family (<c>ExtraPotions.Inscribe.Adopted_affixes*.yml</c>) after merging and validation, with the lookups every
    /// reader needs precomputed at load: by id, the channel table, and the rollable pools per item class. Immutable.
    /// </summary>
    public sealed class AffixRules
    {
        public static readonly AffixRules Empty = new AffixRules();

        /// <summary>The merged <c>caps:</c> map, channel key → cap.</summary>
        public IReadOnlyDictionary<string, float> Caps { get; internal set; } = new Dictionary<string, float>();

        /// <summary>Every affix, enabled or not, in file order.</summary>
        public IReadOnlyList<AffixDef> Affixes { get; internal set; } = Array.Empty<AffixDef>();

        public IReadOnlyDictionary<string, AffixDef> ById { get; internal set; } = new Dictionary<string, AffixDef>();

        /// <summary>One channel per distinct (effect, param) of the enabled affixes; index = AffixDef.ChannelIndex.</summary>
        public IReadOnlyList<ChannelDef> Channels { get; internal set; } = Array.Empty<ChannelDef>();

        internal Dictionary<string, PoolEntry[]> Pools { get; set; } = new Dictionary<string, PoolEntry[]>(StringComparer.Ordinal);

        public AffixDef? Get(string? id) => id != null && ById.TryGetValue(id, out AffixDef def) ? def : null;

        /// <summary>
        /// Enabled affixes with weight above 0 that list the item class in <c>classes.best</c> or <c>classes.allowed</c>,
        /// in file order, each with its fit. Level, tier, requires, limit and exclusion filters are the roller's job.
        /// </summary>
        public IReadOnlyList<PoolEntry> Pool(string? classId) =>
            classId != null && Pools.TryGetValue(classId, out PoolEntry[] pool) ? pool : Array.Empty<PoolEntry>();

        /// <summary>The class ids any affix names (pools exist for exactly these).</summary>
        public IEnumerable<string> PooledClasses => Pools.Keys;
    }

    /// <summary>One affix of a class's pool and how it fits the class.</summary>
    public readonly struct PoolEntry
    {
        public PoolEntry(AffixDef def, ClassFit fit)
        {
            Def = def;
            Fit = fit;
        }

        public AffixDef Def { get; }
        public ClassFit Fit { get; }
    }

    /// <summary>
    /// A summing channel: every enabled affix with the same effect and param feeds it. Channel indexes are
    /// assigned at load so the effects layer never looks anything up by name at runtime.
    /// </summary>
    public sealed class ChannelDef
    {
        public int Index { get; internal set; }

        /// <summary><c>effect</c> or <c>effect:param</c>.</summary>
        public string Key { get; internal set; } = "";

        public EffectDef Effect { get; internal set; } = null!;
        public string? Param { get; internal set; }

        /// <summary>Cap on the sum, or <see cref="float.PositiveInfinity"/> when uncapped.</summary>
        public float Cap { get; internal set; } = float.PositiveInfinity;

        /// <summary>An affix feeding this channel, to read its parsed param (skills, damage types).</summary>
        public AffixDef Sample { get; internal set; } = null!;
    }
}
