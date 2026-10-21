// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Valheim_Admin_Tool's Build Everywhere is a behavior-only reference (no license; nothing copied).
// Native Player.UpdatePlacementGhost / Player.RemovePiece and Location.IsInsideNoBuildLocation read in Valheim's own assembly.
using System;
using HarmonyLib;
namespace ExtraPotions.Timber.Runtime
{
    /// <summary>
    /// The Build Everywhere cheat: while the local player's placement ghost is updated or a piece is removed, the game's
    /// no-build-zone test answers "outside". Only those two native calls are covered; terrain hits and everything else
    /// still see the zone. Wards (PrivateArea), stations and the other placement checks are untouched.
    /// </summary>
    public static class BuildEverywhereRuntime
    {
        [ThreadStatic] static int depth;
        static bool Open(Player player)
        {
            if (player == null || player != Player.m_localPlayer || !Plugin.BuildEverywhere) return false;
            depth++; return true;
        }
        static void Close(bool opened) { if (opened && depth > 0) depth--; }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
        static class Ghost
        {
            static void Prefix(Player __instance, out bool __state) => __state = Open(__instance);
            static Exception? Finalizer(bool __state, Exception? __exception) { Close(__state); return __exception; }
        }
        [HarmonyPatch(typeof(Player), nameof(Player.RemovePiece))]
        static class Remove
        {
            static void Prefix(Player __instance, out bool __state) => __state = Open(__instance);
            static Exception? Finalizer(bool __state, Exception? __exception) { Close(__state); return __exception; }
        }
        [HarmonyPatch(typeof(Location), nameof(Location.IsInsideNoBuildLocation))]
        static class Zone
        {
            static bool Prefix(ref bool __result)
            {
                if (depth == 0) return true;
                __result = false; return false;
            }
        }
    }
}
