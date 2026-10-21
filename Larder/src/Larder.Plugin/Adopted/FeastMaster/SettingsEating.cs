// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/SettingsEating.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using BepInEx.Configuration;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>Food Slots, and Auto Eat: the server's permission in section 0, the player's own choice in section 8.</summary>
    public static partial class Settings
    {
        public static ConfigEntry<bool> AllowAutoEat { get; private set; }
        public static ConfigEntry<bool> AutoEat { get; private set; }
        public static ConfigEntry<int> FoodSlots { get; private set; }

        private static void BindEating(NativeBindings config)
        {
            FoodSlots = config.Bind("3 - Eating", "Food Slots", global::ExtraPotions.Larder.Runtime.Adopted.FeastMaster.FoodSlots.GameSlots,
                "How many foods a player can have active at once. The game uses 3. More slots add a lot of health and stamina; the global multipliers above can balance that. Lowering it keeps extra foods until they run out.",
                acceptableValues: new AcceptableValueRange<int>(1, global::ExtraPotions.Larder.Runtime.Adopted.FeastMaster.FoodSlots.MaxSlots));
            AllowAutoEat = config.Bind("3 - Eating", "Allow Auto Eat", false,
                "When on, a player whose food runs out automatically eats another of the same food from their inventory, if they have one. Each player can still turn it off for themselves with Auto Eat in this section.");
            AutoEat = config.Bind(DisplaySection, "Auto Eat", true,
                "Eat another of the same food when one runs out, if the server allows it with Allow Auto Eat in this section. Your own choice, not synced from the server.", synced: false);
        }
    }
}
