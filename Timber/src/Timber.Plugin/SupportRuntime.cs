// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original C02/owner/persistence adapter over the selectively adopted support-wear setter.
using System;
using ExtraPotions.Timber.Adopted;
using HarmonyLib;
using Timber.Flooring;

namespace ExtraPotions.Timber.Runtime
{
    public static class SupportRuntime
    {
        [ThreadStatic] static bool placingProtected;
        static readonly int ProtectedHash = TimberCore.ProtectedPieceKey.GetStableHashCode();
        public static void Reset() => placingProtected = false;

        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        static class Placement
        {
            [HarmonyPrefix]
            static void Prefix(Player __instance, out bool __state)
            {
                __state = placingProtected;
                placingProtected = __instance == Player.m_localPlayer && Plugin.IgnoreSupport;
            }
            [HarmonyFinalizer]
            static Exception? Finalizer(bool __state, Exception? __exception)
            { placingProtected = __state; return __exception; }
        }

        [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
        static class Created
        {
            [HarmonyPostfix]
            static void Postfix(Piece __instance, long uid)
            {
                var player = Player.m_localPlayer;
                var view = __instance.m_nview;
                if (!placingProtected || !Plugin.Active || player == null || player.GetPlayerID() != uid ||
                    __instance.GetCreator() != uid || __instance.GetComponent<WearNTear>() == null ||
                    view == null || !view.IsValid() || !view.IsOwner()) return;
                view.GetZDO().Set(ProtectedHash, true);
            }
        }

        public struct WearState { public bool Applied; public bool Original; }
        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.UpdateWear))]
        static class Wear
        {
            [HarmonyPrefix]
            static void Prefix(WearNTear __instance, out WearState __state)
            {
                __state = default;
                var view = __instance.m_nview;
                if (!Plugin.Active || view == null || !view.IsValid() || !view.IsOwner()) return;
                bool protectedPiece = view.GetZDO().GetBool(ProtectedHash);
                bool floorSmelter = FiresRuntime.SmeltersActive && __instance.GetComponent<Smelter>() != null;
                if (!protectedPiece && !floorSmelter) return;
                // Current Valheim runs its collapse branch when this inversely named field is true.
                __state.Original = SupportWearKernel.Swap(ref __instance.m_noSupportWear, false);
                __state.Applied = true;
            }
            [HarmonyFinalizer]
            static Exception? Finalizer(WearNTear __instance, WearState __state, Exception? __exception)
            {
                if (__state.Applied) SupportWearKernel.Restore(ref __instance.m_noSupportWear, __state.Original);
                return __exception;
            }
        }
    }
}
