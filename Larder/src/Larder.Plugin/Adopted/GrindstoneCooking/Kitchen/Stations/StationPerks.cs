// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Kitchen/Stations/StationPerks.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// Cooking speed and the burn window at cooking stations and the oven, per slot, from the Cooking level stored
    /// with the slot. The game's UpdateCooking runs every second on every peer and cooks only on the ZDO owner, and
    /// only while the station is lit or fuelled: it takes the world time since its last run (GetDeltaTime, which moves
    /// the ZDO's start time on), adds it to every slot that is neither empty nor burnt, and then applies its done and
    /// burnt thresholds. The prefix runs after FeastMaster's, which puts its own cook times into the recipes for that
    /// one call. It works out the same elapsed time without moving the start time, and moves each slot's cooked time
    /// beforehand so that the game's own addition lands on <see cref="CookClock.Advance"/>; the game's thresholds then
    /// decide done and burnt as usual. The finalizer puts a slot back when the game did not add its time after all
    /// (another mod skipped the method, or it threw before reaching the slot).
    /// </summary>
    [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.UpdateCooking))]
    public static class StationPerks
    {
        internal struct Shift
        {
            public int Slot;
            public float Before;
            public float Stored;
        }

        [HarmonyPrefix]
        [HarmonyAfter("com.meadworks.larder.feastmaster")]
        private static void Prefix(CookingStation __instance, out List<Shift> __state)
        {
            __state = null;
            ZNetView nview = __instance.m_nview;
            if (!Kitchen.IsKitchen(__instance) || nview == null || !nview.IsValid() || !nview.IsOwner() || nview.GetZDO() == null || !WillCook(__instance))
                return;
            float elapsed = PendingDelta(nview.GetZDO());
            if (elapsed <= 0f)
                return;
            for (int slot = 0; slot < __instance.m_slots.Length; slot++)
                AdjustSlot(__instance, slot, elapsed, ref __state);
        }

        [HarmonyFinalizer]
        private static void Finalizer(CookingStation __instance, List<Shift> __state)
        {
            if (__state == null || !__instance.m_nview.IsValid())
                return;
            foreach (Shift shift in __state)
                Undo(__instance, shift);
        }

        /// <summary>The game's own test in UpdateCooking for adding time this run.</summary>
        private static bool WillCook(CookingStation station) =>
            (station.m_requireFire && station.IsFireLit()) ||
            (station.m_useFuel && station.GetFuel() > 0f && (station.m_useFueldWhileEmpty || station.HaveUncookedItem()));

        /// <summary>What GetDeltaTime will return this run, without writing the new start time.</summary>
        private static float PendingDelta(ZDO zdo)
        {
            DateTime now = ZNet.instance.GetTime();
            DateTime start = new DateTime(zdo.GetLong(ZDOVars.s_startTime, now.Ticks));
            return (float)(now - start).TotalSeconds;
        }

        private static void AdjustSlot(CookingStation station, int slot, float elapsed, ref List<Shift> shifts)
        {
            station.GetSlot(slot, out string name, out float cooked, out CookingStation.Status status, out bool cheated);
            if (name == "" || status == CookingStation.Status.Burnt)
                return;
            CookingStation.ItemConversion conversion = station.GetItemConversion(name);
            if (conversion == null)
                return;
            float level = StationSlots.Level(station.m_nview.GetZDO(), slot);
            float speed = Perks.CookingSpeed(level);
            float extraBurn = Perks.ExtraBurnTime(level);
            if (CookClock.IsVanilla(speed, extraBurn))
                return;
            float stored = CookClock.Advance(cooked, elapsed, conversion.m_cookTime, speed, extraBurn) - elapsed;
            station.SetSlot(slot, name, stored, status, cheated);
            (shifts ?? (shifts = new List<Shift>())).Add(new Shift { Slot = slot, Before = cooked, Stored = stored });
        }

        /// <summary>Restores a slot whose cooked time is still exactly what the prefix stored: the game added nothing.</summary>
        private static void Undo(CookingStation station, Shift shift)
        {
            station.GetSlot(shift.Slot, out string name, out float cooked, out CookingStation.Status status, out bool cheated);
            if (name != "" && cooked == shift.Stored)
                station.SetSlot(shift.Slot, name, shift.Before, status, cheated);
        }
    }
}
