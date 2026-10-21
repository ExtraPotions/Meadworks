#nullable disable
// OpenKeep BuildCamera. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/BuildCamera/CameraReachPatches.cs.
// Changed by expDARE, 2026: Timber namespace, Crucible authority/input/localization/lifecycle; kit pickup UI deferred.
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Timber.Camera
{
    /// <summary>
    /// The placement ray (<c>Player.PieceRayTest</c>, private, used by the placement ghost of every build tool): the
    /// game's mask for the selected piece (with water for water-aware pieces) and its extra placement distance.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.PieceRayTest))]
    public static class CameraPlaceReachPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Player __instance, bool water, out CameraReach.Scope __state)
        {
            int mask = water ? __instance.m_placeWaterRayMask : __instance.m_placeRayMask;
            __state = CameraReach.Open(__instance, mask, ExtraDistance(__instance));
        }

        [HarmonyFinalizer]
        public static void Finalizer(Player __instance, CameraReach.Scope __state) => CameraReach.Close(__instance, __state);

        private static float ExtraDistance(Player player)
        {
            Piece piece = player.m_placementGhost != null ? player.m_placementGhost.GetComponent<Piece>() : null;
            return piece != null ? piece.m_extraPlacementDistance : 0f;
        }
    }

    /// <summary>
    /// The remove, copy and repair-hover rays (<c>Player.RemovePiece</c>, <c>Player.CopyPiece</c>,
    /// <c>Player.UpdateWearNTearHover</c>, all private): the game's remove mask, no extra distance.
    /// </summary>
    [HarmonyPatch]
    public static class CameraRemoveReachPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Player), nameof(Player.RemovePiece));
            yield return AccessTools.Method(typeof(Player), nameof(Player.CopyPiece));
            yield return AccessTools.Method(typeof(Player), nameof(Player.UpdateWearNTearHover));
        }

        [HarmonyPrefix]
        public static void Prefix(Player __instance, out CameraReach.Scope __state) =>
            __state = CameraReach.Open(__instance, __instance.m_removeRayMask, 0f);

        [HarmonyFinalizer]
        public static void Finalizer(Player __instance, CameraReach.Scope __state) => CameraReach.Close(__instance, __state);
    }
}
