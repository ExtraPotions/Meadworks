// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/FeastServings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// Feast servings. A placed feast keeps the servings left in its ZDO once someone has eaten from it; until then
    /// GetStack falls back to m_eatStacks, which is also the full count behind GetStackPercentige (visual stages and
    /// the deconstruct refund) and the hover's "left/full". The field is replaced for each of those calls, so a
    /// change reaches untouched feasts at once and 0 leaves the feast's own count. The owner decides each serving
    /// in RPC_TryEat through GetStack; every peer draws the hover and the stages.
    /// </summary>
    public static class FeastServings
    {
        /// <summary>The feast's own count when replaced, -1 when nothing was.</summary>
        public static int Swap(Feast feast)
        {
            int servings = NativeFeastMaster.Value(Settings.FeastServings);
            if (servings <= 0 || feast.m_eatStacks == servings)
                return -1;
            int own = feast.m_eatStacks;
            feast.m_eatStacks = servings;
            return own;
        }

        public static void Restore(Feast feast, int own)
        {
            if (own >= 0)
                feast.m_eatStacks = own;
        }
    }

    [HarmonyPatch(typeof(Feast), nameof(Feast.GetStack))]
    public static class FeastStackPatch
    {
        public static bool Prepare() => Customized.Any(Settings.FeastServings);

        [HarmonyPrefix]
        public static void Prefix(Feast __instance, out int __state) => __state = FeastServings.Swap(__instance);

        [HarmonyFinalizer]
        public static void Finalizer(Feast __instance, int __state) => FeastServings.Restore(__instance, __state);
    }

    /// <summary>
    /// The share of servings left. Capped at 1: a feast started under a larger count keeps more servings than a
    /// lowered count, and the deconstruct refund multiplies the feast's resources by this share.
    /// </summary>
    [HarmonyPatch(typeof(Feast), nameof(Feast.GetStackPercentige))]
    public static class FeastShareLeftPatch
    {
        public static bool Prepare() => Customized.Any(Settings.FeastServings);

        [HarmonyPrefix]
        public static void Prefix(Feast __instance, out int __state) => __state = FeastServings.Swap(__instance);

        [HarmonyPostfix]
        public static void Postfix(ref float __result) => __result = Mathf.Min(__result, 1f);

        [HarmonyFinalizer]
        public static void Finalizer(Feast __instance, int __state) => FeastServings.Restore(__instance, __state);
    }

    [HarmonyPatch(typeof(Feast), nameof(Feast.GetHoverText))]
    public static class FeastHoverPatch
    {
        public static bool Prepare() => Customized.Any(Settings.FeastServings);

        [HarmonyPrefix]
        public static void Prefix(Feast __instance, out int __state) => __state = FeastServings.Swap(__instance);

        [HarmonyFinalizer]
        public static void Finalizer(Feast __instance, int __state) => FeastServings.Restore(__instance, __state);
    }
}
