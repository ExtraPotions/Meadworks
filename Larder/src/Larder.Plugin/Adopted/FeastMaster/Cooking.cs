// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/Cooking.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>The station's own values, replaced for one cooking tick; Times is null when nothing was replaced.</summary>
    public struct CookingSwap
    {
        public bool CanOvercook;
        public float[] Times;
    }

    /// <summary>
    /// Cook time and burning. UpdateCooking runs every second on every peer but cooks only on the station's ZDO
    /// owner: it adds the elapsed world time to each slot's cooked time (kept in the ZDO), marks a slot done past
    /// the recipe's m_cookTime and burnt past twice that while m_canOvercookItems is set. Both are replaced for
    /// that one call on the owner and restored afterwards, so edits reach food already on the fire.
    /// </summary>
    [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.UpdateCooking))]
    public static class CookingPatch
    {
        public static bool Prepare() => Customized.Any(Settings.CookTimeMultiplier, Settings.FoodCanBurn) || CookTimes.AnyChanged() || NativeFeastMaster.InstantKitchen;

        [HarmonyPrefix]
        public static void Prefix(CookingStation __instance, out CookingSwap __state) => __state = CookingRules.Swap(__instance);

        [HarmonyFinalizer]
        public static void Finalizer(CookingStation __instance, CookingSwap __state) => CookingRules.Restore(__instance, __state);
    }

    public static class CookingRules
    {
        // The station's own times for the one call in progress: UpdateCooking does not run inside another, so one buffer
        // serves every station; a call that somehow nests gets an array of its own.
        private static float[] buffer = new float[8];
        private static bool bufferInUse;

        /// <summary>Applies the configured values on the owner; nothing replaced (Times null) on every other peer.</summary>
        public static CookingSwap Swap(CookingStation station)
        {
            ZNetView view = station.m_nview;
            if (view == null || !view.IsValid() || !view.IsOwner())
                return default;
            CookingSwap saved = new CookingSwap { CanOvercook = station.m_canOvercookItems };
            station.m_canOvercookItems &= NativeFeastMaster.Value(Settings.FoodCanBurn) && !NativeFeastMaster.InstantKitchen;
            CookTimes.TryGet(view.GetZDO().GetPrefab(), out Dictionary<ItemDrop, ConfigEntry<float>> entries);
            saved.Times = SwapTimes(station.m_conversion, entries, NativeFeastMaster.Value(Settings.CookTimeMultiplier));
            return saved;
        }

        private static float[] Times(int count)
        {
            if (bufferInUse)
                return new float[count];
            if (buffer.Length < count)
                buffer = new float[count];
            bufferInUse = true;
            return buffer;
        }

        /// <summary>Each recipe's time becomes its station entry (or its own time) times the multiplier.</summary>
        private static float[] SwapTimes(List<CookingStation.ItemConversion> recipes, Dictionary<ItemDrop, ConfigEntry<float>> entries, float multiplier)
        {
            float[] times = Times(recipes.Count);
            for (int i = 0; i < recipes.Count; i++)
            {
                CookingStation.ItemConversion recipe = recipes[i];
                if (recipe == null)
                    continue;
                times[i] = recipe.m_cookTime;
                ConfigEntry<float> entry = null;
                bool configured = entries != null && recipe.m_from != null && entries.TryGetValue(recipe.m_from, out entry);
                recipe.m_cookTime = NativeFeastMaster.InstantKitchen ? 0f : (configured ? NativeFeastMaster.Value(entry) : recipe.m_cookTime) * multiplier;
            }
            return times;
        }

        public static void Restore(CookingStation station, CookingSwap saved)
        {
            if (saved.Times == null)
                return;
            station.m_canOvercookItems = saved.CanOvercook;
            List<CookingStation.ItemConversion> recipes = station.m_conversion;
            for (int i = 0; i < saved.Times.Length && i < recipes.Count; i++)
            {
                if (recipes[i] != null)
                    recipes[i].m_cookTime = saved.Times[i];
            }
            if (ReferenceEquals(saved.Times, buffer))
                bufferInUse = false;
        }
    }
}
