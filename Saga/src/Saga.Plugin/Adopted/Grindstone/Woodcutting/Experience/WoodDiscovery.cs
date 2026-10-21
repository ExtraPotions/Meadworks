// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Experience/WoodDiscovery.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The kinds of tree a character has felled, for the discovery bonus: species (log prefab names, so the variants
    /// of one tree count once), comma-separated, in the player's own Player.m_customData under
    /// <see cref="Keys.FelledTrees"/>. Player.Save writes m_customData into the character file and Player.Load reads it
    /// back, so the list survives relogs and follows the character between worlds. Only the woodcutter's own client
    /// reads or writes it, when a fell credit arrives (<see cref="WoodXp.OnCredit"/>).
    /// </summary>
    public static class WoodDiscovery
    {
        private const char Separator = ',';

        /// <summary>Whether the character has felled this kind of tree before.</summary>
        public static bool HasFelled(Player player, string species)
        {
            if (player == null || string.IsNullOrEmpty(species) || !player.m_customData.TryGetValue(Keys.FelledTrees, out string felled))
                return false;
            return Array.IndexOf((felled ?? "").Split(Separator), species) >= 0;
        }

        /// <summary>Records the kind of tree as felled. True when this was the character's first one.</summary>
        public static bool TryRecord(Player player, string species)
        {
            if (player == null || string.IsNullOrEmpty(species) || species.IndexOf(Separator) >= 0 || HasFelled(player, species))
                return false;
            player.m_customData.TryGetValue(Keys.FelledTrees, out string felled);
            player.m_customData[Keys.FelledTrees] = string.IsNullOrEmpty(felled) ? species : felled + Separator + species;
            return true;
        }
    }
}
