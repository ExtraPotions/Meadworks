// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; tier floor and
// source-only rarities (MeadWorks Epic and Legendary).
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>One rung of the ladder (rarity.md, economy-yaml.md section 2). Ladder order = <see cref="Index"/>.</summary>
    public sealed class RarityDef
    {
        public string Id { get; internal set; } = "";

        /// <summary>0 for the base rarity (Normal).</summary>
        public int Index { get; internal set; }

        /// <summary>A <c>$key</c> (default <c>$ecf_rarity_&lt;id&gt;</c>) or literal text.</summary>
        public string Name { get; internal set; } = "";

        /// <summary><c>#RRGGBB</c>, the single source for every rarity-colored surface.</summary>
        public string Color { get; internal set; } = "#FFFFFF";

        public Color32 Color32 { get; internal set; } = new Color32(255, 255, 255, 255);

        /// <summary>Ground glow; always false on the base rarity.</summary>
        public bool Glow { get; internal set; }

        public int MinAffixes { get; internal set; }
        public int MaxAffixes { get; internal set; }

        /// <summary>At most this many prefix inscriptions (YAML <c>prefixes</c>; default: the maximum count).</summary>
        public int MaxPrefixes { get; internal set; }

        /// <summary>At most this many suffix inscriptions (YAML <c>suffixes</c>; default: the maximum count).</summary>
        public int MaxSuffixes { get; internal set; }

        /// <summary>The limit for one kind of inscription.</summary>
        public int Limit(AffixKind kind) => kind == AffixKind.Prefix ? MaxPrefixes : MaxSuffixes;

        /// <summary>Multiplier on this rarity's drop weights; 0 = never drops.</summary>
        public float DropWeight { get; internal set; } = 1f;

        /// <summary>A fresh roll at this rarity keeps only the best this-many open tiers (YAML <c>tier_floor</c>); 0 = all.</summary>
        public int TierFloor { get; internal set; }

        /// <summary>
        /// Never from creature or chest drops, runes or promotions (YAML <c>source_only</c>): only the sources that ask for it
        /// by id (Wildblood bosses and raids through <c>meadworks.inscribe.legendary_reward</c>).
        /// </summary>
        public bool SourceOnly { get; internal set; }

        public bool IsBase => Index == 0;

        /// <summary>Display's icon backdrop tone, worked out once per definition (<c>Display/Backdrops/BackdropTone</c>).</summary>
        internal Color? BackdropTone;
    }

    /// <summary>The <c>rolling:</c> section (rarity.md section 4, classes-and-tiers.md section 5).</summary>
    public sealed class RollingSettings
    {
        /// <summary>
        /// On a class where an inscription is only <c>allowed</c>, its top <c>floor(k * fraction)</c> tiers stay closed
        /// (default 0.334: 13 tiers stop at T5, 8 at T3).
        /// </summary>
        public float AllowedClosedFraction { get; internal set; } = 0.334f;

        public int PromoteAddsAtLeast { get; internal set; } = 1;

        /// <summary>rarity id → (affix count → weight). A rarity absent here draws its count uniformly.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyDictionary<int, float>> CountWeights
        { get; internal set; } = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyDictionary<int, float>>();
    }
}
