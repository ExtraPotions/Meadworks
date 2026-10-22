// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. A new world destroys the HUD and the player. Statics that still point at them are dropped here.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Solstice.Runtime
{
    static class SeasonScene
    {
        public static void Entered()
        {
            SeasonIndicator.Shutdown();
            SeasonHeatHud.Shutdown();
            SeasonStatus.ReleaseLive();
            SeasonMarks.ReleaseDestroyed();
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        static class World
        {
            static void Postfix() => Entered();
        }

        [HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
        static class HudAwake
        {
            static void Postfix() => Entered();
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Awake))]
        static class PlayerAwake
        {
            static void Postfix(Player __instance)
            {
                if (__instance == null) return;
                if (Player.m_localPlayer != null && Player.m_localPlayer != __instance) return;
                SeasonStatus.ReleaseLive();
            }
        }
    }
}
