// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Shepherd.Troughs
{
    /// <summary>
    /// Trough prefab names (design section 11.1) and the container role Shepherd reports for them to Burrow
    /// (design "Burrow contracts": <c>meadworks.containers.role</c> answers <see cref="FeedRole"/> for a trough, null
    /// otherwise). <c>grindstone_feeder</c> is the GrindstoneSkills Animal Feeder, aliased to the Feed Barrel.
    /// </summary>
    public static class TroughPrefabs
    {
        public const string FeedBarrel = "shepherd_feed_barrel";
        public const string PlankTrough = "shepherd_trough_plank";
        public const string StoneTrough = "shepherd_trough_stone";
        public const string GrindstoneFeeder = "grindstone_feeder";

        /// <summary>The role Burrow keeps out of its material sources, Top Up and routing.</summary>
        public const string FeedRole = "feed";

        private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal)
        {
            FeedBarrel, PlankTrough, StoneTrough, GrindstoneFeeder,
        };

        public static IReadOnlyCollection<string> All => Names;

        public static bool IsTrough(string? prefabName) => prefabName != null && Names.Contains(prefabName);

        /// <summary>The container role of a prefab: <see cref="FeedRole"/> for a trough, otherwise null.</summary>
        public static string? RoleOf(string? prefabName) => IsTrough(prefabName) ? FeedRole : null;
    }
}
