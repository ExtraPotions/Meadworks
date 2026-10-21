using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Burrow.Embedded.PatchGuard;

namespace Burrow.Stacks
{
    /// <summary>
    /// The optional <prefab>.Stack and <prefab>.Weight entries of sections 4a and 4b, bound from the item database
    /// when "Per Item Config Entries" is on. Defaults are the vanilla values; 0 or the vanilla value means not set,
    /// so the multipliers stay in charge until a value is actually changed. The YAML wins where it names the item.
    /// </summary>
    public static class PerItemEntries
    {
        private static readonly Dictionary<string, ConfigEntry<int>> stacks = new Dictionary<string, ConfigEntry<int>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ConfigEntry<float>> weights = new Dictionary<string, ConfigEntry<float>>(StringComparer.OrdinalIgnoreCase);

        public static int Count => stacks.Count;

        /// <summary>
        /// Binds the entries of every prefab that has none yet. BepInEx writes the whole .cfg on every Bind, so the
        /// file is written once at the end instead, and binding an entry the .cfg already holds raises
        /// SettingChanged, so the apply is suspended meanwhile.
        /// </summary>
        public static void BindAll(List<ItemDrop> prefabs)
        {
            ConfigFile config = StacksModule.Synced.Config;
            int before = stacks.Count;
            bool saveOnSet = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            StackValues.Suspend(true);
            try
            {
                foreach (ItemDrop prefab in prefabs)
                    Bind(prefab);
            }
            finally
            {
                config.SaveOnConfigSet = saveOnSet;
                StackValues.Suspend(false);
            }
            if (stacks.Count == before)
                return;
            if (saveOnSet)
                config.Save();
            Plugin.Log.LogInfo($"Burrow: bound stack and weight entries for {stacks.Count} items.");
        }

        private static void Bind(ItemDrop prefab)
        {
            string name = prefab.name;
            if (stacks.ContainsKey(name))
                return;
            ItemValue vanilla = VanillaValues.Remember(name, prefab.m_itemData.m_shared);
            try
            {
                ConfigEntry<int> stack = StacksModule.Synced.Bind(StacksSettings.StackSection, name + ".Stack", vanilla.Stack,
                    $"Maximum stack of {name}. Vanilla {vanilla.Stack}; 0 or the vanilla value leaves the multiplier in charge. Burrow.Stacks.yml wins when it names this item.");
                ConfigEntry<float> weight = StacksModule.Synced.Bind(StacksSettings.WeightSection, name + ".Weight", vanilla.Weight,
                    $"Weight of one {name}. Vanilla {vanilla.Weight}; 0 or the vanilla value leaves the multiplier in charge. Burrow.Stacks.yml wins when it names this item.");
                stack.SettingChanged += Guard.Wrap($"{name} stack", (_, _) => StackValues.ApplyItem(name));
                weight.SettingChanged += Guard.Wrap($"{name} weight", (_, _) => StackValues.ApplyItem(name));
                stacks[name] = stack;
                weights[name] = weight;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Burrow: no config entries for {name}: {e.Message}");
            }
        }

        /// <summary>Replaces the multiplied values with the entries' values where they are set and differ from vanilla.</summary>
        public static void Override(string name, ItemValue vanilla, ref int stack, ref float weight)
        {
            if (!StacksSettings.PerItemConfigEntries.Value)
                return;
            if (stacks.TryGetValue(name, out ConfigEntry<int> stackEntry) && stackEntry.Value > 0 && stackEntry.Value != vanilla.Stack)
                stack = stackEntry.Value;
            if (weights.TryGetValue(name, out ConfigEntry<float> weightEntry) && weightEntry.Value > 0f && weightEntry.Value != vanilla.Weight)
                weight = weightEntry.Value;
        }
    }
}
