// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/FeastMasterData.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Larder.Foods;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>Section 0 and one section per food and per mead, bound from the item database.</summary>
    public static class FeastMasterData
    {
        public const string GlobalSection = "2 - Foods";
        private const string OldLockSection = "General";
        private const string OldMeadPrefix = "0Meads_";

        public const string Health = "Health";
        public const string Stamina = "Stamina";
        public const string Duration = "Duration";
        public const string HealthRegen = "HealthRegen";
        public const string Eitr = "Eitr";
        public const string Vigor = "Vigor";
        public const string EitrVigor = "EitrVigor";

        /// <summary>Per food prefab name: Health / Stamina / Duration / HealthRegen / Eitr / Vigor / EitrVigor entries.</summary>
        public static Dictionary<string, Dictionary<string, ConfigEntry<float>>> FoodConfigs { get; }
            = new Dictionary<string, Dictionary<string, ConfigEntry<float>>>();

        /// <summary>Per mead prefab name.</summary>
        public static Dictionary<string, MeadEffectConfig> MeadConfigs { get; }
            = new Dictionary<string, MeadEffectConfig>();

        /// <summary>Mead configs keyed by the name hash of the mead's consume status effect, for the SEMan patch.</summary>
        public static Dictionary<int, MeadEffectConfig> MeadsByEffectHash { get; }
            = new Dictionary<int, MeadEffectConfig>();

        // Localization token ($item_...) to prefab name, for item data without a prefab reference.
        private static readonly Dictionary<string, string> foodsByToken = new Dictionary<string, string>();

        public static ConfigEntry<float> HealthModifier { get; private set; }
        public static ConfigEntry<float> StaminaModifier { get; private set; }
        public static ConfigEntry<float> DurationModifier { get; private set; }
        public static ConfigEntry<float> HealthRegenModifier { get; private set; }
        public static ConfigEntry<float> EitrModifier { get; private set; }
        public static ConfigEntry<bool> DisableFoodDegradation { get; private set; }
        public static ConfigEntry<float> DegradationCurve { get; private set; }
        public static ConfigEntry<float> EatAgainAt { get; private set; }
        public static ConfigEntry<bool> LockConfiguration { get; private set; }

        private static NativeBindings synced;

        internal static void Reset()
        {
            FoodConfigs.Clear(); MeadConfigs.Clear(); MeadsByEffectHash.Clear(); foodsByToken.Clear();
            FoodLookup.Forget();
        }

        public static void Initialize(NativeBindings config)
        {
            synced = config;
            HealthModifier = config.Bind(GlobalSection, "Health Modifier", 1f, "Multiplier for health value of all foods.");
            StaminaModifier = config.Bind(GlobalSection, "Stamina Modifier", 1f, "Multiplier for stamina value of all foods.");
            DurationModifier = config.Bind(GlobalSection, "Duration Modifier", 1f, "Multiplier for how long all foods last. Food Rate in 5 - Exertion also changes this; both apply.");
            HealthRegenModifier = config.Bind(GlobalSection, "Health Regen Modifier", 1f, "Multiplier for health regeneration of all foods.");
            EitrModifier = config.Bind(GlobalSection, "Eitr Modifier", 1f, "Multiplier for eitr value of all foods.");
            DisableFoodDegradation = config.Bind("3 - Eating", "Disable Food Degradation", false,
                "When enabled, food stats do not decrease over time.");
            DegradationCurve = config.Bind("3 - Eating", "Degradation Curve", 0.3f,
                "Exponent of the food strength curve: strength = remaining fraction ^ curve. 0.3 is the game's curve, 1 is linear, higher values make food fade sooner, 0 means no fading. Disable Food Degradation overrides it.");
            EatAgainAt = config.Bind("3 - Eating", "Eat Again At", 0.5f,
                "Fraction of a food's duration below which it can be eaten again. The game uses 0.5; 1 allows it at any time, 0 only once the food has run out.", acceptableValues: new AcceptableValueRange<float>(0f, 1f));
        }

        /// <summary>Lock Configuration lived in section General up to 3.3.5; its value is carried over.</summary>


        /// <summary>
        /// Binds one section per food and per mead found in the item database and writes the configured values
        /// into the items. Safe to call repeatedly: items that already have entries are skipped, so the start scene
        /// and the main scene database can both feed it; the item references are refreshed every time.
        /// </summary>
        public static void LoadConfigurations()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || db.m_items.Count == 0)
                return;

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            if (BindAll(db))
                NativeFeastMaster.Log.Verbose($"Loaded {FoodConfigs.Count} food and {MeadConfigs.Count} mead configurations in {stopwatch.ElapsedMilliseconds} ms.");
            ItemValues.ApplyAll();
            ChangedRules.Refresh();
            PatchSwitch.MarkDirty();
        }

        /// <summary>
        /// Binds every consumable of the database. BepInEx rewrites the whole .cfg on every Bind by default; with
        /// hundreds of entries that is hundreds of full file writes during scene load, so the file is written once
        /// at the end instead, and only when new entries appeared. Returns whether new entries appeared.
        /// </summary>
        private static bool BindAll(ObjectDB db)
        {
            int entriesBefore = FoodConfigs.Count + MeadConfigs.Count;
            bool restedBound = false;
            ConfigFile configFile = synced.Config;
            bool saveOnSet = configFile.SaveOnConfigSet;
            configFile.SaveOnConfigSet = false;
            ItemValues.SuspendWhileBinding(true);
            try
            {
                foreach (GameObject prefab in db.m_items)
                    BindConsumable(prefab);
                restedBound = Rested.Bind(db);
                restedBound = RestDelay.Bind(db) || restedBound;
            }
            finally
            {
                configFile.SaveOnConfigSet = saveOnSet;
                ItemValues.SuspendWhileBinding(false);
            }
            bool changed = restedBound || FoodConfigs.Count + MeadConfigs.Count != entriesBefore;
            if (changed && saveOnSet)
                configFile.Save();
            return changed;
        }

        private static void BindConsumable(GameObject prefab)
        {
            ItemDrop itemDrop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (itemDrop == null)
                return;

            ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;
            if (shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable)
                return;

            if (shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f)
            {
                BindFood(prefab.name, shared);
                ItemValues.RegisterFood(prefab.name);
            }
            else if (shared.m_consumeStatusEffect is SE_Stats effect)
            {
                BindMead(prefab.name, shared.m_name, effect);
                ItemValues.RegisterMead(prefab.name, effect);
            }
        }

        private static void BindFood(string prefabName, ItemDrop.ItemData.SharedData shared)
        {
            if (FoodConfigs.ContainsKey(prefabName))
                return;

            string name = DisplayName(shared.m_name, prefabName), token = shared.m_name;
            FoodConfigs[prefabName] = new Dictionary<string, ConfigEntry<float>>
            {
                [Health] = BindItem(SettingTemplates.Food, prefabName, Health, shared.m_food, name, token),
                [Stamina] = BindItem(SettingTemplates.Food, prefabName, Stamina, shared.m_foodStamina, name, token),
                [Duration] = BindItem(SettingTemplates.Food, prefabName, Duration, shared.m_foodBurnTime, name, token),
                [HealthRegen] = BindItem(SettingTemplates.Food, prefabName, HealthRegen, shared.m_foodRegen, name, token),
                [Eitr] = BindItem(SettingTemplates.Food, prefabName, Eitr, shared.m_foodEitr, name, token),
                [Vigor] = BindItem(SettingTemplates.Food, prefabName, Vigor, 0f, name, token),
                [EitrVigor] = BindItem(SettingTemplates.Food, prefabName, EitrVigor, 0f, name, token),
            };
            if (!string.IsNullOrEmpty(shared.m_name))
                foodsByToken[shared.m_name] = prefabName;
            FoodLookup.Forget();
        }

        /// <summary>
        /// Binds the food a placed feast feeds, found through the feast in ZNetScene rather than the item database,
        /// so it gets a section whatever its item type. Returns whether a section was added.
        /// </summary>
        public static bool BindFeastFood(ItemDrop food)
        {
            ItemDrop.ItemData.SharedData shared = food.m_itemData.m_shared;
            string prefabName = food.gameObject.name;
            if (FoodConfigs.ContainsKey(prefabName) || (shared.m_food <= 0f && shared.m_foodStamina <= 0f && shared.m_foodEitr <= 0f))
                return false;
            BindFood(prefabName, shared);
            ItemValues.RegisterFood(prefabName);
            return true;
        }

        /// <summary>The item's name as players see it, or the prefab name when no translation is loaded.</summary>
        internal static string DisplayName(ItemDrop item)
        {
            return item == null ? "" : DisplayName(item.m_itemData?.m_shared?.m_name ?? "", item.name);
        }

        internal static string DisplayName(string token, string prefabName)
        {
            if (string.IsNullOrEmpty(token) || Localization.instance == null)
                return prefabName;
            string name = Localization.instance.Localize(token);
            return string.IsNullOrEmpty(name) || name[0] == '$' || name[0] == '[' ? prefabName : name;
        }

        /// <summary>Mead sections were named 0Meads_prefab up to 3.3.5; the four old values are carried over.</summary>
        private static void BindMead(string prefabName, string token, SE_Stats effect)
        {
            if (MeadConfigs.ContainsKey(prefabName))
                return;

            MeadEffectConfig config = new MeadEffectConfig
            {
                Duration = BindItem(SettingTemplates.Mead, prefabName, "Duration", effect.m_ttl, prefabName, token),
                HealthOverTime = BindItem(SettingTemplates.Mead, prefabName, "HealthOverTime", effect.m_healthOverTime, prefabName, token),
                StaminaOverTime = BindItem(SettingTemplates.Mead, prefabName, "StaminaOverTime", effect.m_staminaOverTime, prefabName, token),
                EitrOverTime = BindItem(SettingTemplates.Mead, prefabName, "EitrOverTime", effect.m_eitrOverTime, prefabName, token),
            };
            BindMeadBonuses(prefabName, token, effect, config);
            MeadConfigs[prefabName] = config;
            MeadsByEffectHash[effect.NameHash()] = config;
        }

        private static void BindMeadBonuses(string prefabName, string token, SE_Stats effect, MeadEffectConfig config)
        {
            config.HealthRegenMultiplier = BindItem(SettingTemplates.Mead, prefabName, "HealthRegenMultiplier", effect.m_healthRegenMultiplier, prefabName, token);
            config.StaminaRegenMultiplier = BindItem(SettingTemplates.Mead, prefabName, "StaminaRegenMultiplier", effect.m_staminaRegenMultiplier, prefabName, token);
            config.EitrRegenMultiplier = BindItem(SettingTemplates.Mead, prefabName, "EitrRegenMultiplier", effect.m_eitrRegenMultiplier, prefabName, token);
            config.RunStaminaModifier = BindItem(SettingTemplates.Mead, prefabName, "RunStaminaModifier", effect.m_runStaminaDrainModifier, prefabName, token);
            config.JumpStaminaModifier = BindItem(SettingTemplates.Mead, prefabName, "JumpStaminaModifier", effect.m_jumpStaminaUseModifier, prefabName, token);
        }

        /// <summary>
        /// One entry of a food or mead section. The description names the item as before (a food by its name, a mead by
        /// its prefab); other languages show the translated template with the item's name in the game's language.
        /// </summary>
        private static ConfigEntry<float> BindItem(string kind, string prefabName, string key, float value, string subject, string token)
        {
            string Name() => DisplayName(token, prefabName);
            return synced.Bind(SettingTemplates.SectionPrefix[kind] + prefabName, key, value, SettingTemplates.Help(kind, key, subject),
                presentation: LarderSettingPresentation.For(kind, key, Name, () => new object[] { Name() }));
        }

        /// <summary>
        /// The config of a food item, or false for any other item. Hot (the regen hooks every physics step, item
        /// spawns): the answer is kept per shared data object (<see cref="FoodLookup"/>), so the prefab's name, which
        /// allocates, is read once per item kind.
        /// </summary>
        public static bool TryGetFood(ItemDrop.ItemData item, out Dictionary<string, ConfigEntry<float>> configs)
        {
            configs = null;
            return item != null && FoodLookup.TryGet(item, out configs);
        }

        /// <summary>Finds the config of a food item by its prefab, falling back to its localization token.</summary>
        internal static bool FindFood(ItemDrop.ItemData item, out Dictionary<string, ConfigEntry<float>> configs)
        {
            configs = null;

            if (item.m_dropPrefab != null && FoodConfigs.TryGetValue(item.m_dropPrefab.name, out configs))
                return true;

            return item.m_shared.m_name != null
                && foodsByToken.TryGetValue(item.m_shared.m_name, out string prefabName)
                && FoodConfigs.TryGetValue(prefabName, out configs);
        }
    }
}
