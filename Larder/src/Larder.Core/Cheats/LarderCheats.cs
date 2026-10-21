// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;

namespace ExtraPotions.Larder.Cheats
{
    /// <summary>Larder's cheats (design 4.10, user decision Q3).</summary>
    public enum LarderCheat
    {
        /// <summary>Station slots turn done at the next cooking tick, fermenters are ready at once, nothing burns.</summary>
        InstantKitchen,

        /// <summary>Active foods never run down (the HUD timer holds).</summary>
        EndlessFood,

        /// <summary>Food slots never report "full" and Eat Again At reads 1 for the cheating player.</summary>
        EatAnything,
    }

    /// <summary>Why the authority gate blocks a cheat (Crucible C02, design 4.10); None means allowed.</summary>
    public enum CheatBlock
    {
        None,
        NotAdmin,
        ProductDisabled,
        VanillaServer,
        AuthorityPending,
    }

    /// <summary>Cheat effective values: a blocked cheat reads its safe value (off); the gate decision comes from the plugin.</summary>
    public static class LarderCheats
    {
        public static IReadOnlyList<LarderCheat> All { get; } = new[] { LarderCheat.InstantKitchen, LarderCheat.EndlessFood, LarderCheat.EatAnything };

        /// <summary>Every Larder cheat's safe value is off.</summary>
        public static bool SafeValue(LarderCheat cheat) => false;

        public static bool Effective(LarderCheat cheat, CheatBlock block, bool value) => block == CheatBlock.None ? value : SafeValue(cheat);
    }

    /// <summary>What Instant Kitchen does to the kitchen values (L11 applies it in the plugin).</summary>
    public static class KitchenCheats
    {
        /// <summary>The recipe time: 0 under Instant Kitchen, so the slot is done at the next cooking tick.</summary>
        public static float RecipeTime(float time, bool instantKitchen) => instantKitchen ? 0f : time;

        /// <summary>Nothing burns under Instant Kitchen.</summary>
        public static bool CanBurn(bool canBurn, bool instantKitchen) => canBurn && !instantKitchen;

        /// <summary>The fermentation duration: 0 under Instant Kitchen, so a barrel with a base is ready at once.</summary>
        public static float FermentationDuration(float duration, bool instantKitchen) => instantKitchen ? 0f : duration;
    }
}
