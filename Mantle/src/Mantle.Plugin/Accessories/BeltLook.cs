// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;

namespace Mantle.Accessories
{
    /// <summary>
    /// The worn game belt (<see cref="BeltWear"/>) is not the game's utility item, so the game would not draw it: its look is added to
    /// the character's equipment after the game set it up, as the game draws a worn Megingjord. The game's utility, when there is
    /// one, loses the look to the belt (the character shows one utility item). Local player only; the game replicates the look.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.SetupVisEquipment))]
    public static class BeltLook
    {
        [HarmonyPostfix]
        public static void Postfix(Humanoid __instance, VisEquipment visEq)
        {
            string look = BeltWear.LookName(__instance);
            if (look != null && visEq != null)
                visEq.SetUtilityItem(look.GetStableHashCode());
        }
    }
}
