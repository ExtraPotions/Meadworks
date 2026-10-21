// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Presentation boundary over the adopted Perks/CookLevel; the skill page remains an optional shared capability.
using HarmonyLib;
namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    public static class KitchenHover
    {
        [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.GetHoverText))]
        static class Crafting
        {
            [HarmonyPostfix]
            static void Postfix(CraftingStation __instance, ref string __result)
            {
                if (!Kitchen.IsKitchen(__instance) || string.IsNullOrEmpty(__result)) return;
                float level = CookLevel.Local();
                __result += "\n" + LarderText.Format("crafting_perks", Perks.ExtraFoodChance(level) * 100f,
                    Perks.IngredientSaveChance(level) * 100f);
            }
        }
        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.GetHoverText))]
        static class Station
        {
            [HarmonyPostfix]
            static void Postfix(CookingStation __instance, ref string __result)
            {
                if (!Kitchen.IsKitchen(__instance) || string.IsNullOrEmpty(__result)) return;
                float level = CookLevel.Local();
                __result += "\n" + LarderText.Format("cooking_perks", (Perks.CookingSpeed(level) - 1f) * 100f,
                    Perks.ExtraBurnTime(level) * 100f);
            }
        }
        [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.GetHoverText))]
        static class Barrel
        {
            [HarmonyPostfix]
            static void Postfix(Fermenter __instance, ref string __result)
            {
                if (!NativeCooking.Active || string.IsNullOrEmpty(__result)) return;
                float level = __instance.m_nview != null && __instance.m_nview.IsValid() && __instance.GetContent() != 0
                    ? FermenterBase.GetLevel(__instance.m_nview.GetZDO()) : CookLevel.Local();
                __result += "\n" + LarderText.Format("fermenting_perks", (Perks.FermentingSpeed(level) - 1f) * 100f);
            }
        }
    }
}
