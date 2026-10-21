// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Foraging/Experience/ForageDiscovery.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The kinds of forage a character has picked, for the discovery bonus: item prefab names, comma-separated, in the
    /// player's own Player.m_customData under <see cref="Keys.ForagedKinds"/>. Player.Save writes m_customData into the
    /// character file and Player.Load reads it back, so the list survives relogs and follows the character between
    /// worlds. Only the picker's own client reads or writes it, inside the pick (<see cref="ForageXp"/>).
    /// </summary>
    public static class ForageDiscovery
    {
        private const char Separator = ',';

        public static bool HasPicked(Player player, string item)
        {
            if (player == null || string.IsNullOrEmpty(item) || !player.m_customData.TryGetValue(Keys.ForagedKinds, out string picked))
                return false;
            return Array.IndexOf((picked ?? "").Split(Separator), item) >= 0;
        }

        /// <summary>Records the kind of forage as picked. True when this was the character's first one.</summary>
        public static bool TryRecord(Player player, string item)
        {
            if (player == null || string.IsNullOrEmpty(item) || item.IndexOf(Separator) >= 0 || HasPicked(player, item))
                return false;
            player.m_customData.TryGetValue(Keys.ForagedKinds, out string picked);
            player.m_customData[Keys.ForagedKinds] = string.IsNullOrEmpty(picked) ? item : picked + Separator + item;
            return true;
        }
    }
}
