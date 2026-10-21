// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/PlayerIds.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The player ID behind a character's ZDOID, read from the player's ZDO (ZDOVars.s_playerID), on whichever machine
    /// has that ZDO: a hit's target owner reads its attacker this way. Players' ZDOs reach every machine near them, so
    /// the owner of something a player is hitting has it. 0 when the ZDO is unknown or not a player's. And the other
    /// way round, the machine a player ID plays on (<see cref="PeerOf"/>), to address a routed RPC to that player alone.
    /// </summary>
    public static class PlayerIds
    {
        public static long Of(ZDOID character)
        {
            if (character.IsNone() || ZDOMan.instance == null)
                return 0L;
            ZDO zdo = ZDOMan.instance.GetZDO(character);
            return zdo == null ? 0L : zdo.GetLong(ZDOVars.s_playerID);
        }

        /// <summary>
        /// The peer ID to send a routed RPC to so that only the machine whose local player has <paramref name="playerId"/>
        /// gets it. For this machine's own player, this machine's ID: ZRoutedRpc then handles the call at once and sends
        /// nothing. Else the owner of that player's character ZDO (a player always owns their own), found through the
        /// player list the server keeps and sends to every client. ZRoutedRpc.Everybody when this machine does not have
        /// that ZDO (a client far from the player): the call then reaches every machine, as it always did.
        /// </summary>
        public static long PeerOf(long playerId)
        {
            Player local = Player.m_localPlayer;
            if (local != null && local.GetPlayerID() == playerId)
                return ZNet.GetUID();
            if (ZNet.instance == null || ZDOMan.instance == null)
                return ZRoutedRpc.Everybody;
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                long owner = OwnerOf(info.m_characterID, playerId);
                if (owner != 0L)
                    return owner;
            }
            return ZRoutedRpc.Everybody;
        }

        /// <summary>The owner of the character's ZDO when it is the player with this ID; 0 otherwise or when it is not here.</summary>
        private static long OwnerOf(ZDOID character, long playerId)
        {
            ZDO zdo = character.IsNone() ? null : ZDOMan.instance.GetZDO(character);
            return zdo != null && zdo.GetLong(ZDOVars.s_playerID) == playerId ? zdo.GetOwner() : 0L;
        }
    }
}
