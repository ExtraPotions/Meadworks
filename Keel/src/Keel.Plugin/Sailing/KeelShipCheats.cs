// Keel - ExtraPotions Meadworks ships and sailing. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Meadworks-original. Valheim_Admin_Tool's ship cheats are a behavior-only reference (no license; nothing copied).
// Native Ship.CustomFixedUpdate / Ship.GetSailForce and EnvMan's wind read in Valheim's own assembly.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Sailing;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// Tailwind and Fast Sailing act only on the ship the local player steers, and only while the cheat is allowed for
    /// this player. Tailwind turns the wind for that ship's own update alone (sail, rudder and force) and puts the
    /// game's wind back right after, so the world's wind, Wind Call and other ships are unchanged. Fast Sailing raises the
    /// sail force the game computes on the machine that moves the ship.
    /// </summary>
    internal static class KeelShipCheats
    {
        static bool On(KeelSetting<bool> cheat) => Plugin.Settings != null && Plugin.Settings.Get(cheat);
        static bool Steering(Ship ship) => Player.m_localPlayer != null && ship != null && SailingPerks.Helmsman(ship) == Player.m_localPlayer;

        [HarmonyPatch(typeof(Ship), nameof(Ship.CustomFixedUpdate))]
        static class Wind
        {
            [HarmonyPrefix]
            static void Prefix(Ship __instance, out Vector4? __state)
            {
                __state = null;
                var env = EnvMan.instance;
                if (env == null || !Steering(__instance) || !On(KeelSettings.Cheats.Tailwind)) return;
                var forward = __instance.transform.forward;
                if (!ShipCheats.HasTailwind(forward.x, forward.z)) return;
                var (x, z) = ShipCheats.Tailwind(forward.x, forward.z);
                __state = env.m_wind;
                env.m_wind = new Vector4(x, 0f, z, env.m_wind.w);
            }

            [HarmonyFinalizer]
            static void Finalizer(Vector4? __state)
            {
                if (__state.HasValue && EnvMan.instance != null) EnvMan.instance.m_wind = __state.Value;
            }
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.GetSailForce))]
        static class Speed
        {
            [HarmonyPostfix]
            static void Postfix(Ship __instance, ref Vector3 __result)
            {
                if (Steering(__instance) && On(KeelSettings.Cheats.FastSailing)) __result *= ShipCheats.SailForceFactor(true);
            }
        }
    }
}
