// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
// Changed by expDARE, 2026: Kall Fimbulbringer's final phase points to the Bog Witch, whose trades wait on defeated_frozenking_p3.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Hints
{
    /// <summary>
    /// Which bosses leave a hint and where it points, from the boss's own altar: Bonemass to the Bog Witch, Moder to the
    /// ancient forge (the Forge of Potential), Yagluth to Haldor, the Elder to Hildir and Kall Fimbulbringer to the Bog Witch. Both ends are the game's own
    /// location names, which the server's world knows the place of. Fixed by design: no other boss hints.
    /// </summary>
    internal static class BossHintTable
    {
        public readonly struct Entry
        {
            public readonly string Altar;
            public readonly string Target;

            public Entry(string altar, string target)
            {
                Altar = altar;
                Target = target;
            }
        }

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>
        {
            ["Bonemass"] = new Entry("Bonemass", "BogWitch_Camp"),
            ["Dragon"] = new Entry("Dragonqueen", "AncientUpgradeStation"),
            ["GoblinKing"] = new Entry("GoblinKing", "Vendor_BlackForest"),
            ["gd_king"] = new Entry("GDKing", "Hildir_camp"),
            // Only the final phase: the earlier two fall mid-fight. Its arena's location name is not verified, so no altar
            // name is given and the hint is taken from where it fell, as for a boss spawned by command.
            ["FrozenKing_p3"] = new Entry("", "BogWitch_Camp"),
        };

        private static readonly string[] Words =
            { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };

        public static bool TryGet(string boss, out Entry entry) => Entries.TryGetValue(boss, out entry);

        /// <summary>The eighth of the compass from <paramref name="from"/> toward <paramref name="to"/>, clockwise from
        /// north: 0 is north (+z, up on the map), 2 is east (+x).</summary>
        public static int Sector(Vector3 from, Vector3 to)
        {
            float degrees = Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
            return Wrap(Mathf.RoundToInt(degrees / 45f));
        }

        public static string Word(int sector) => Words[Wrap(sector)];

        private static int Wrap(int sector) => ((sector % 8) + 8) % 8;
    }
}
