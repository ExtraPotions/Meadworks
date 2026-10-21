#nullable disable
// OpenKeep BuildCamera. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/BuildCamera/CameraViewPatches.cs.
// Changed by expDARE, 2026: Timber namespace, Crucible authority/input/localization/lifecycle; kit pickup UI deferred.
using HarmonyLib;
using UnityEngine;

namespace Timber.Camera
{
    /// <summary>
    /// The game camera shows the build camera. <c>GameCamera.GetCameraPosition</c> (private, called from its LateUpdate
    /// for a living player without a seat camera) moves the camera one step and returns its pose instead of the
    /// third-person one, with the near clip plane the game uses close to walls.
    /// </summary>
    [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.GetCameraPosition))]
    public static class CameraViewPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(GameCamera __instance, float dt, ref Vector3 pos, ref Quaternion rot)
        {
            if (!CameraState.Active)
                return true;
            CameraMotion.Step(Player.m_localPlayer, dt);
            pos = CameraState.Position;
            rot = CameraState.Rotation;
            __instance.m_camera.nearClipPlane = __instance.m_nearClipPlaneMin;
            return false;
        }
    }

    /// <summary>
    /// The sound listener stays on the camera while it is out (<c>GameCamera.UpdateListner</c>, private, puts it on the
    /// player's eyes otherwise), so you hear what you see: the hammer, the pieces, the fire in the house you build.
    /// </summary>
    [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateListner))]
    public static class CameraListenerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameCamera __instance)
        {
            if (CameraState.Active && __instance.m_listner != null)
                __instance.m_listner.transform.localPosition = Vector3.zero;
        }
    }

    /// <summary>
    /// The game fades lights (<c>LightLod</c>) by their distance to the player, or to the camera in its own free fly
    /// mode; while the build camera is out it counts from the camera too (<c>LightLod.GetLightReferencePoint</c>, private
    /// static), so torches by the camera stay lit when the player stands far off.
    /// </summary>
    [HarmonyPatch(typeof(LightLod), nameof(LightLod.GetLightReferencePoint))]
    public static class CameraLightLodPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref Vector3 __result)
        {
            if (!CameraState.Active)
                return true;
            __result = CameraState.Position;
            return false;
        }
    }

    /// <summary>After the game camera's frame: the head light's copy follows the camera's state.</summary>
    [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.LateUpdate))]
    public static class CameraFramePatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameCamera __instance)
        {
            CircletLight.Update(__instance);
        }
    }
}
