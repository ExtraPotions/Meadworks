// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Experience/MineDiscovery.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The ore deposits a character has mined, for the discovery credit: deposit identities
    /// (<see cref="RockInfo.Identity"/>: the name token such as "$piece_deposit_copper", else the kind, so an intact
    /// deposit and its fractured form, and kinds that read the same, count once), comma-separated, in the player's own
    /// Player.m_customData under <see cref="Keys.MinedDeposits"/>. Player.Save writes m_customData into the character
    /// file and Player.Load reads it back, so the list survives relogs and follows the character between worlds. Only
    /// the miner's own client reads or writes it, on a local pickaxe hit (<see cref="MineXp.OnRockHit"/>).
    /// </summary>
    public static class MineDiscovery
    {
        private const char Separator = ',';

        /// <summary>Whether the character has mined this deposit (<see cref="RockInfo.Identity"/>) before.</summary>
        public static bool HasMined(Player player, string identity)
        {
            if (player == null || string.IsNullOrEmpty(identity) || !player.m_customData.TryGetValue(Keys.MinedDeposits, out string mined))
                return false;
            return Array.IndexOf((mined ?? "").Split(Separator), identity) >= 0;
        }

        /// <summary>Records the deposit as mined. True when this was the character's first one.</summary>
        public static bool TryRecord(Player player, string identity)
        {
            if (player == null || string.IsNullOrEmpty(identity) || identity.IndexOf(Separator) >= 0 || HasMined(player, identity))
                return false;
            player.m_customData.TryGetValue(Keys.MinedDeposits, out string mined);
            player.m_customData[Keys.MinedDeposits] = string.IsNullOrEmpty(mined) ? identity : mined + Separator + identity;
            return true;
        }
    }
}
