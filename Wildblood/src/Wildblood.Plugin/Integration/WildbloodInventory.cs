// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime;

internal static class WildbloodInventory
{
    const int MaxCells = 16384;
    // An inventory owner (Mantle) is installed: its protected cells must come from its capability (presence, as the plugin check was).
    static readonly FeatureLink inventoryOwner = new(FeatureOwnerContract.Id(FeatureOwnerContract.Inventory));
    static readonly System.Version Version = new(1, 0);

    internal static bool TryProtected(Player player, Inventory inventory, out HashSet<Vector2i> cells)
    {
        cells = new HashSet<Vector2i>();
        if (!Current(player, inventory)) return false;
        int width = inventory.GetWidth(), height = inventory.GetHeight();
        if (width <= 0 || height <= 0 || (long)width * height > MaxCells) return false;
        long generation = CruciblePlugin.Session.Generation, profile = player.GetPlayerID();
        string session = CruciblePlugin.Session.SessionId;
        ZNet network = ZNet.instance;
        bool StillCurrent() => Current(player, inventory) && ReferenceEquals(network, ZNet.instance)
            && generation == CruciblePlugin.Session.Generation && session == CruciblePlugin.Session.SessionId
            && profile == player.GetPlayerID() && inventory.GetWidth() == width && inventory.GetHeight() == height;
        try
        {
            if (!CruciblePlugin.Services.Capabilities.TryResolve<Func<Tuple<bool, IReadOnlyList<Tuple<int, int>>>>>(
                "meadworks.inventory.protected_slots", Version, out var read) || read == null)
                return !inventoryOwner.TryOwned(CruciblePlugin.Services.Capabilities, out _) && StillCurrent();
            var snapshot = read();
            if (snapshot == null || !snapshot.Item1 || snapshot.Item2 == null) return false;
            int count = snapshot.Item2.Count;
            if (count < 0 || count > (long)width * height) return false;
            for (int i = 0; i < count; i++)
            {
                var cell = snapshot.Item2[i];
                if (cell == null || cell.Item1 < 0 || cell.Item1 >= width || cell.Item2 < 0 || cell.Item2 >= height)
                    return false;
                if (!cells.Add(new Vector2i(cell.Item1, cell.Item2))) return false;
            }
            return StillCurrent();
        }
        catch { return false; }
    }

    internal static bool Current(Player player, Inventory inventory) => player != null && player == Player.m_localPlayer
        && player.m_nview != null && player.m_nview.IsValid() && player.m_nview.IsOwner()
        && ReferenceEquals(player.GetInventory(), inventory) && Plugin.NativeRulesReady
        && CruciblePlugin.Session.AuthorityReady && CruciblePlugin.Session.WorldId != 0
        && ZNet.instance != null && ZNet.instance.GetWorldUID() == CruciblePlugin.Session.WorldId;
}
