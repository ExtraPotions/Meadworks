// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT. Adapted from BetterSleepBruh 2.1.0 at
// fe2bb81866e58f3c4afeb832876cba6ebf1e12cd: BetterSleepBruh/Patches/CookingStationPatches.cs, SmelterPatches.cs,
// FermenterPatches.cs, PlantPatches.cs and PickablePatches.cs. The MIT notice is retained in
// THIRD_PARTY_NOTICES/BetterSleepBruh-MIT.txt.
// Changed by expDARE, 2026: one shared owner-only shift helper over Ember.Core StationTimers; the rate and the boost
// total come from Ember's sleep runtime (the authority's own values, or the last state reply); nothing shifts until
// the total is known, so a client that has not yet heard from the authority cannot reset a marker; Ember's own ZDO
// marker keys (BetterSleepBruh's markers count a different total); gated on Ember's sleep module; namespace Ember.
using ExtraPotions.Ember.Config;
using ExtraPotions.Ember.Sleep;
using HarmonyLib;

namespace ExtraPotions.Ember.Runtime
{
    /// <summary>Station timers progress at normal speed during the partial sleep boost (one server toggle per kind).</summary>
    [HarmonyPatch]
    internal static class StationTimerPatches
    {
        // Old markers count every boost interval; first use of the protected totals only establishes a baseline.
        static readonly int FermenterMarker = "meadworks.ember.fermenter_protected_boost".GetStableHashCode();
        static readonly int PlantMarker = "meadworks.ember.plant_protected_boost".GetStableHashCode();
        static readonly int PickableMarker = "meadworks.ember.pickable_protected_boost".GetStableHashCode();

        /// <summary>Moves the owned timer's start forward by the boost added since its marker, then advances the marker.</summary>
        static void ShiftStart(ZNetView? nview, int timeKey, int markerKey, bool requireStart, EmberSetting<bool> setting)
        {
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
            var zdo = nview.GetZDO();
            if (zdo == null) return;
            if (requireStart && zdo.GetLong(timeKey, 0L) == 0L) return;
            if (!SleepRuntime.TryStationBoostTotal(setting, out var total)) return;
            var decision = StationTimers.Shift(StationTimers.ToTicks(total), zdo.GetLong(markerKey, -1L));
            if (decision.Shift > 0L)
            {
                var start = zdo.GetLong(timeKey, 0L);
                if (start > 0L) zdo.Set(timeKey, start + decision.Shift);
            }
            if (decision.Mark) zdo.Set(markerKey, decision.Marker);
        }

        [HarmonyPatch(typeof(Smelter), nameof(Smelter.GetDeltaTime)), HarmonyPostfix]
        static void SmelterDelta(ref double __result)
        {
            if (SleepRuntime.StationsOn(EmberSettings.Stations.ProtectSmelterTimers))
                __result = StationTimers.ScaleDelta(__result, SleepRuntime.StationRate);
        }

        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.GetDeltaTime)), HarmonyPostfix]
        static void CookingDelta(ref float __result)
        {
            if (SleepRuntime.StationsOn(EmberSettings.Stations.ProtectCookingTimers))
                __result = (float)StationTimers.ScaleDelta(__result, SleepRuntime.StationRate);
        }

        [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.GetFermentationTime)), HarmonyPrefix]
        static void Fermentation(Fermenter __instance)
        {
            if (__instance != null)
                ShiftStart(__instance.m_nview, ZDOVars.s_startTime, FermenterMarker, requireStart: true, EmberSettings.Stations.ProtectMeadTimers);
        }

        [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.RPC_AddItem)), HarmonyPrefix]
        static void BeforeFermenterAdd(Fermenter __instance, out long __state) =>
            __state = __instance?.m_nview?.GetZDO()?.GetLong(ZDOVars.s_startTime, 0L) ?? 0L;

        [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.RPC_AddItem)), HarmonyPostfix]
        static void FermenterAdded(Fermenter __instance, long __state)
        {
            var view = __instance?.m_nview;
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            var zdo = view.GetZDO();
            var start = zdo?.GetLong(ZDOVars.s_startTime, 0L) ?? 0L;
            if (start > 0L && start != __state)
                zdo!.Set(FermenterMarker, SleepRuntime.TryStationBoostTotal(EmberSettings.Stations.ProtectMeadTimers, out var total) ? StationTimers.ToTicks(total) : -1L);
        }

        [HarmonyPatch(typeof(Plant), nameof(Plant.TimeSincePlanted)), HarmonyPrefix]
        static void Planted(Plant __instance)
        {
            if (__instance != null)
                ShiftStart(__instance.m_nview, ZDOVars.s_plantTime, PlantMarker, requireStart: false, EmberSettings.Stations.ProtectCropTimers);
        }

        static void PickableShift(Pickable pickable)
        {
            if (pickable == null || pickable.m_respawnTimeMinutes <= 0f || !pickable.GetPicked()) return;
            ShiftStart(pickable.m_nview, ZDOVars.s_pickedTime, PickableMarker, requireStart: false, EmberSettings.Stations.ProtectPickableTimers);
        }

        [HarmonyPatch(typeof(Pickable), nameof(Pickable.ShouldRespawn)), HarmonyPrefix]
        static void ShouldRespawn(Pickable __instance) => PickableShift(__instance);

        [HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText)), HarmonyPrefix]
        static void PickableHover(Pickable __instance) => PickableShift(__instance);

        /// <summary>A fresh pick starts its regrowth at the current total, so earlier boost is not taken off it.</summary>
        [HarmonyPatch(typeof(Pickable), nameof(Pickable.SetPicked)), HarmonyPostfix]
        static void Picked(Pickable __instance, bool picked)
        {
            if (!picked || __instance == null || __instance.m_respawnTimeMinutes <= 0f) return;
            var nview = __instance.m_nview;
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
            nview.GetZDO()?.Set(PickableMarker, SleepRuntime.TryStationBoostTotal(EmberSettings.Stations.ProtectPickableTimers, out var total) ? StationTimers.ToTicks(total) : -1L);
        }
    }
}
