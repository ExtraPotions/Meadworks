// Optional favourites and inventory-layout boundaries around adopted OpenKeep salvage gameplay: MeadWorks products are
// reached only through Crucible capabilities (meadworks.items.favorites, meadworks.owns.inventory,
// meadworks.inventory.layout); the third-party PackPanel link reads its config and main-grid data as before.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Kindle.Runtime.Salvage
{
    internal static class SalvageInterop
    {
        static readonly CapabilityLink<Func<IReadOnlyCollection<string>>> favourites = new CapabilityLink<Func<IReadOnlyCollection<string>>>(FavouritesContract.Id, FavouritesContract.ContractVersion);
        static readonly FeatureLink inventoryOwner = new FeatureLink(FeatureOwnerContract.Id(FeatureOwnerContract.Inventory));
        static readonly CapabilityLink<Func<Tuple<bool,int,int,int>>> layout = new CapabilityLink<Func<Tuple<bool,int,int,int>>>(InventoryLayoutContract.Id, InventoryLayoutContract.ContractVersion);
        static CapabilityRegistry? Registry => CruciblePlugin.IsReady ? CruciblePlugin.Services.Capabilities : null;
        static bool Loaded(string id) => Chainloader.PluginInfos.TryGetValue(id, out PluginInfo info) && info.Instance != null && info.Instance.isActiveAndEnabled;
        static bool PackPanelGrid()
        {
            if (!Loaded("milkyteam.packpanel")) return false;
            return !Chainloader.PluginInfos["milkyteam.packpanel"].Instance.Config.TryGetEntry("1. Inventory", "Enabled", out ConfigEntry<bool> enabled) || enabled.Value;
        }
        internal static bool Favourite(ItemDrop.ItemData item)
        {
            if (item == null || Player.m_localPlayer == null) return false;
            try
            {
                // No provider: nothing is a favourite. A provider that fails cannot authorize destruction.
                if (!favourites.TryGet(Registry, out var read)) return false;
                if (!FavouritesContract.TryRead(read, out var names)) return true;
                return FavouritesContract.Contains(names, ItemNames.PrefabName(item), item.m_shared.m_name);
            }
            catch { return true; }
        }
        /// <summary>A value that changes when the favorite names change: 0 without a provider, 1 when it cannot be read.</summary>
        internal static int FavouritesStamp()
        {
            try
            {
                if (!favourites.TryGet(Registry, out var read)) return 0;
                if (!FavouritesContract.TryRead(read, out var names)) return 1;
                int stamp = 17 + names.Count;
                foreach (string name in names) stamp = stamp * 31 + StringComparer.Ordinal.GetHashCode(name ?? "");
                return stamp == 0 || stamp == 1 ? 2 : stamp;
            }
            catch { return 1; }
        }
        internal static bool ListedCell(Player player, ItemDrop.ItemData item) => MainCell(player,item.m_gridPos,true);
        internal static bool FreedCell(Player player, ItemDrop.ItemData item) => MainCell(player,item.m_gridPos,false);
        /// <summary>Empty cells an ordinary item can take in the player's inventory: the main grid when an inventory owner or
        /// PackPanel lays it out (their slot cells refuse ordinary items), every cell otherwise; none while that grid cannot be read.</summary>
        internal static int FreeCells(Player player, Inventory inventory)
        {
            int free = 0;
            for (int y = 0; y < inventory.GetHeight(); y++)
                for (int x = 0; x < inventory.GetWidth(); x++)
                    if (inventory.GetItemAt(x, y) == null && MainCell(player, new Vector2i(x, y), false)) free++;
            return free;
        }
        static bool MainCell(Player player, Vector2i cell,bool listing)
        {
            if (cell.x < 0 || cell.y < 0 || (listing && cell.y == 0 && cell.x < 8)) return false;
            var registry = Registry;
            bool mantle = inventoryOwner.On(registry), legacy = PackPanelGrid();
            if (mantle == legacy) return listing ? cell.x < 8 && cell.y < 4
                : !mantle && cell.x < player.GetInventory().GetWidth() && cell.y < player.GetInventory().GetHeight();
            int width, rows, blocked;
            if (mantle)
            {
                // The owner lays out the inventory but its grid cannot be read yet: protect as an unknown grid.
                if (!layout.TryGet(registry, out var read) || !InventoryLayoutContract.TryRead(read, out var grid) || !grid.LaysOut)
                    return listing && cell.x < 8 && cell.y < 4;
                width = grid.Width; rows = grid.Rows; blocked = grid.Blocked;
            }
            else
            {
                if (!player.m_customData.TryGetValue("PackPanel.mainGrid", out string text) || text == null || text.Length > 40) return listing && cell.x < 8 && cell.y < 4;
                string[] parts = text.Split('|');
                if (parts.Length != 3 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out width)
                    || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out rows)
                    || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out blocked)) return listing && cell.x < 8 && cell.y < 4;
            }
            if (width < 1 || width > 12 || rows < 0 || rows > 128 || blocked < 0 || blocked >= width || (rows == 0 && blocked != 0)
                || player.GetInventory().GetWidth() != width || rows > player.GetInventory().GetHeight()) return listing && cell.x < 8 && cell.y < 4;
            return cell.x < width && cell.y < rows && !(cell.y == rows - 1 && cell.x >= width - blocked);
        }
    }
}
