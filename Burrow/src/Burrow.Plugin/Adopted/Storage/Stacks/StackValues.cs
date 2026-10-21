using System;
using System.Collections.Generic;
using ExtraPotions.Burrow.Embedded.ItemCopies;
using Burrow.Core;
using UnityEngine;

namespace Burrow.Stacks
{
    /// <summary>
    /// Writes the configured stack and weight of every item into the prefabs' shared data and into every live
    /// item: vanilla times the multipliers (the weight multiplier only for items with a vanilla stack above 1; a key raised to PackPanel's Key Stack, <see cref="PackPanelKeys"/>),
    /// then the per item .cfg entries, then the YAML rules (patterns in file order, plain names last). Runs when the item
    /// database loads, on every setting change and on every YAML apply; with the module off it restores vanilla, keys
    /// still at Key Stack. Live items are reached through ItemCopies (world drops, the
    /// local inventory, the open container, and new copies through <see cref="ApplyCopy"/>) and, for the other
    /// loaded containers, through <see cref="LiveItems"/>.
    /// </summary>
    public static class StackValues
    {
        private static readonly Dictionary<string, ItemValue> current = new Dictionary<string, ItemValue>(StringComparer.OrdinalIgnoreCase);
        private static bool suspended;

        /// <summary>Binding hundreds of entries raises SettingChanged for each; the apply is switched off meanwhile.</summary>
        public static void Suspend(bool suspend) => suspended = suspend;

        /// <summary>The current values of an item by prefab name or shared name token, after the last apply.</summary>
        public static bool TryGetCurrent(string name, out ItemValue value) => current.TryGetValue(name, out value);

        public static void ApplyAll()
        {
            ObjectDB db = ObjectDB.instance;
            if (suspended || db == null || db.m_items == null || db.m_items.Count == 0)
                return;
            List<ItemDrop> prefabs = Prefabs(db);
            if (StacksSettings.PerItemConfigEntries.Value)
                PerItemEntries.BindAll(prefabs);
            StacksModel model = Model();
            current.Clear();
            foreach (ItemDrop prefab in prefabs)
                ApplyPrefab(prefab, model);
            Copies.ApplyAll(WriteCurrent);
            if (Player.m_localPlayer != null)
                Player.m_localPlayer.GetInventory()?.UpdateTotalWeight();
            LiveItems.ForEachInContainers(ApplyCopy);
        }

        /// <summary>Re-applies one item (a per item entry changed): its prefab and the live items of that prefab.</summary>
        public static void ApplyItem(string prefabName)
        {
            ObjectDB db = ObjectDB.instance;
            GameObject prefab = !suspended && db != null ? db.GetItemPrefab(prefabName) : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                return;
            ApplyPrefab(drop, Model());
            Copies.Apply(prefabName, shared => WriteCurrent(prefabName, shared));
            if (Player.m_localPlayer != null)
                Player.m_localPlayer.GetInventory()?.UpdateTotalWeight();
            LiveItems.ForEachInContainers(item =>
            {
                if (string.Equals(ItemNames.PrefabName(item), prefabName, StringComparison.OrdinalIgnoreCase))
                    ApplyCopy(item);
            });
        }

        /// <summary>The ExtraPotions.Burrow.Embedded.ItemCopies.HookSpawns callback: a new world item or an item entering an inventory takes the current values.</summary>
        public static void ApplyCopy(ItemDrop.ItemData item)
        {
            if (item != null && item.m_shared != null)
                WriteCurrent(ItemNames.PrefabName(item), item.m_shared);
        }

        /// <summary>The ItemCopies write: the current values of the item by prefab name (or shared name token).</summary>
        private static void WriteCurrent(string name, ItemDrop.ItemData.SharedData shared)
        {
            if (!current.TryGetValue(name, out ItemValue value))
                return;
            shared.m_maxStackSize = value.Stack;
            shared.m_weight = value.Weight;
        }

        private static StacksModel Model() => StacksModule.Set != null ? StacksModule.Set.Current as StacksModel : null;

        /// <summary>The item prefabs of the database that carry item data.</summary>
        public static List<ItemDrop> Prefabs(ObjectDB db)
        {
            List<ItemDrop> prefabs = new List<ItemDrop>();
            foreach (GameObject prefab in db.m_items)
            {
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null)
                    prefabs.Add(drop);
            }
            return prefabs;
        }

        private static void ApplyPrefab(ItemDrop prefab, StacksModel model)
        {
            string name = prefab.name;
            ItemDrop.ItemData.SharedData shared = prefab.m_itemData.m_shared;
            ItemValue vanilla = VanillaValues.Remember(name, shared);
            ItemValue value = StacksSettings.Enabled.Value ? Compute(name, shared, vanilla, model)
                : new ItemValue(PackPanelKeys.Over(name, vanilla.Stack), vanilla.Weight);   // Key Stack holds with the module off too
            shared.m_maxStackSize = value.Stack;
            shared.m_weight = value.Weight;
            current[name] = value;
            if (!string.IsNullOrEmpty(shared.m_name))
                current[shared.m_name] = value;
        }

        private static ItemValue Compute(string name, ItemDrop.ItemData.SharedData shared, ItemValue vanilla, StacksModel model)
        {
            float stackMultiplier = model?.StackMultiplier ?? StacksSettings.StackMultiplier.Value;
            float weightMultiplier = model?.WeightMultiplier ?? StacksSettings.WeightMultiplier.Value;
            int stack = PackPanelKeys.Over(name, MultiplyStack(vanilla.Stack, stackMultiplier));
            // Weight follows the stack: only stackable items are lighter, so a full stack weighs as much as vanilla at the default 4 / 0.25.
            float weight = vanilla.Stack > 1 ? vanilla.Weight * weightMultiplier : vanilla.Weight;
            PerItemEntries.Override(name, vanilla, ref stack, ref weight);
            if (model != null)
            {
                ApplyRules(model, name, shared, false, ref stack, ref weight);
                ApplyRules(model, name, shared, true, ref stack, ref weight);
            }
            return new ItemValue(Math.Max(1, stack), Math.Max(0f, weight));
        }

        /// <summary>Rounded, at least 1; a vanilla stack of 1 (or less) is not multiplied.</summary>
        public static int MultiplyStack(int vanilla, float multiplier)
        {
            if (vanilla <= 1)
                return vanilla;
            return Math.Max(1, Mathf.RoundToInt(vanilla * multiplier));
        }

        private static void ApplyRules(StacksModel model, string name, ItemDrop.ItemData.SharedData shared, bool exact, ref int stack, ref float weight)
        {
            foreach (StackRule rule in model.Rules)
            {
                if (rule.IsExact != exact || !rule.Matches(name, shared))
                    continue;
                if (rule.Stack.HasValue)
                    stack = rule.Stack.Value;
                if (rule.Weight.HasValue)
                    weight = rule.Weight.Value;
            }
        }
    }
}
