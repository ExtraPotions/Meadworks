// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT. Adapted from BetterSleepBruh 2.1.0 at
// fe2bb81866e58f3c4afeb832876cba6ebf1e12cd: BetterSleepBruh/Patches/WaterVolumePatches.cs and ShipPatches.cs. The MIT
// notice is retained in THIRD_PARTY_NOTICES/BetterSleepBruh-MIT.txt.
// Changed by expDARE, 2026: decisions live in Ember.Core OceanRules; the boost rate is Ember's own (the authority's value
// or the last state reply) and counts only while Ember's sleep is active; the calm factor is smoothed once per frame;
// each safeguard is a server setting; the wave clock advances once per frame (UpdateWaterTime runs from FixedUpdate,
// Update and LateUpdate); namespace Ember.
using ExtraPotions.Ember.Config;
using ExtraPotions.Ember.Sleep;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Ember.Runtime
{
    /// <summary>Calm waves, real-time wave clock and no spurious boat impacts while the night is accelerated.</summary>
    internal static class OceanPatches
    {
        static float calmFactor = 1f;
        static int calmFrame = -1;
        static int waveFrame = -1;

        /// <summary>The extra sleep rate in effect on this peer; zero when Ember's sleep is not running.</summary>
        static double Rate => SleepRuntime.OceanRate;

        [HarmonyPatch(typeof(WaterVolume), "UpdateWaterTime")]
        static class WaveClock
        {
            static bool Prefix()
            {
                if (!OceanRules.DecoupleWaves(Plugin.Ready && Plugin.Settings != null && Plugin.Settings.Get(EmberSettings.Ocean.DecoupleWaterWaveSpeed), Rate)) return true;
                var step = OceanRules.WaveClockStep(Time.inFixedTimeStep, Time.frameCount, ref waveFrame, Time.deltaTime);
                WaterVolume.s_wrappedDayTimeSeconds = (WaterVolume.s_wrappedDayTimeSeconds + step) % 86400f;
                WaterVolume.s_waterTime += step;
                return false;
            }
        }

        [HarmonyPatch(typeof(WaterVolume), nameof(WaterVolume.GetWaterSurface))]
        static class WaveHeight
        {
            static void Prefix(ref float waveFactor)
            {
                if (!Plugin.Ready || Plugin.Settings == null) return;
                if (!Plugin.Settings.Get(EmberSettings.Ocean.CalmOceanDuringSleep) && Mathf.Approximately(calmFactor, 1f)) return;
                if (calmFrame != Time.frameCount)
                {
                    calmFrame = Time.frameCount;
                    var target = OceanRules.CalmTarget(Plugin.Settings.Get(EmberSettings.Ocean.CalmOceanDuringSleep), Rate,
                        Plugin.Settings.Get(EmberSettings.Ocean.OceanCalmMultiplier));
                    calmFactor = OceanRules.Approach(calmFactor, target, Time.deltaTime);
                }
                waveFactor *= calmFactor;
            }
        }

        [HarmonyPatch(typeof(Ship), "UpdateWaterForce")]
        static class BoatImpacts
        {
            static bool Prefix(Ship __instance, float depth, float time)
            {
                if (!Plugin.Ready || Plugin.Settings == null) return true;
                if (!OceanRules.SkipShipWaterForce(Plugin.Settings.Get(EmberSettings.Ocean.PreventBoatSleepImpactDamage),
                        Plugin.Settings.Get(EmberSettings.Ocean.SuppressBoatImpactScreenShake), Rate)) return true;
                __instance.m_lastDepth = depth;
                __instance.m_lastUpdateWaterForceTime = time;
                return false;
            }
        }
    }
}
