// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from OpenKeep 3.0.0 ad278ecf323901c3645eb1f9f6fac6833a0f8cb1,
// OpenKeep/OpenKeep/src/Batch/BatchAmount.cs. Copyright (C) 2026 Gerald Glasgow.
// GNU GPL version 3 or later; full notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: Kindle settings/lifecycle; stepping/repeats/typing delegated to KitStepper.
using ExtraPotions.Kindle.Config;
using UnityEngine;

namespace ExtraPotions.Kindle.Runtime
{
    /// <summary>
    /// The amount the stepper shows, counted in crafts: a recipe that makes 20 arrows makes 20 per step, and the game's
    /// recipe name above shows the total. It goes back to 1 when another recipe is selected and never stays above what
    /// can be made now (<see cref="CanMake"/>), so once a batch has used up the materials it drops by itself.
    /// </summary>
    public static class BatchAmount
    {
        private static Recipe recipe;

        public static int Value { get; private set; } = 1;

        /// <summary>The recipe the amount belongs to (the one selected on the Craft tab when the stepper last showed).</summary>
        public static Recipe Current => recipe;

        internal static void Reset() { recipe = null; Value = 1; }

        /// <summary>The stepper belongs to the Craft tab (not Upgrade, not Salvage) of every station and of crafting by hand, except an upgrader.</summary>
        public static bool Applies(InventoryGui gui, Player player)
        {
            if (!KindleBatch.Active || SalvageTabCapability.Active || !KindleBatch.Config.Get(KindleSettings.Batch.Enabled) || gui == null || player == null || !gui.InCraftTab())
                return false;
            CraftingStation station = player.GetCurrentCraftingStation();
            return station == null || !station.m_upgrader;
        }

        /// <summary>Every frame while the stepper applies: back to 1 on another recipe, down to what can be made.</summary>
        public static void Follow(Player player, Recipe selected)
        {
            if (selected != recipe)
            {
                recipe = selected;
                Value = 1;
            }
            if (recipe != null && Value > 1 && !CanMake(player, recipe, Value))
                Value = Limit(player, recipe);
        }

        public static bool CanStepUp(Player player) => recipe != null && CanMake(player, recipe, Value + 1);

        /// <summary>How many crafts the next press of Craft makes on the Craft tab: the stepper's amount, or without it the game's Shift + Craft rule.</summary>
        public static int NextCraft(InventoryGui gui, Player player)
        {
            if (Applies(gui, player))
                return Value;
            bool multi = ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyLStick") || gui.m_touchMultiCrafting;
            return multi ? gui.m_multiCraftAmount : 1;
        }

        /// <summary>An amount typed into the field, kept within 1 and the most that can be made.</summary>
        public static void Set(int typed)
        {
            Player player = Player.m_localPlayer;
            if (player == null || recipe == null)
                return;
            Value = Mathf.Clamp(typed, 1, Limit(player, recipe));
        }

        /// <summary>
        /// Whether that many crafts can be made now: within Max Amount, the materials (the game's HaveRequirements, so the
        /// station counts and, with Reach, the containers; skipped without a craft cost) and room for what is made, the
        /// last two remembered while nothing changed (<see cref="BatchCheck"/>). One craft always passes; the game checks
        /// it as usual.
        /// </summary>
        public static bool CanMake(Player player, Recipe made, int amount)
        {
            if (amount <= 1)
                return true;
            if (amount > KindleBatch.Config.Get(KindleSettings.Batch.MaxAmount))
                return false;
            return BatchCheck.HaveAndFit(player, made, amount);
        }

        /// <summary>The most crafts that can be made now, at least 1. CanMake only turns false as the amount grows, so a binary search finds it.</summary>
        public static int Limit(Player player, Recipe made)
        {
            int low = 1;
            int high = KindleBatch.Config.Get(KindleSettings.Batch.MaxAmount);
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (CanMake(player, made, middle))
                    low = middle;
                else
                    high = middle - 1;
            }
            return low;
        }
    }
}
