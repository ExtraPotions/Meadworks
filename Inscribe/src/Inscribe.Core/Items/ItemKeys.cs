// Inscribe - ExtraPotions Meadworks gear inscriptions. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Changed by expDARE, 2026: the custom-data key names of both sources in one table (design 10.2: Inscribe reads and
// writes the sources' own keys, so no item migration pass exists).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCrafting 0.7.0 (Affixes/ItemKeys.cs, Loot/LootKeys.cs),
// geraldjglasgow/ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Items
{
    /// <summary>
    /// Every <c>m_customData</c> key Inscribe touches. Inscribe only reads, writes or removes these, never clears the
    /// dictionary, and leaves every other key (other mods') exactly as it was. The ItemEnhancements names are key strings
    /// only (facts, not code); the removed enhancement's conversion is in <c>Enhancement.EnhancementConversion</c>.
    /// </summary>
    public static class ItemKeys
    {
        public const string EcPrefix = "ecf_";

        /// <summary>Format version, integer; present whenever any other EliteCrafting key is.</summary>
        public const string EcVersion = "ecf_v";

        /// <summary>Rarity id; absent is Normal. Never written as the base rarity.</summary>
        public const string EcRarity = "ecf_rarity";

        /// <summary><c>id:grade:value;id:grade:value</c>, the inscriptions in display order.</summary>
        public const string EcInscriptions = "ecf_inscriptions";

        /// <summary>The same list under its name before 2026-10-01: read when the new key is absent, removed on write.</summary>
        public const string EcLegacyInscriptions = "ecf_affixes";

        /// <summary>Sealed reason id (<see cref="EcSealedSerpent"/>); any value is sealed.</summary>
        public const string EcSealed = "ecf_sealed";

        /// <summary>Reserved, never written (the item level is computed); preserved when present.</summary>
        public const string EcTier = "ecf_tier";

        /// <summary>A world chest already rolled its runes and gear (design D5); on the container's ZDO.</summary>
        public const string EcChestFilled = "ecf_filled";

        public const int EcCurrentFormat = 2;
        public const string EcSealedSerpent = "serpent";

        /// <summary>Keys of the retired EliteCrafting systems: never read, removed the next time the item is written.</summary>
        public static IReadOnlyList<string> EcRetired { get; } = new[] { "ecf_bound", "ecf_refine", "ecf_sigil", "ecf_sockets", "ecf_gems", "ecf_catalyst" };

        /// <summary>The removed enhancement level (ItemEnhancements' key), integer text 0 to 20: read once for the conversion, never written or removed.</summary>
        public const string EnhancementLevel = "ValheimEnhancement_Level";

        /// <summary>The removed "Enhanced by" player name (ItemEnhancements' key): kept, never written.</summary>
        public const string EnhancementCrafter = "ValheimEnhancement_Crafter";

        /// <summary>Set once an enhanced item converted (Inscribe 0.2.0), <c>v1;level=N;steps=S;raised=R;runes=G</c>; its presence stops a second conversion.</summary>
        public const string EnhancementConverted = "ecf_enhancement_converted";
    }
}
