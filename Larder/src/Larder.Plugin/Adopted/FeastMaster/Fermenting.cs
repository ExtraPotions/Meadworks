// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/Fermenting.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using System.Collections.Generic;
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// Fermenter brew time. The barrel stores only its start time in the ZDO; GetStatus compares the elapsed world
    /// time with m_fermentationDuration on every peer (hover, visuals, interact) and on the ZDO owner before a tap.
    /// The field is replaced for that one call, so an edit reaches barrels already brewing and 0 leaves the barrel's
    /// own time.
    /// </summary>
    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.GetStatus))]
    public static class FermentationTimePatch
    {
        public static bool Prepare() => Customized.Any(Settings.FermentationTime) || NativeFeastMaster.InstantKitchen;

        // Original hover adapter shares the same effective duration; no field mutation outside GetStatus.
        internal static float EffectiveDuration(Fermenter barrel) => NativeFeastMaster.InstantKitchen ? 0f
            : ExtraPotions.Larder.Kitchen.KitchenRules.FermentationDuration(barrel.m_fermentationDuration,
                NativeFeastMaster.Value(Settings.FermentationTime));

        [HarmonyPrefix]
        public static void Prefix(Fermenter __instance, out ScaledFields __state)
        {
            __state = default;
            float time = NativeFeastMaster.InstantKitchen ? 0f : NativeFeastMaster.Value(Settings.FermentationTime);
            if (time > 0f || NativeFeastMaster.InstantKitchen)
                __state = CostRules.Swap(ref __instance.m_fermentationDuration, time);
        }

        [HarmonyFinalizer]
        public static void Finalizer(Fermenter __instance, ScaledFields __state)
        {
            CostRules.Restore(ref __instance.m_fermentationDuration, __state);
        }
    }

    /// <summary>
    /// Fermenter batch yield: every conversion's m_producedItems replaced for one call and restored afterwards.
    /// The batch is spawned on the barrel's ZDO owner in DelayedTap, 1.5 s after a tap. (The game's DropAllItems
    /// also reads the count but nothing calls it: a destroyed barrel loses its batch.)
    /// </summary>
    public static class FermenterYield
    {
        public static int[] Swap(Fermenter fermenter)
        {
            int yield = NativeFeastMaster.Value(Settings.BatchYield);
            if (yield <= 0)
                return null;
            List<Fermenter.ItemConversion> conversions = fermenter.m_conversion;
            int[] saved = new int[conversions.Count];
            for (int i = 0; i < conversions.Count; i++)
            {
                saved[i] = conversions[i].m_producedItems;
                conversions[i].m_producedItems = yield;
            }
            return saved;
        }

        public static void Restore(Fermenter fermenter, int[] saved)
        {
            if (saved == null)
                return;
            List<Fermenter.ItemConversion> conversions = fermenter.m_conversion;
            for (int i = 0; i < saved.Length && i < conversions.Count; i++)
                conversions[i].m_producedItems = saved[i];
        }
    }

    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.DelayedTap))]
    public static class TapYieldPatch
    {
        public static bool Prepare() => Customized.Any(Settings.BatchYield);

        [HarmonyPrefix]
        public static void Prefix(Fermenter __instance, out int[] __state) => __state = FermenterYield.Swap(__instance);

        [HarmonyFinalizer]
        public static void Finalizer(Fermenter __instance, int[] __state) => FermenterYield.Restore(__instance, __state);
    }
}
