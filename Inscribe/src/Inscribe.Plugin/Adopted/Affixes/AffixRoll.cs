// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;

namespace ExtraPotions.Inscribe.Adopted.Affixes
{
    /// <summary>
    /// One rolled affix on an item: its id, the tier it rolled at as a strength grade (1 = the inscription's weakest
    /// tier; shown as <c>AffixDef.ShownTier</c>) and the rolled value (already rounded and scaled at roll time, so what is
    /// stored is exactly what is displayed and applied; flags store 1). Immutable.
    /// </summary>
    public readonly struct AffixRoll : IEquatable<AffixRoll>
    {
        public AffixRoll(string id, int tier, float value)
        {
            Id = id;
            Tier = tier;
            Value = value;
        }

        public string Id { get; }

        /// <summary>The strength grade on the inscription's own ladder: 1 = T<c>k</c> (the weakest), k = T1.</summary>
        public int Tier { get; }
        public float Value { get; }

        public AffixRoll WithValue(float value) => new AffixRoll(Id, Tier, value);

        public bool Equals(AffixRoll other) => Id == other.Id && Tier == other.Tier && Value.Equals(other.Value);

        public override bool Equals(object? obj) => obj is AffixRoll other && Equals(other);

        public override int GetHashCode() => (Id?.GetHashCode() ?? 0) ^ (Tier * 397) ^ Value.GetHashCode();

        public override string ToString() => ItemCodec.Encode(this);
    }

    /// <summary>One position of the affix list: a parsed roll, or an unreadable segment kept verbatim.</summary>
    internal readonly struct ItemSegment
    {
        public ItemSegment(AffixRoll roll)
        {
            Roll = roll;
            Raw = null;
        }

        public ItemSegment(string raw)
        {
            Roll = default;
            Raw = raw;
        }

        public AffixRoll Roll { get; }

        /// <summary>Non-null for an unreadable segment, written back unchanged in its position.</summary>
        public string? Raw { get; }

        public bool IsRoll => Raw == null;
    }
}
