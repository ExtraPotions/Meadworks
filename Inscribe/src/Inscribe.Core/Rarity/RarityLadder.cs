// Inscribe - ExtraPotions Meadworks gear inscriptions. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Changed by expDARE, 2026: Unity-free rarity rungs and rolling settings with the default ladder as code, the promotion
// count as a pure function, and the rarity lookup of an item state; MeadWorks Epic and Legendary rungs (tier floor,
// source-only).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCrafting 0.7.0 (Rules/Model/RarityDef.cs, Rolling/RollOps.cs Promote,
// config/EliteCrafting_economy.yml rarities and rolling), geraldjglasgow/ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCrafting 0.10.0 (config/EliteCrafting_economy.yml rarities: Magic 2, Rare 3),
// geraldjglasgow/ValheimMods d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Inscribe.Rarity
{
    /// <summary>One rung of the ladder (design A1). Ladder order is <see cref="Index"/>; 0 is the base rarity (Normal).</summary>
    public sealed class RarityDef
    {
        public RarityDef(string id, int index, string color, bool glow, int minInscriptions, int maxInscriptions, int? maxPrefixes = null,
            int? maxSuffixes = null, float dropWeight = 1f, int tierFloor = 0, bool sourceOnly = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Expected a rarity id.", nameof(id));
            if (minInscriptions < 0 || maxInscriptions < minInscriptions) throw new ArgumentException(id + ": expected 0 <= min <= max.", nameof(minInscriptions));
            Id = id;
            Index = index;
            Color = color;
            Glow = index != 0 && glow;
            MinInscriptions = minInscriptions;
            MaxInscriptions = maxInscriptions;
            MaxPrefixes = maxPrefixes ?? maxInscriptions;
            MaxSuffixes = maxSuffixes ?? maxInscriptions;
            DropWeight = Math.Max(0f, dropWeight);
            TierFloor = Math.Max(0, tierFloor);
            SourceOnly = sourceOnly;
        }

        /// <summary>A fresh roll at this rarity keeps only the best this-many open tiers; 0 = all (YAML <c>tier_floor</c>).</summary>
        public int TierFloor { get; }

        /// <summary>Never drops, never comes from a rune or promotion: only a named source hands it out (YAML <c>source_only</c>).</summary>
        public bool SourceOnly { get; }

        public string Id { get; }
        public int Index { get; }

        /// <summary><c>#RRGGBB</c>, the single source for every rarity-coloured surface.</summary>
        public string Color { get; }

        /// <summary>Ground glow; always false on the base rarity.</summary>
        public bool Glow { get; }

        public int MinInscriptions { get; }
        public int MaxInscriptions { get; }

        /// <summary>At most this many prefix inscriptions (YAML <c>prefixes</c>; default: the maximum count).</summary>
        public int MaxPrefixes { get; }

        /// <summary>At most this many suffix inscriptions (YAML <c>suffixes</c>; default: the maximum count).</summary>
        public int MaxSuffixes { get; }

        /// <summary>Multiplier on this rarity's drop weights; 0 never drops.</summary>
        public float DropWeight { get; }

        public bool IsBase => Index == 0;

        /// <summary>The limit for one kind of inscription.</summary>
        public int Limit(InscriptionKind kind) => kind == InscriptionKind.Prefix ? MaxPrefixes : MaxSuffixes;

        public override string ToString() => Id;
    }

    /// <summary>The <c>rolling:</c> section (design A6, A7).</summary>
    public sealed class RollingSettings
    {
        public RollingSettings(float allowedClosedFraction = 0.334f, int promoteAddsAtLeast = 1, IReadOnlyDictionary<string, IReadOnlyDictionary<int, float>>? countWeights = null)
        {
            AllowedClosedFraction = allowedClosedFraction;
            PromoteAddsAtLeast = Math.Max(0, promoteAddsAtLeast);
            CountWeights = countWeights ?? new Dictionary<string, IReadOnlyDictionary<int, float>>();
        }

        /// <summary>On a class where an inscription is only <c>allowed</c>, its top <c>floor(k * fraction)</c> tiers stay closed.</summary>
        public float AllowedClosedFraction { get; }

        public int PromoteAddsAtLeast { get; }

        /// <summary>rarity id to (inscription count to weight). A rarity absent here draws its count uniformly.</summary>
        public IReadOnlyDictionary<string, IReadOnlyDictionary<int, float>> CountWeights { get; }
    }

    /// <summary>The rarity ladder in YAML order (design A1, A7); the default is EliteCrafting 0.7.0's.</summary>
    public sealed class RarityLadder
    {
        private readonly RarityDef[] rarities;

        public RarityLadder(IEnumerable<RarityDef> rarities, RollingSettings rolling)
        {
            this.rarities = (rarities ?? throw new ArgumentNullException(nameof(rarities))).OrderBy(r => r.Index).ToArray();
            if (this.rarities.Length == 0 || this.rarities[0].Index != 0) throw new ArgumentException("A ladder starts with its base rarity at index 0.", nameof(rarities));
            if (this.rarities.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != this.rarities.Length) throw new ArgumentException("Duplicate rarity id.", nameof(rarities));
            Rolling = rolling ?? throw new ArgumentNullException(nameof(rolling));
        }

        /// <summary>EliteCrafting 0.7.0 defaults: normal (0, white, no drop), magic (1 to 2, one prefix and one suffix), rare (3 to 6, three and three).</summary>
        public static RarityLadder Default { get; } = new RarityLadder(new[]
        {
            new RarityDef("normal", 0, "#FFFFFF", false, 0, 0, dropWeight: 0f),
            new RarityDef("magic", 1, "#1EFF00", true, 2, 2, 1, 1),
            new RarityDef("rare", 2, "#0070DD", true, 3, 3, 2, 2),
            new RarityDef(RarityTiers.Epic, 3, "#A335EE", true, 4, 4, 2, 2, tierFloor: 4),
            new RarityDef(RarityTiers.Legendary, 4, "#FFB400", true, 5, 5, 3, 3, dropWeight: 0f, tierFloor: 2, sourceOnly: true),
        }, new RollingSettings());

        public IReadOnlyList<RarityDef> Rarities => rarities;

        public RollingSettings Rolling { get; }

        public RarityDef Base => rarities[0];

        public RarityDef? Find(string? id) => id == null ? null : rarities.FirstOrDefault(r => r.Id == id);

        /// <summary>The rarity of an item: no rarity key is the base rarity; an id the ladder does not know is null.</summary>
        public RarityDef? Of(ItemState state) => state.RarityId == null ? Base : Find(state.RarityId);

        /// <summary>One rung up; null at the top.</summary>
        public RarityDef? Next(RarityDef rarity)
        {
            var i = Array.IndexOf(rarities, rarity);
            return i >= 0 && i + 1 < rarities.Length ? rarities[i + 1] : null;
        }

        /// <summary>
        /// Inscriptions a promotion to <paramref name="to"/> adds (EliteCrafting <c>RollOps.Promote</c>): every one kept,
        /// <c>max(to.min - count, promote_adds_at_least)</c> added, capped at <c>to.max - count</c>.
        /// </summary>
        public int PromotionAdds(int count, RarityDef to) =>
            Math.Min(Math.Max(to.MinInscriptions - count, Rolling.PromoteAddsAtLeast), Math.Max(to.MaxInscriptions - count, 0));
    }
}
