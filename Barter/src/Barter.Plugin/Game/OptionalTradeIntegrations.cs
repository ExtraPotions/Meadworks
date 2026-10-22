// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Barter.Runtime
{
    internal static class OptionalTradeIntegrations
    {
        internal const string EpicLootGuid = "randyknapp.mods.epicloot";
        internal const string ExtraSlotsGuid = "shudnal.ExtraSlots";
        internal const string AzuSlotsGuid = "Azumatt.AzuExtendedPlayerInventory";
        private static readonly Dictionary<string, object> plugins = new Dictionary<string, object>();
        private static readonly HashSet<string> warned = new HashSet<string>();
        private const string BurrowHoldId = "meadworks.burrow.inventory_hold";
        private const string BurrowOwner = "com.meadworks.burrow";

        internal static bool InventoryHeld(Inventory inventory)
        {
            if (inventory == null || !CruciblePlugin.IsReady) return false;
            try
            {
                var capabilities = CruciblePlugin.Services.Capabilities;
                var providers = capabilities.Providers(BurrowHoldId);
                if (providers.Count == 0) return false;
                if (providers.Count != 1 || providers[0].Owner != BurrowOwner ||
                    !capabilities.TryResolve<Func<object, bool>>(BurrowHoldId, new System.Version(1, 0), out var held) || held == null)
                    throw new InvalidOperationException("Burrow inventory hold contract unavailable.");
                return held(inventory);
            }
            catch (Exception error) { Warn(BurrowOwner, error); return true; }
        }

        internal static void Initialize()
        {
            Shutdown();
            foreach (string guid in new[] { EpicLootGuid, ExtraSlotsGuid, AzuSlotsGuid })
                if (Chainloader.PluginInfos.TryGetValue(guid, out var plugin)) plugins[guid] = plugin.Instance;
        }

        internal static bool EpicLootAdventure
        {
            get
            {
                if (!plugins.TryGetValue(EpicLootGuid, out object plugin)) return false;
                try
                {
                    MethodInfo method = plugin.GetType().GetMethod("IsAdventureModeEnabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                        null, Type.EmptyTypes, null);
                    if (method == null || method.ReturnType != typeof(bool)) throw new MissingMethodException("IsAdventureModeEnabled");
                    return (bool)method.Invoke(method.IsStatic ? null : plugin, null);
                }
                catch (Exception error) { Warn(EpicLootGuid, error); return true; }
            }
        }

        internal static bool ProtectedItem(ItemDrop.ItemData item, Player player)
        {
            if (item == null) return true;
            foreach (string guid in new[] { ExtraSlotsGuid, AzuSlotsGuid })
            {
                if (!plugins.TryGetValue(guid, out object plugin)) continue;
                try
                {
                    if (guid == ExtraSlotsGuid)
                    {
                        Type api = plugin.GetType().Assembly.GetType("ExtraSlots.API") ?? throw new TypeLoadException("ExtraSlots.API");
                        MethodInfo method = api.GetMethod("GetAllExtraSlotsItems", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                        if (method == null || method.Invoke(null, null) is not IEnumerable items) throw new MissingMethodException("GetAllExtraSlotsItems");
                        foreach (object held in items) if (ReferenceEquals(item, held)) return true;
                    }
                    else
                    {
                        Type api = plugin.GetType().Assembly.GetType("AzuExtendedPlayerInventory.API") ?? throw new TypeLoadException("AzuExtendedPlayerInventory.API");
                        MethodInfo method = api.GetMethod("GetSlots", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                        object slots = method?.Invoke(null, null) ?? throw new MissingMethodException("GetSlots");
                        object functions = slots.GetType().GetField("GetItemFuncs")?.GetValue(slots) ?? slots.GetType().GetProperty("GetItemFuncs")?.GetValue(slots);
                        if (functions is not IEnumerable getters) throw new MissingMemberException("GetItemFuncs");
                        foreach (object getter in getters)
                        {
                            if (getter == null) continue;
                            if (getter is not Delegate callback) throw new InvalidOperationException("Unexpected slot callback type.");
                            if (ReferenceEquals(callback.DynamicInvoke(player), item)) return true;
                        }
                    }
                }
                catch (Exception error)
                {
                    // An installed slot provider with an unknown API cannot authorize taking any inventory item.
                    Warn(guid, error);
                    return true;
                }
            }
            return false;
        }

        private static void Warn(string guid, Exception error)
        {
            if (warned.Add(guid)) Plugin.Log?.Warning("Optional trader integration unavailable for " + guid + ": " + error.GetType().Name + ". Barter keeps protected items and foreign panels safe.");
        }

        internal static void Shutdown() { plugins.Clear(); warned.Clear(); }
    }
}
