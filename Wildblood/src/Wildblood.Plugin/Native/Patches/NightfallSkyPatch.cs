// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Adopted.Aspects;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The one place the game draws its weather and time of day, once per environment step on every machine with a
    /// screen, after it has decided them and before it judges whether it is day. While a Nightfall fight holds this
    /// client, the weather and the four light weights about to be drawn are blended toward the storm at midnight
    /// (<see cref="NightfallLook"/>), and the game's smoothed time of day is lent the blended time for this one call -
    /// it sets the sun's and the moon's angle - and given back by the finalizer whatever happens. So the game's day and
    /// night, its weather and everything that reads them (spawns, sleeping, the day count, rain on buildings) never
    /// change; only what is drawn does. With no Nightfall boss loaded and no night left to lift, it is one bool check.
    /// It runs inside the game's environment step, so a failure is reported and never rethrown.
    /// </summary>
    [HarmonyPatch(typeof(EnvMan), "SetEnv")]
    public static class NightfallSkyPatch
    {
        /// <summary>The game's own time of day while a blended one is lent; negative when nothing is lent.</summary>
        private struct Borrow
        {
            internal float Real, Shown;
            internal bool Held;
        }

        private static void Prefix(EnvMan __instance, ref EnvSetup env, ref float dayInt, ref float nightInt,
            ref float morningInt, ref float eveningInt, out Borrow __state)
        {
            __state = default;
            if (!Plugin.NativeRulesReady || !WildbloodModules.Active(WildbloodModule.Bosses) || NightfallBlend.Idle)
            {
                return;
            }
            try
            {
                float shown = NightfallBlend.Step(__instance);
                if (shown > 0f)
                {
                    float fraction = NightfallLook.Apply(__instance, shown, ref env, ref dayInt, ref nightInt,
                        ref morningInt, ref eveningInt);
                    __state = new Borrow { Real = __instance.m_smoothDayFraction, Shown = fraction, Held = true };
                    __instance.m_smoothDayFraction = fraction;
                }
            }
            catch (Exception e)
            {
                Guard.Report(e, "EnvMan.SetEnv nightfall");
            }
        }

        private static Exception? Finalizer(EnvMan __instance, Exception? __exception, Borrow __state)
        {
            if (__state.Held && __instance != null && __instance.m_smoothDayFraction == __state.Shown)
                __instance.m_smoothDayFraction = __state.Real;
            return __exception;
        }
    }

    /// <summary>
    /// The local player's weather check for Wet, Cold and Freezing, once per physics step. While Nightfall's storm is
    /// drawn here the check reads the storm's flags on top of the real weather (<see cref="NightfallChill"/>), lent for
    /// this call alone and given back by the finalizer whatever happens. Any other step is a single bool check; a
    /// failure is reported, never rethrown.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdateEnvStatusEffects")]
    public static class NightfallChillPatch
    {
        private static void Prefix(Player __instance, out bool __state)
        {
            __state = false;
            if (!Plugin.NativeRulesReady || !WildbloodModules.Active(WildbloodModule.Bosses) || !NightfallBlend.Storming || __instance != Player.m_localPlayer)
            {
                return;
            }
            try
            {
                __state = NightfallChill.Begin();
            }
            catch (Exception e)
            {
                Guard.Report(e, "Player.UpdateEnvStatusEffects nightfall");
            }
        }

        private static Exception? Finalizer(Exception? __exception, bool __state)
        {
            if (__state) NightfallChill.End();
            return __exception;
        }
    }
}
