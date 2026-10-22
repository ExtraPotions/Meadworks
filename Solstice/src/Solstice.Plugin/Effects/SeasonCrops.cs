// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: winter freeze for ripe crops; failed survival drops their normal harvest once.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Effects;
using ExtraPotions.Solstice.Localization;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// Controlled pickables in a winter that stops growth. Only the owner writes or drops the harvest. Main thread only.
    /// </summary>
    public static class SeasonCrops
    {
        static readonly int FreezeKey = "solstice_freeze".GetStableHashCode();
        static readonly int RealFreezeKey = "solstice_freeze_remaining".GetStableHashCode();
        static readonly int WindowKey = "solstice_freeze_window".GetStableHashCode();
        static readonly int SurvivedKey = "solstice_survived".GetStableHashCode();
        static ConditionalWeakTable<Pickable, CropFreezeTimer> timers = new ConditionalWeakTable<Pickable, CropFreezeTimer>();
        static readonly HashSet<CropWatch> watches = new HashSet<CropWatch>();

        [HarmonyPatch(typeof(Pickable), "Awake")]
        static class Awake
        {
            static void Postfix(Pickable __instance)
            {
                try
                {
                    var view = __instance.m_nview;
                    if (view == null || !view.IsValid() || !Controlled(NameOf(__instance))) return;
                    if (__instance.GetComponent<CropWatch>() == null)
                    {
                        var watch = __instance.gameObject.AddComponent<CropWatch>();
                        watch.Crop = __instance;
                        watches.Add(watch);
                        watch.InvokeRepeating(nameof(CropWatch.Check), UnityEngine.Random.Range(1f, 5f), 1f);
                    }
                }
                catch (Exception error)
                {
                    SolsticeSeasons.LogWarning("Crop watch: " + error.GetType().Name);
                }
            }
        }

        [HarmonyPatch(typeof(Pickable), nameof(Pickable.UpdateRespawn))]
        static class Respawn
        {
            static bool Prefix(Pickable __instance)
            {
                try
                {
                    Check(__instance);
                    if (!TryDecide(__instance, out var decision, out _, out _)) return true;
                    return decision.Action != CropWinterAction.BeginFreeze && decision.Action != CropWinterAction.StayFreezing
                        && decision.Action != CropWinterAction.Survive && decision.Action != CropWinterAction.Perish;
                }
                catch (Exception error)
                {
                    SolsticeSeasons.LogWarning("Crop winter: " + error.GetType().Name);
                    return true;
                }
            }
        }

        public sealed class CropWatch : MonoBehaviour
        {
            public Pickable Crop = null!;
            public void Check() { if (Crop != null) SeasonCrops.Check(Crop); }
            void OnDestroy() => watches.Remove(this);
        }

        public static void Shutdown()
        {
            foreach (var watch in watches)
            {
                if (watch == null) continue;
                watch.CancelInvoke();
                UnityEngine.Object.Destroy(watch);
            }
            watches.Clear();
            timers = new ConditionalWeakTable<Pickable, CropFreezeTimer>();
        }

        static void Check(Pickable crop)
        {
            try
            {
                var view = crop.m_nview;
                if (view == null || !view.IsValid() || view.GetZDO() == null) return;
                var timer = timers.GetOrCreateValue(crop);
                var zdo = view.GetZDO();
                long budget = zdo.GetLong(RealFreezeKey, 0L);
                bool owner = view.IsOwner();
                bool running = SolsticeSeasons.Effects != null && Controlled(NameOf(crop));
                if (owner && running && budget > 0L)
                {
                    long window = FreezeMilliseconds();
                    long previous = zdo.GetLong(WindowKey, window);
                    if (previous != window)
                    {
                        budget = Math.Max(1L, Math.Min(window + 1L, budget + window - previous));
                        zdo.Set(RealFreezeKey, budget);
                        zdo.Set(WindowKey, window);
                    }
                }
                double now = Time.realtimeSinceStartupAsDouble;
                long next = timer.Advance(budget, owner && running, now);
                if (owner && budget != next) zdo.Set(RealFreezeKey, next);
                if (TryDecide(crop, out var decision, out _, out _) && owner) Apply(crop, view, decision);
                timer.Advance(zdo.GetLong(RealFreezeKey, 0L), owner && running, now);
            }
            catch (Exception error) { SolsticeSeasons.LogWarning("Crop winter: " + error.GetType().Name); }
        }

        [HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText))]
        static class Hover
        {
            static void Postfix(Pickable __instance, ref string __result)
            {
                try
                {
                    if (!TryDecide(__instance, out var decision, out _, out _)) return;
                    var line = Line(decision);
                    if (line.Length == 0) return;
                    __result = string.IsNullOrEmpty(__result) ? line : __result + "\n" + line;
                }
                catch (Exception error)
                {
                    SolsticeSeasons.LogWarning("Crop hover: " + error.GetType().Name);
                }
            }
        }

        static bool TryDecide(Pickable pickable, out CropWinterDecision decision, out ZNetView view, out bool owner)
        {
            decision = default;
            owner = false;
            view = pickable.m_nview;
            var clock = SolsticeSeasons.Clock;
            var effects = SolsticeSeasons.Effects;
            if (clock == null || effects == null || view == null || !view.IsValid() || view.GetZDO() == null) return false;
            if (!Controlled(NameOf(pickable))) return false;
            owner = view.IsOwner();
            var zdo = view.GetZDO();
            long start = zdo.GetLong(RealFreezeKey, 0L);
            int saved = zdo.GetInt(SurvivedKey, 0);
            bool survived = CropWinter.SurvivedWinter(saved, clock.Value.AnchorDay, clock.Value.DaysIn(Season.Winter));
            double remaining = start > 0 ? (start - 1L) / 1000.0 : 0d;
            var plant = pickable.GetComponent<Plant>();
            bool hardy = SolsticeSeasons.HardyCrops.Survives(plant != null && plant.m_tolerateCold, new[] { NameOf(pickable) });
            bool winter = SolsticeSeasons.Current == Season.Winter && effects.PlantsGrowthMultiplier <= 0f;
            decision = CropWinter.Decide(
                SolsticeSeasons.CropsPerish, winter, pickable.CanBePicked(), pickable.m_picked, true,
                SeasonProtection.IsProtected(pickable.transform.position), hardy,
                SeasonProtection.ProtectedWithHeat(pickable.transform.position), survived,
                SolsticeSeasons.View.DayInSeason, SolsticeSeasons.PerishFromDay, SolsticeSeasons.FreezeSeconds, SolsticeSeasons.HarvestChance,
                start > 0, remaining, CropWinter.Roll(IdOf(zdo), clock.Value.WorldDay));
            return true;
        }

        static void Apply(Pickable pickable, ZNetView view, CropWinterDecision decision)
        {
            var zdo = view.GetZDO();
            var clock = SolsticeSeasons.Clock;
            switch (decision.Action)
            {
                case CropWinterAction.BeginFreeze:
                    if (zdo.GetLong(RealFreezeKey, 0L) == 0L)
                        zdo.Set(RealFreezeKey, FreezeMilliseconds() + 1L);
                    zdo.Set(WindowKey, FreezeMilliseconds());
                    zdo.Set(FreezeKey, 0L);
                    break;
                case CropWinterAction.Survive:
                    if (clock.HasValue) zdo.Set(SurvivedKey, clock.Value.WorldDay);
                    zdo.Set(RealFreezeKey, 0L);
                    zdo.Set(FreezeKey, 0L);
                    break;
                case CropWinterAction.Perish:
                    zdo.Set(RealFreezeKey, 0L);
                    zdo.Set(FreezeKey, 0L);
                    Harvest(pickable, view);
                    break;
                case CropWinterAction.ClearFreeze:
                    if (zdo.GetLong(RealFreezeKey, 0L) != 0L) zdo.Set(RealFreezeKey, 0L);
                    if (zdo.GetLong(FreezeKey, 0L) != 0L) zdo.Set(FreezeKey, 0L);
                    break;
            }
        }

        static void Harvest(Pickable pickable, ZNetView view)
        {
            if (!view.IsOwner() || pickable.m_picked || pickable.m_itemPrefab == null) return;
            // Mark it picked before Drop. A throw must not leave the crop free to harvest again.
            // RPC_Pick dereferences the local player on the dedicated-server baseline.
            pickable.m_picked = true;
            view.InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", true);
            int amount = pickable.m_dontScale ? pickable.m_amount
                : Math.Max(pickable.m_minAmountScaled, Game.instance.ScaleDrops(pickable.m_itemPrefab, pickable.m_amount));
            int offset = 0;
            for (int i = 0; i < amount; i++) pickable.Drop(pickable.m_itemPrefab, offset++, 1);
            if (!pickable.m_extraDrops.IsEmpty())
                foreach (var item in pickable.m_extraDrops.GetDropListItems()) pickable.Drop(item.m_dropPrefab, offset++, item.m_stack);
        }

        static string Line(CropWinterDecision decision)
        {
            switch (decision.Hover)
            {
                case CropHover.FrostResistant: return SolsticeText.Text(SolsticeTextKeys.PlantFrostResistant);
                case CropHover.HeatProtected: return SolsticeText.Text(SolsticeTextKeys.PlantHeatProtected);
                case CropHover.Survived: return SolsticeText.Text(SolsticeTextKeys.PlantSurvivedWinter);
                case CropHover.Freezing: return SolsticeText.Format(SolsticeTextKeys.PlantFreezing, decision.HoverSeconds);
                case CropHover.WillPerish: return SolsticeText.Text(SolsticeTextKeys.PlantWillPerish);
                case CropHover.Exposed: return SolsticeText.Text(SolsticeTextKeys.PlantExposed);
                default: return "";
            }
        }

        static bool Controlled(string name) => SolsticeSeasons.ControlledCrops.Survives(false, new[] { name });

        static long FreezeMilliseconds() => (long)Math.Ceiling(CropWinter.FreezeWindow(SolsticeSeasons.FreezeSeconds) * 1000d);

        static string NameOf(Component component) => CropWinter.PrefabName(component != null ? component.name : null);

        static long IdOf(ZDO zdo)
        {
            var id = zdo.m_uid;
            return id.UserID ^ ((long)id.ID << 17);
        }

    }
}
