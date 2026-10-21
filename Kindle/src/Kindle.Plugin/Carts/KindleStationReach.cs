// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Kindle.Config;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Kindle.Runtime
{
    /// <summary>
    /// Crafting Stations / Use Distance Multiplier: the local player may use a crafting station from this many times the
    /// game's distance for it. The game checks that distance in one place (CraftingStation.InUseDistance) for the hover
    /// text, opening the menu and closing it when the player walks away, so one postfix covers all three. It only widens
    /// a refused check; prefabs and instance fields are never changed. The value is read on every check, so a local edit
    /// or the host's value takes effect at once. Reaching a station still needs it under the crosshair (the player's own
    /// interact reach).
    /// </summary>
    public static class KindleStationReach
    {
        static KindleConfig? settings;
        static Harmony? patches;

        public static void Initialize(KindleConfig config)
        {
            Shutdown(); settings = config;
            patches = new Harmony(Plugin.Guid + ".stationreach");
            patches.CreateClassProcessor(typeof(DistancePatch)).Patch();
        }

        public static void Shutdown()
        {
            patches?.UnpatchSelf(); patches = null; settings = null;
        }

        static float Multiplier => settings != null && Plugin.GameplayAvailable ? settings.Get(KindleSettings.Stations.UseDistance) : 1f;

        [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.InUseDistance))]
        static class DistancePatch
        {
            [HarmonyPostfix]
            static void Postfix(CraftingStation __instance, Humanoid human, ref bool __result)
            {
                if (__result || !__instance || !human || human != Player.m_localPlayer) return;
                try
                {
                    float multiplier = Multiplier;
                    if (multiplier > 1f)
                        __result = Vector3.Distance(human.transform.position, __instance.transform.position) < __instance.m_useDistance * multiplier;
                }
                catch (Exception error) { KindleSalvage.Warn("Station use distance: " + error.GetType().Name + ": " + error.Message); }
            }
        }
    }
}
