// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
namespace ExtraPotions.Inscribe.Adopted.Affixes
{
    /// <summary>
    /// Every custom-data key the mod writes on an item (item-data.md section 3). The mod only ever reads, writes or
    /// removes these, never clears the dictionary, and leaves every other key (other mods') exactly as it was.
    /// </summary>
    public static class ItemKeys
    {
        public const string Prefix = "ecf_";

        /// <summary>Format version, integer; present whenever any other key is.</summary>
        public const string Version = "ecf_v";

        /// <summary>Rarity id; absent = Normal. Never written as the base rarity.</summary>
        public const string Rarity = "ecf_rarity";

        /// <summary>
        /// <c>id:grade:value;id:grade:value</c>, the item's inscriptions in display order. The middle field is the strength
        /// grade (1 = the inscription's weakest tier, T<c>k</c>), shown as T(k + 1 - grade) with that inscription's own k.
        /// </summary>
        public const string Affixes = "ecf_inscriptions";

        /// <summary>The same list under its name before 2026-10-01: read when the new key is absent, removed on write.</summary>
        public const string LegacyAffixes = "ecf_affixes";

        /// <summary>Sealed reason id (<see cref="SealedSerpent"/>); any value = sealed.</summary>
        public const string Sealed = "ecf_sealed";

        /// <summary>Socket count, integer 1-3 (sockets.md section 2); absent = none.</summary>
        public const string Sockets = "ecf_sockets";

        /// <summary>
        /// The filled sockets in socket order: <c>gem:inscription:grade:value;...</c>. Never longer than the socket count;
        /// a gem is replaced in place, never removed, so the empty sockets are always the last ones.
        /// </summary>
        public const string Gems = "ecf_gems";

        /// <summary>Reserved, never written (the item level is computed, classes-and-tiers.md section 2); preserved when present.</summary>
        public const string Tier = "ecf_tier";

        /// <summary>
        /// 2 since item classes and tier ladders (classes-and-tiers.md section 7): grades are per inscription ladder.
        /// Format 1 stored grades 1-7 over seven tiers and is migrated on read (<see cref="ItemMigrations"/>).
        /// </summary>
        public const int CurrentFormat = 2;

        public const char EntrySeparator = ';';
        public const char FieldSeparator = ':';

        public const string SealedSerpent = "serpent";

        /// <summary>The keys that make an item carry state (all but the version itself).</summary>
        public static readonly string[] StateKeys = { Rarity, Affixes, LegacyAffixes, Sealed, Tier, Sockets, Gems };

        /// <summary>
        /// Keys of the systems the runes replaced on 2026-10-02 (binding, honing and tempering, sigils, catalysts): never
        /// read, removed from an item the next time it is written. Sockets and gems came back on 2026-10-07 under their old
        /// keys (no released version ever wrote them).
        /// </summary>
        public static readonly string[] RetiredKeys = { "ecf_bound", "ecf_refine", "ecf_sigil", "ecf_catalyst" };
    }
}
