// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Threading;
using Mantle.Core;
using Mantle.Layout;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace Mantle.Protection
{
    public static class ProtectedSlots
    {
        private static int ownerThread;
        internal static void Initialize() => ownerThread = Thread.CurrentThread.ManagedThreadId;
        internal static void Reset() => ownerThread = 0;

        public static Tuple<bool, IReadOnlyList<Tuple<int, int>>> Read()
        {
            if (ownerThread == 0 || ownerThread != Thread.CurrentThread.ManagedThreadId) return ProtectedCellSnapshot.Unavailable;
            try
            {
                if (!Plugin.WorldReady || ZNet.m_world == null || ZNet.instance.GetWorldUID() != CruciblePlugin.Session.WorldId ||
                    InventoryModule.LayoutPending || LayoutApply.IsApplying) return ProtectedCellSnapshot.Unavailable;
                var network = ZNet.instance;
                long generation = CruciblePlugin.Session.Generation, world = CruciblePlugin.Session.WorldId;
                string session = CruciblePlugin.Session.SessionId;
                var player = Player.m_localPlayer;
                if (player == null || !ReferenceEquals(InventoryState.Player, player) || player.m_nview == null ||
                    !player.m_nview.IsValid() || !player.m_nview.IsOwner() || player.IsDead() || player.IsTeleporting())
                    return ProtectedCellSnapshot.Unavailable;
                var inventory = player.GetInventory();
                if (inventory == null) return ProtectedCellSnapshot.Unavailable;
                bool enabled = InventorySettings.Enabled.Value;
                bool hasRecord = player.m_customData.TryGetValue(LayoutRecord.Key, out string raw);
                if (!enabled && player.m_customData.ContainsKey(GridContract.Key)) return ProtectedCellSnapshot.Unavailable;
                var wanted = LayoutBuilder.Wanted(player);
                var layout = InventoryState.Layout ?? (!enabled && !hasRecord ? LayoutBuilder.GameLayout(player) : null);
                int width = inventory.GetWidth(), height = inventory.GetHeight();
                var snapshot = ProtectedCellSnapshot.Create(layout, wanted, width, height,
                    InventoryModule.LayoutPending, LayoutApply.IsApplying, enabled, hasRecord, InventoryLayout.Parse(raw));
                if (!snapshot.Item1) return snapshot;
                var items = inventory.GetAllItems();
                if (items == null || items.Count > width * height) return ProtectedCellSnapshot.Unavailable;
                foreach (var item in items)
                    if (item == null || item.m_gridPos.x < 0 || item.m_gridPos.x >= width || item.m_gridPos.y < 0 || item.m_gridPos.y >= height)
                        return ProtectedCellSnapshot.Unavailable;
                if (!Plugin.WorldReady || ZNet.m_world == null || !ReferenceEquals(network, ZNet.instance) ||
                    network.GetWorldUID() != world || generation != CruciblePlugin.Session.Generation ||
                    world != CruciblePlugin.Session.WorldId || session != CruciblePlugin.Session.SessionId ||
                    !ReferenceEquals(player, Player.m_localPlayer) || !ReferenceEquals(player, InventoryState.Player) ||
                    !ReferenceEquals(inventory, player.GetInventory()) || width != inventory.GetWidth() || height != inventory.GetHeight() ||
                    InventoryModule.LayoutPending || LayoutApply.IsApplying || InventorySettings.Enabled.Value != enabled)
                    return ProtectedCellSnapshot.Unavailable;
                return snapshot;
            }
            catch { return ProtectedCellSnapshot.Unavailable; }
        }
    }
}
