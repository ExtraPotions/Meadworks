#nullable disable
// OpenKeep BuildCamera. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/BuildCamera/CameraState.cs.
// Changed by expDARE, 2026: Timber namespace, Crucible authority/input/localization/lifecycle; kit pickup UI deferred.
using UnityEngine;

namespace Timber.Camera
{
    /// <summary>
    /// The detached camera of the local player: whether it is out, where it is and where it looks. Only this client
    /// knows it; nothing is written to a ZDO or sent. It counts as out only while the player who brought it out is
    /// still the local player, so a logout or a new character ends it without a clean-up call.
    /// </summary>
    public static class CameraState
    {
        private const float MaxPitch = 89f;

        private static Player owner;

        public static Vector3 Position { get; set; }
        public static float Yaw { get; private set; }
        public static float Pitch { get; private set; }

        public static bool Active => CameraModule.Allowed && owner != null && owner == Player.m_localPlayer;

        public static Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, 0f);

        /// <summary>The camera is out and this is the player it belongs to.</summary>
        public static bool IsOut(Player player) => Active && player == owner;

        /// <summary>Starts where the game camera is, looking where it looks.</summary>
        public static void Enter(Player player, Transform from)
        {
            owner = player;
            Position = from.position;
            Vector3 angles = from.rotation.eulerAngles;
            Yaw = angles.y;
            Pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), -MaxPitch, MaxPitch);
        }

        public static void Exit()
        {
            owner = null;
            CameraZoom.Restore();
            SE_DemisterCameraPatch.Cleanup();
            CameraCollision.Reset();
        }

        /// <summary>The game's look input in degrees (mouse or right stick, its sensitivity and inversion applied).</summary>
        public static void Look(Vector2 delta)
        {
            Yaw += delta.x;
            Pitch = Mathf.Clamp(Pitch - delta.y, -MaxPitch, MaxPitch);
        }
    }
}
