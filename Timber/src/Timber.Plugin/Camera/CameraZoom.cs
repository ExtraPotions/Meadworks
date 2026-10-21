// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native zoom ownership for the approved first-person compatibility requirement.
using HarmonyLib;
using UnityEngine;

namespace Timber.Camera
{
    [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateCamera))]
    internal static class CameraZoom
    {
        static GameCamera? owner;
        static float original, held;

        [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyBefore("Azumatt.FirstPersonMode")]
        internal static void Prefix(GameCamera __instance)
        {
            if (!CameraState.Active) { Restore(); return; }
            if (owner != __instance)
            {
                Restore();
                owner = __instance;
                original = __instance.m_distance;
            }
            else if (!Mathf.Approximately(__instance.m_distance, held)) original = __instance.m_distance;
            held = Mathf.Max(1f, __instance.m_minDistance);
            __instance.m_distance = held;
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        internal static void Postfix(GameCamera __instance)
        {
            if (CameraState.Active && owner == __instance) __instance.m_distance = held;
        }

        internal static void Restore()
        {
            if (owner != null && Mathf.Approximately(owner.m_distance, held)) owner.m_distance = original;
            owner = null;
        }
    }
}
