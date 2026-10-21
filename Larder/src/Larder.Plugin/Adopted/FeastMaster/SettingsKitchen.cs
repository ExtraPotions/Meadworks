// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/SettingsKitchen.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using BepInEx.Configuration;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// Section 9: the kitchen (fermenter, cooking stations, feasts). Synced, and read at use time: whenever a
    /// barrel or feast is checked, a barrel tapped, and on every cooking tick. The per-recipe cook times live in one section per cooking station, see
    /// <see cref="CookTimes"/>.
    /// </summary>
    public static partial class Settings
    {
        /// <summary>The section's name in 4.2.0, before cooking joined it; its entries are carried over.</summary>
        private const string OldFermenterSection = "9. Fermenter";

        // 9. Kitchen
        public static ConfigEntry<float> FermentationTime { get; private set; }
        public static ConfigEntry<int> BatchYield { get; private set; }
        public static ConfigEntry<float> CookTimeMultiplier { get; private set; }
        public static ConfigEntry<bool> FoodCanBurn { get; private set; }
        public static ConfigEntry<int> FeastServings { get; private set; }

        private static void BindKitchen(NativeBindings config)
        {
            BindFermenter(config);
            CookTimeMultiplier = config.Bind(KitchenSection, "Cook Time Multiplier", 1f,
                "Multiplier on the cook time of every recipe on every cooking station and oven, on top of each recipe's own time in the station's section (one section per station, named after its prefab). 0.5 cooks twice as fast. Food on the fire when the value changes follows at once.",
                acceptableValues: new AcceptableValueRange<float>(0.01f, 100f));
            FoodCanBurn = config.Bind(KitchenSection, "Food Can Burn", true,
                "On: cooked food left on a station burns as in the game, at twice its cook time. Off: cooked food never burns and waits on the station until it is taken.");
            FeastServings = config.Bind(KitchenSection, "Feast Servings", 0,
                "Servings a placed feast holds. 0 keeps each feast's own count. Feasts nobody has eaten from follow a change at once; a feast already started keeps the servings it has left.",
                acceptableValues: new AcceptableValueRange<int>(0, 100));
        }

        private static void BindFermenter(NativeBindings config)
        {
            FermentationTime = BindMoved(config, "Fermentation Time", 0f,
                "Seconds a fermenter takes to brew a batch. 0 keeps each barrel's own time (the game's fermenter takes 2400, 40 minutes). Also applies to barrels already brewing: one that has brewed longer than the new time is ready at once.",
                new AcceptableValueRange<float>(0f, 86400f));
            BatchYield = BindMoved(config, "Batch Yield", 0,
                "Meads tapped from one batch. 0 keeps each recipe's own yield. Applies when a batch is tapped, so it also covers batches already brewing.",
                new AcceptableValueRange<int>(0, 100));
        }

        /// <summary>Binds an entry of section 9, carrying over its value from the 4.2.0 section name.</summary>
        private static ConfigEntry<T> BindMoved<T>(NativeBindings config, string key, T defaultValue, string description, AcceptableValueBase range)
        {

            ConfigEntry<T> entry = config.Bind(KitchenSection, key, defaultValue, description, acceptableValues: range);

            return entry;
        }
    }
}
