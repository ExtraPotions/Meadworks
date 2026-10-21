// Adapted from ValheimMods OpenKeep/OpenKeep/src/Homestead/HivePatch.cs,
// OpenKeep/OpenKeep/src/Homestead/HiveRate.cs and OpenKeep/OpenKeep/src/Homestead/HiveProgress.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: 3bb778330f23645ccddb42fbc0c6ffab72c842b6
// Changed by expDARE, 2026: scoped native-unit progress, authority/source lifecycle and conditional restoration.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx.Bootstrap;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Conversion;
using ExtraPotions.Shepherd.Yield;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    public static class HoneyProduction
    {
        public const string HarmonyId = Plugin.Guid + ".honey_production";
        static Harmony? harmony;
        static bool installed;
        static bool? sourceReported;
        public static bool SourcePresent => Chainloader.PluginInfos.ContainsKey(SourceModules.OpenKeep);
        public static bool Active => harmony != null && Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out _)
            && ShepherdSettings.Enabled.Value && HusbandrySettings.Enabled.Value && !SourcePresent;

        public static void Initialize() { Shutdown(); harmony = new Harmony(HarmonyId); }

        public static void Tick()
        {
            if (harmony == null) return;
            bool source = SourcePresent;
            if (sourceReported != source)
            {
                sourceReported = source;
                if (source) MeadworksStepAside.ReportModule(Plugin.Guid, "honey_production", new SourceMod(SourceModules.OpenKeep, "OpenKeep"));
                else MeadworksStepAside.WithdrawModule(Plugin.Guid, "honey_production");
            }
            float fixedRate = HusbandryYieldSettings.HoneyPerDay.Value;
            bool wanted = Active && (HusbandryYieldSettings.HoneyPerPlayerOnline.Value
                || (HoneyProductionMath.Finite(fixedRate) && fixedRate > 0f));
            if (wanted == installed) return;
            if (wanted) harmony.CreateClassProcessor(typeof(HoneyProductionPatch)).Patch();
            else harmony.UnpatchSelf();
            installed = wanted;
        }

        public static float SecondsPerHoney() => EnvMan.instance == null ? 0f : HoneyProductionMath.SecondsPerHoney(
            HusbandryYieldSettings.HoneyPerDay.Value, HusbandryYieldSettings.HoneyPerPlayerOnline.Value,
            ZNet.instance != null ? ZNet.instance.GetNrOfPlayers() : 1, EnvMan.instance.m_dayLengthSec);

        public static void Shutdown()
        {
            harmony?.UnpatchSelf(); harmony = null; installed = false; sourceReported = null;
            if (ExtraPotions.Meadworks.Crucible.Plugin.IsReady)
                MeadworksStepAside.WithdrawModule(Plugin.Guid, "honey_production");
        }
    }

    [HarmonyPatch(typeof(Beehive), nameof(Beehive.UpdateBees))]
    public static class HoneyProductionPatch
    {
        public sealed class TickState
        {
            public ZNetView View = null!;
            public ZDO Zdo = null!;
            public float NativeSeconds, AppliedSeconds, OriginalProduct, AppliedProduct;
            public long LastTime;
        }

        [HarmonyPrefix]
        public static void Prefix(Beehive __instance, out TickState? __state)
        {
            __state = null;
            if (!HoneyProduction.Active || !__instance || !__instance.m_honeyItem
                || Utils.GetPrefabName(__instance.m_honeyItem.gameObject) != "Honey") return;
            var view = __instance.GetComponent<ZNetView>();
            if (!view || !view.IsValid() || !view.IsOwner() || !ZNetScene.instance || !ZNet.instance) return;
            var zdo = view.GetZDO();
            var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
            var original = prefab ? prefab.GetComponent<Beehive>() : null;
            float native = original ? original.m_secPerUnit : 0f;
            float seconds = HoneyProduction.SecondsPerHoney();
            if (!HoneyProductionMath.Finite(native) || native <= 0f || seconds <= 0f
                || __instance.m_secPerUnit != native) return;
            float product = zdo.GetFloat(ZDOVars.s_product);
            long lastTime = zdo.GetLong(ZDOVars.s_lastTime, 0L), now = ZNet.instance.GetTime().Ticks;
            if (lastTime <= 0 || now < lastTime || !HoneyProductionMath.TryRescale(product, native, seconds, out float applied)
                || !HoneyProductionMath.TryNativeRemainder(applied, (float)new TimeSpan(now - lastTime).TotalSeconds, seconds, out _, out _)) return;
            __state = new TickState { View = view, Zdo = zdo, NativeSeconds = native, AppliedSeconds = seconds,
                OriginalProduct = product, AppliedProduct = applied, LastTime = lastTime };
            __instance.m_secPerUnit = seconds;
            zdo.Set(ZDOVars.s_product, applied);
        }

        [HarmonyFinalizer]
        public static void Finalizer(Beehive __instance, TickState? __state, Exception? __exception)
        {
            if (__state == null || !__instance) return;
            try
            {
                if (!__state.View || !__state.View.IsValid() || !__state.View.IsOwner()
                    || !ReferenceEquals(__state.View.GetZDO(), __state.Zdo)) return;
                var zdo = __state.Zdo;
                float product = zdo.GetFloat(ZDOVars.s_product);
                long after = zdo.GetLong(ZDOVars.s_lastTime, 0L);
                // Restore an unchanged temporary before-image even if a later field writer or prefix threw.
                if (product == __state.AppliedProduct && after == __state.LastTime)
                {
                    zdo.Set(ZDOVars.s_product, __state.OriginalProduct);
                    return;
                }
                if (__instance.m_secPerUnit != __state.AppliedSeconds || after < __state.LastTime) return;
                float elapsed = (float)new TimeSpan(after - __state.LastTime).TotalSeconds;
                if (!HoneyProductionMath.TryNativeRemainder(__state.AppliedProduct, elapsed, __state.AppliedSeconds,
                    out float total, out float rest)) return;
                float expected = total > __state.AppliedSeconds ? 0f : total;
                if (product == expected && HoneyProductionMath.TryRescale(rest, __state.AppliedSeconds, __state.NativeSeconds, out float nativeProduct))
                    zdo.Set(ZDOVars.s_product, nativeProduct);
                else if (__exception != null && product == __state.AppliedProduct)
                    zdo.Set(ZDOVars.s_product, __state.OriginalProduct);
            }
            finally
            {
                if (__instance.m_secPerUnit == __state.AppliedSeconds) __instance.m_secPerUnit = __state.NativeSeconds;
            }
        }
    }
}
