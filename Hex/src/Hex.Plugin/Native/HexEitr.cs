// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
// Regeneration-rate references were not adopted: increasing regeneration does not implement unlimited eitr.
using HarmonyLib;
namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexEitr
{
    private static bool On(Character actor) => HexMovement.On(actor, "unlimited_eitr");
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    [HarmonyPatch(typeof(Player), nameof(Player.UseEitr))]
    private static class Consumption
    {
        private static void Prefix(Player __instance, ref float v) { if (v > 0f && Finite(v) && On(__instance)) v = 0f; }
    }
    [HarmonyPatch(typeof(Player), nameof(Player.HaveEitr))]
    private static class Availability
    {
        private static void Postfix(Player __instance, float amount, ref bool __result)
        {
            if (Finite(amount) && On(__instance)) __result = true;
        }
    }
    [HarmonyPatch(typeof(Character), nameof(Character.TryUseEitr))]
    private static class NativeUseGate
    {
        // Native TryUseEitr refuses a zero food-derived maximum before it asks HaveEitr. Answer this availability gate;
        // do not fabricate food, change the maximum, refill the reserve or save an artificial resource amount.
        private static bool Prefix(Character __instance, float eitrUse, ref bool __result)
        {
            if (!(eitrUse > 0f && Finite(eitrUse) && On(__instance))) return true;
            __result = true;
            return false; // also avoids the native "eitr required" warning for an allowed zero-maximum action
        }
    }
}
