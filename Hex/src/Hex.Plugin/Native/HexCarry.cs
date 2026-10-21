// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexCarry
{
    [HarmonyPatch(typeof(Player), nameof(Player.GetMaxCarryWeight))]
    private static class Capacity
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Player __instance, ref float __result)
        {
            if (!HexMovement.Owned(__instance) || Plugin.Settings == null) return;
            __result = HexCatalog.CarryCapacity(__result, Plugin.Settings.Effective("no_carry_limit", false), Plugin.Settings.Effective("custom_carry_weight", 0));
            // Neutral/blocked values leave native, equipment and other mods' capacity contributions untouched.
        }
    }
    [HarmonyPatch(typeof(InventoryGui), "UpdateInventoryWeight")]
    private static class NativeReadout
    {
        // After other prefixes: when one already replaced the game's write (Mantle's stacked weight), writing the
        // fraction here too changed the label twice a frame and rebuilt the inventory's canvas every frame.
        [HarmonyPriority(Priority.Low)]
        private static bool Prefix(InventoryGui __instance, Player player, bool __runOriginal)
        {
            if (!__runOriginal) return false;
            if (!HexMovement.Owned(player) || __instance.m_weight == null || !float.IsPositiveInfinity(player.GetMaxCarryWeight())) return true;
            // Native CeilToInt cannot represent an unlimited capacity. Keep its ordinary fraction and existing text.
            // Mantle's existing postfix may still use its stacked layout; its formatter also recognizes infinity.
            __instance.m_weight.text = Mathf.CeilToInt(player.GetInventory().GetTotalWeight()) + "/∞";
            return false;
        }
    }
}
