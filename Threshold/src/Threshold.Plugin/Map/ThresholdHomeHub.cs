// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the local character's home hub per world, kept in
// Player.m_customData under HomeHub.DataKey(ZNet.GetWorldUID()), so it travels with the character and differs per world.
using ExtraPotions.Threshold.Portals;
using ExtraPotions.Threshold.Routing;

namespace ExtraPotions.Threshold.Runtime.Map
{
    public static class ThresholdHomeHub
    {
        /// <summary>The stored home hub key in this world, or null (none chosen, no player or no world).</summary>
        public static string? Load()
        {
            var player = Player.m_localPlayer;
            var net = ZNet.instance;
            if (player == null || net == null) return null;
            return player.m_customData.TryGetValue(HomeHub.DataKey(net.GetWorldUID()), out var text) ? HomeHub.Parse(text) : null;
        }

        /// <summary>Chooses <paramref name="hub"/> as home in this world; false when it is not a hub or nothing is loaded.</summary>
        public static bool Save(PortalEntry hub)
        {
            var player = Player.m_localPlayer;
            var net = ZNet.instance;
            if (player == null || net == null || hub == null || !HomeHub.CanSet(hub)) return false;
            player.m_customData[HomeHub.DataKey(net.GetWorldUID())] = HomeHub.Format(hub);
            return true;
        }

        /// <summary>Forgets the home hub in this world (no Return To Hub button until one is set again).</summary>
        public static void Clear()
        {
            var player = Player.m_localPlayer;
            var net = ZNet.instance;
            if (player == null || net == null) return;
            player.m_customData.Remove(HomeHub.DataKey(net.GetWorldUID()));
        }
    }
}
