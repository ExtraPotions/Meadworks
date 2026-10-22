// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from Seasons 1.10.6 by shudnal (https://github.com/shudnal/Seasons), commit 4054af6e8823d787921d673607e4942554872b66,
// Unlicense (public domain), SeasonState/SeasonStatePatches.cs and SeasonState/EnvManPatches.cs. See THIRD_PARTY_NOTICES/Seasons-Unlicense.txt.
// Changed by expDARE, 2026: the patch bodies on the game's own methods are kept (plant growth history, winter cold for plants, beehive, food and
// stamina drain, fire fuel, sap, rested duration, wind, rain protection); they read Solstice's server-authoritative effects instead of the
// reference's season state, run only while Solstice is active, and skip places the seasons do not reach (SeasonProtection).
// Not ported here: torch warmth, night length, procreation, wood and meat drops (other Meadworks products provide them; the facts
// contract publishes the numbers), bush respawn, textures, snow meshes, ice floes and the full summer-heat meter.
// Pickable perish is SeasonCrops. The season status and warm-clothes overheat are SeasonStatus. Plant growth uses the saved clock.
using System;
using System.Linq;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Effects;
using HarmonyLib;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// The seasonal effects on the game's own systems. Every patch reads <see cref="SolsticeSeasons.Effects"/>, which is null whenever Solstice
    /// is off for the session, stepped aside or switched off by the server, and then does nothing. All patches belong to the one Harmony
    /// instance of the plugin and are removed with it.
    /// </summary>
    public static class SeasonPatches
    {
        /// <summary>The effects in force, or null.</summary>
        static SeasonEffects? Now => SolsticeSeasons.Effects;

        static bool Sheltered(Player player) => player.InInterior() || player.InShelter();

        static double ScaleElapsed(double elapsed, Func<SeasonEffects, float> rate)
        {
            if (Now == null || ZNet.instance == null || EnvMan.instance == null) return elapsed;
            var clock = SolsticeSeasons.Clock;
            Func<Season, double> multiplier = season => Math.Max(0f, SolsticeSeasons.EffectsOf(season) is SeasonEffects effects ? rate(effects) : 1f);
            if (clock.HasValue)
                return SeasonTime.ScaleAt(elapsed, ZNet.instance.GetTimeSeconds(), EnvMan.instance.m_dayLengthSec, clock.Value.Calendar,
                    multiplier, SolsticeSeasons.Held ?? clock.Value.Season,
                    SolsticeSeasons.Held.HasValue ? SolsticeSeasons.View.DayInSeason : clock.Value.DayInSeason);
            return SeasonTime.Scale(elapsed, ZNet.instance.GetTimeSeconds(), EnvMan.instance.m_dayLengthSec, SolsticeSeasons.Calendar, multiplier);
        }

        [HarmonyPatch(typeof(Plant), nameof(Plant.TimeSincePlanted))]
        static class PlantTimeSincePlanted
        {
            /// <summary>Plant growth is the elapsed time weighted by the growth multiplier of every season the plant lived through.</summary>
            static void Postfix(Plant __instance, ref double __result)
            {
                if (Now == null || ZNet.instance == null || EnvMan.instance == null || SeasonProtection.IsProtected(__instance.transform.position)) return;
                __result = ScaleElapsed(__result, effects => effects.PlantsGrowthMultiplier);
            }
        }

        [HarmonyPatch(typeof(Plant), nameof(Plant.UpdateHealth))]
        static class PlantCold
        {
            /// <summary>In a winter that stops growth a plant that is not hardy and has no fire nearby is too cold; indoors it is left alone.</summary>
            static void Prefix(Plant __instance, ref double timeSincePlanted, out bool __state)
            {
                __state = Now == null || SeasonProtection.IsProtected(__instance.transform.position);
                if (__state) return;
                timeSincePlanted = ColdPlants.AdjustedAge(timeSincePlanted, SolsticeSeasons.Current ?? Season.Spring, Now!.PlantsGrowthMultiplier);
            }

            static void Postfix(Plant __instance, bool __state)
            {
                var effects = Now;
                if (__state || effects == null || SolsticeSeasons.Current == null) return;
                var grown = __instance.m_grownPrefabs == null ? Array.Empty<string>() : __instance.m_grownPrefabs.Where(g => g != null).Select(g => g.name).ToArray();
                if (ColdPlants.IsTooCold(__instance.m_status == Plant.Status.Healthy, SolsticeSeasons.Current.Value, effects.PlantsGrowthMultiplier,
                        SolsticeSeasons.HardyCrops.Survives(__instance.m_tolerateCold, grown), SeasonProtection.ProtectedWithHeat(__instance.transform.position)))
                    __instance.m_status = Plant.Status.TooCold;
            }
        }

        [HarmonyPatch(typeof(Beehive), nameof(Beehive.GetTimeSinceLastUpdate))]
        static class BeehiveTime
        {
            static void Postfix(Beehive __instance, ref float __result)
            {
                if (Now != null && !SeasonProtection.IsProtected(__instance.transform.position)) __result = (float)ScaleElapsed(__result, effects => effects.BeehiveProductionMultiplier);
            }
        }

        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetTimeSinceLastUpdate))]
        static class FireplaceTime
        {
            static void Postfix(Fireplace __instance, ref double __result)
            {
                if (Now != null && !SeasonProtection.IsProtected(__instance.transform.position)) __result = ScaleElapsed(__result, effects => effects.FireplaceDrainMultiplier);
            }
        }

        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.UpdateFuel))]
        static class CookingStationFuel
        {
            static void Prefix(CookingStation __instance, ref float dt)
            {
                if (Now != null && !SeasonProtection.IsProtected(__instance.transform.position)) dt *= Math.Max(0f, Now.FireplaceDrainMultiplier);
            }
        }

        [HarmonyPatch(typeof(SapCollector), nameof(SapCollector.GetTimeSinceLastUpdate))]
        static class SapTime
        {
            static void Postfix(SapCollector __instance, ref float __result)
            {
                if (Now != null && !SeasonProtection.IsProtected(__instance.transform.position)) __result = (float)ScaleElapsed(__result, effects => effects.SapCollectingSpeedMultiplier);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdateFood))]
        static class FoodDrain
        {
            /// <summary>Outdoors, each food tick takes the multiplier's share of a tick from the food's time left.</summary>
            static void Prefix(Player __instance, float dt, bool forceUpdate)
            {
                var effects = Now;
                if (effects == null || __instance == null || Math.Abs(effects.FoodDrainMultiplier - 1f) < 1e-6f) return;
                if (Sheltered(__instance)) return;
                if (!(dt * Game.m_foodRate + __instance.m_foodUpdateTimer >= 1f || forceUpdate)) return;
                foreach (var food in __instance.m_foods) food.m_time += 1f - Math.Max(0f, effects.FoodDrainMultiplier);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UseStamina))]
        static class StaminaDrain
        {
            static void Prefix(Player __instance, ref float v)
            {
                var effects = Now;
                if (effects == null || __instance == null || Sheltered(__instance)) return;
                v *= Math.Max(0f, effects.StaminaDrainMultiplier);
            }
        }

        [HarmonyPatch(typeof(SE_Rested), nameof(SE_Rested.UpdateTTL))]
        static class RestedDuration
        {
            static void Prefix(SE_Rested __instance, out Tuple<float, float>? __state)
            {
                __state = null;
                var effects = Now;
                if (effects == null || Math.Abs(effects.RestedBuffDurationMultiplier - 1f) < 1e-6f) return;
                __state = Tuple.Create(__instance.m_baseTTL, __instance.m_TTLPerComfortLevel);
                __instance.m_baseTTL *= effects.RestedBuffDurationMultiplier;
                __instance.m_TTLPerComfortLevel *= effects.RestedBuffDurationMultiplier;
            }

            static void Finalizer(SE_Rested __instance, Tuple<float, float>? __state)
            {
                if (__state == null) return;
                __instance.m_baseTTL = __state.Item1;
                __instance.m_TTLPerComfortLevel = __state.Item2;
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.GetWindForce))]
        static class WindForce
        {
            static void Postfix(ref UnityEngine.Vector3 __result)
            {
                if (Now != null) __result *= Now.WindIntensityMultiplier;
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.GetWindIntensity))]
        static class WindIntensity
        {
            static void Postfix(ref float __result)
            {
                if (Now != null) __result *= Now.WindIntensityMultiplier;
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.UpdateWear))]
        static class RainProtection
        {
            /// <summary>While the season protects from rain, pieces take no rain wear; the game's own flag is restored afterwards.</summary>
            static void Prefix(WearNTear __instance, out bool? __state)
            {
                __state = null;
                if (Now == null || !Now.RainProtection || __instance.m_nview == null || !__instance.m_nview.IsValid() || SeasonProtection.IsProtected(__instance.transform.position)) return;
                __state = __instance.m_noRoofWear;
                __instance.m_noRoofWear = false;
            }

            static void Finalizer(WearNTear __instance, bool? __state)
            {
                if (__state.HasValue) __instance.m_noRoofWear = __state.Value;
            }
        }
    }
}
