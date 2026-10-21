// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// Keeps rarity and enhancement data through a workbench upgrade. The game removes
    /// the old item and adds a brand-new one from the prefab, which copies no custom data. Capture both owned key families
    /// just before (DoCrafting prefix), write them onto the item the game adds for that recipe at the
    /// old grid position (10-argument AddItem postfix), and clear whatever is left when DoCrafting ends (finalizer).
    /// Upgrader stations: success and "level reduced" both re-add the item at the old position and get the pairs;
    /// "broke" adds only returned ingredients at no position, which never match. Other mods' keys are not copied.
    /// Runs on the crafting player's client only (DoCrafting is the local player's crafting panel).
    /// </summary>
    [HarmonyPatch]
    internal static class UpgradeCarryOver
    {
        private static Pending? _pending;

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        [HarmonyPrefix]
        private static void Capture(InventoryGui __instance)
        {
            _pending = null;
            ItemDrop.ItemData? item = __instance.m_craftUpgradeItem;
            Recipe? recipe = __instance.m_craftRecipe;
            if (item?.m_customData == null || recipe?.m_item == null)
            {
                return;
            }
            Dictionary<string, string>? ours = ExtraPotions.Inscribe.Items.UpgradeData.Capture(item.m_customData);
            if (ours != null)
            {
                _pending = new Pending(recipe.m_item.gameObject.name, item.m_gridPos, ours);
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        [HarmonyFinalizer]
        private static void Clear() => _pending = null;

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[]
        {
            typeof(string), typeof(int), typeof(int), typeof(int), typeof(long), typeof(string), typeof(Vector2i),
            typeof(bool), typeof(bool), typeof(bool),
        })]
        [HarmonyPostfix]
        private static void Restore(string name, Vector2i position, ItemDrop.ItemData? __result)
        {
            Pending? pending = _pending;
            if (pending == null || __result == null || !pending.Matches(name, position))
            {
                return;
            }
            _pending = null;
            // A new dictionary instance, so the parse cache (keyed on the dictionary reference) re-reads the item.
            Dictionary<string, string> data = __result.m_customData != null
                ? new Dictionary<string, string>(__result.m_customData)
                : new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> pair in pending.Pairs)
            {
                data[pair.Key] = pair.Value;
            }
            __result.m_customData = data;
            // The game filled durability before the pairs were back, so a durability affix's share was missing.
            __result.m_durability = __result.GetMaxDurability();
            Log.Debug($"upgrade kept {pending.Pairs.Count} item-state keys on {name}");
        }

        private sealed class Pending
        {
            public Pending(string prefabName, Vector2i position, Dictionary<string, string> pairs)
            {
                PrefabName = prefabName;
                Position = position;
                Pairs = pairs;
            }

            public string PrefabName { get; }
            public Vector2i Position { get; }
            public Dictionary<string, string> Pairs { get; }

            public bool Matches(string name, Vector2i position) =>
                name == PrefabName && position.x == Position.x && position.y == Position.y;
        }
    }
}
