// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/ColumnLayout.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// Who is in the column and in what order. The members are the container's children named as column boxes,
    /// <c>PlateColumn_plate_&lt;rank:D4&gt;_&lt;id&gt;</c>, active or not; lower ranks sit higher, equal ranks by name. The rank
    /// lives in the name so every copy of this library, each merged into its own mod, reads the same order from the scene.
    /// A member is a mod's box or a seat (<see cref="Seats"/>): an empty stand-in whose id is <c>seat_&lt;name&gt;</c>, holding
    /// the place of the player panel's direct child of that name (the game's armour and weight boxes, another mod's box),
    /// which stays on the panel and is pinned over it. Seats are named as boxes so older copies sort them too. The hidden
    /// <c>PlateColumn_mark_first</c> / <c>_last</c> markers older copies left on the panel are not members.
    /// </summary>
    internal static class ColumnLayout
    {
        private const string PlatePrefix = "PlateColumn_plate_";
        private const string SeatMark = "_seat_";
        private const int RankDigits = 4;

        /// <summary>Before this library, OpenKeep made its trash plate under this name, between the game's two plates.</summary>
        private const string LegacyTrash = "OpenKeep_trash";
        private const int LegacyTrashRank = 200;

        public static string NameOf(PlateSpec spec) => $"{PlatePrefix}{spec.Rank:D4}_{spec.Id}";

        /// <summary>The seat holding the place of the panel's child named <paramref name="occupant"/>.</summary>
        public static string SeatName(int rank, string occupant) => $"{PlatePrefix}{Mathf.Clamp(rank, 0, 9999):D4}{SeatMark}{occupant}";

        /// <summary>A mod's box or a seat, or OpenKeep's pre-library trash plate.</summary>
        public static bool IsMember(Transform t) => RankOf(t.name) != null;

        /// <summary>The name of the panel's child a seat holds the place of; null for anything that is not a seat.</summary>
        public static string? OccupantOf(string name)
        {
            int mark = PlatePrefix.Length + RankDigits;
            if (name.Length <= mark + SeatMark.Length || !name.StartsWith(PlatePrefix, StringComparison.Ordinal))
            {
                return null;
            }
            return string.CompareOrdinal(name, mark, SeatMark, 0, SeatMark.Length) == 0 ? name.Substring(mark + SeatMark.Length) : null;
        }

        /// <summary>The members among <paramref name="parent"/>'s children, top to bottom.</summary>
        public static List<RectTransform> Sorted(Transform parent)
        {
            List<KeyValuePair<string, RectTransform>> found = new List<KeyValuePair<string, RectTransform>>();
            foreach (Transform child in parent)
            {
                if (child is RectTransform rect && KeyOf(child) is string key)
                {
                    found.Add(new KeyValuePair<string, RectTransform>(key, rect));
                }
            }
            found.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return found.ConvertAll(pair => pair.Value);
        }

        /// <summary>The member's sort key (rank, then name), null for anything that is not a member.</summary>
        private static string? KeyOf(Transform t)
        {
            string name = t.name;
            return RankOf(name) is int rank ? $"{rank:D4}{name}" : null;
        }

        private static int? RankOf(string name)
        {
            if (name == LegacyTrash)
            {
                return LegacyTrashRank;
            }
            if (!name.StartsWith(PlatePrefix, StringComparison.Ordinal) || name.Length < PlatePrefix.Length + RankDigits)
            {
                return null;
            }
            return int.TryParse(name.Substring(PlatePrefix.Length, RankDigits), out int rank) ? rank : (int?)null;
        }
    }
}
