// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/PackPanelOwner.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: the column's owner is any registered Meadworks product
// that lays out the inventory (Mantle), as well as PackPanel itself when it runs; PackPanelOwner stays as the name the
// adopted library files ask.
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// The inventory's column of brown boxes is PackPanel's look: the column is built only while a mod lays out the
    /// inventory. In Meadworks that is the product that owns the inventory (Mantle), which registers here; PackPanel
    /// itself, found by its plugin GUID with its inventory section on (<c>1. Inventory / Enabled</c>), still counts so
    /// the library behaves as upstream when PackPanel is the inventory mod. Without an owner <see cref="Column"/> changes
    /// nothing and the game's armour and weight plates stay exactly as the game draws them.
    /// </summary>
    public static class InventoryOwner
    {
        private static readonly List<Func<bool>> owners = new List<Func<bool>>();

        /// <summary>A product that lays out the inventory while <paramref name="laysOut"/> says so.</summary>
        public static void Register(Func<bool> laysOut)
        {
            if (laysOut != null && !owners.Contains(laysOut))
            {
                owners.Add(laysOut);
            }
        }

        public static void Unregister(Func<bool> laysOut) => owners.Remove(laysOut);

        /// <summary>A registered product lays out the inventory now, or PackPanel does. A failing check counts as no.</summary>
        public static bool LaysOutInventory
        {
            get
            {
                foreach (Func<bool> owner in owners)
                {
                    try
                    {
                        if (owner())
                        {
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                return PackPanelOwner.PackPanelLaysOut;
            }
        }
    }

    internal static class PackPanelOwner
    {
        private const string Guid = "milkyteam.packpanel";

        private static bool looked;
        private static ConfigEntry<bool>? enabled;

        /// <summary>What the adopted column files ask: any owner (<see cref="InventoryOwner"/>).</summary>
        public static bool LaysOutInventory => InventoryOwner.LaysOutInventory;

        public static bool PackPanelLaysOut
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    enabled = Find();
                }
                return enabled != null && enabled.Value;
            }
        }

        private static ConfigEntry<bool>? Find()
        {
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo info) || info.Instance == null)
            {
                return null;
            }
            return info.Instance.Config.TryGetEntry("1. Inventory", "Enabled", out ConfigEntry<bool> entry) ? entry : null;
        }
    }
}
