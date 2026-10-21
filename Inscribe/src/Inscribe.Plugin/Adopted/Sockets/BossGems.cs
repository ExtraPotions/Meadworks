// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Sockets
{
    /// <summary>
    /// What each boss adds of the socket stones (sockets.md section 7), on top of its YAML drops: the Dvergr Chisel one
    /// time in four, and gems, any of the eleven at random (user 2026-10-07: "all bosses should be able to drop all gems"),
    /// each a 50% roll: one for the boss and one more for each of its stars (user 2026-10-08: "each boss should have a 50%
    /// chance to drop a single gem. Also you get 50% chance per star. So a 4 star monster could drop 1 + the 4 stars").
    /// Fixed in code so every server has them whatever its own economy file says. Rolled on the creature's owner with the
    /// rest of the boss's drops, while <c>Rune drops</c> and <c>Gems and sockets</c> are on.
    /// </summary>
    internal static class BossGems
    {
        private const float ChiselChance = 25f;
        private const float GemChance = 50f;

        // The game's bosses; a boss a server adds to its own boss map drops no gems.
        private static readonly HashSet<string> Bosses = new HashSet<string>(StringComparer.Ordinal)
        {
            "Eikthyr", "gd_king", "Bonemass", "Dragon", "GoblinKing", "SeekerQueen", "Fader", "FrozenKing_p3",
        };

        private static readonly List<StoneDef> Gems = new List<StoneDef>();

        /// <summary>The boss's gems (one roll, plus one per star) and chisel, each rolled on its own; a disabled stone never drops.</summary>
        public static void Add(EconomyRules economy, string? bossPrefab, int stars, Random random, List<StoneDef> into)
        {
            if (!SocketSwitch.On || bossPrefab == null || !Bosses.Contains(bossPrefab))
            {
                return;
            }
            for (int i = 0; i <= Math.Max(0, stars); i++)
            {
                if (Rolls(random, GemChance))
                {
                    AnyGem(economy, random, into);
                }
            }
            StoneDef? chisel = economy.Stone(StoneCatalog.ChiselId);
            if (chisel != null && chisel.Enabled && Rolls(random, ChiselChance))
            {
                into.Add(chisel);
            }
        }

        private static void AnyGem(EconomyRules economy, Random random, List<StoneDef> into)
        {
            Gems.Clear();
            foreach (string id in StoneCatalog.GemIds)
            {
                StoneDef? gem = economy.Stone(id);
                if (gem != null && gem.Enabled)
                {
                    Gems.Add(gem);
                }
            }
            if (Gems.Count > 0)
            {
                into.Add(Gems[random.Next(Gems.Count)]);
            }
        }

        private static bool Rolls(Random random, float percent) => random.NextDouble() * 100.0 < percent;
    }
}
