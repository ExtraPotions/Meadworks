#nullable disable
// OpenKeep BuildCamera. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/BuildCamera/CameraReach.cs.
// Changed by expDARE, 2026: Timber namespace, Crucible authority/input/localization/lifecycle; kit pickup UI deferred.
using UnityEngine;

namespace Timber.Camera
{
    /// <summary>
    /// The game's build rays start at the camera but measure reach from the player's eyes (<c>m_eye</c> against
    /// <c>m_maxPlaceDistance</c>), which a camera far from the player never meets. While the camera is out, each of those
    /// methods first casts the same ray itself (origin, direction, mask), extending its native 50 m length when needed: when the hit lies within the player's own
    /// place distance plus Extra Reach of the camera, the place distance is lifted for that one call so the game's
    /// check passes; otherwise it is made negative so the game finds nothing. The value the game (or another mod) had
    /// is put back by a finalizer right after the call, so nothing else ever sees the change.
    /// </summary>
    public static class CameraReach
    {
        private const float GameRayLength = 50f;
        private const float Unbounded = 100000f;
        private static float rayLength = GameRayLength;

        public readonly struct Scope
        {
            internal readonly bool Changed;
            internal readonly float Distance, RayLength;
            internal Scope(float distance, float length) { Changed = true; Distance = distance; RayLength = length; }
        }

        /// <summary>Captures the native distance and current ray scope; an empty scope changes nothing.</summary>
        public static Scope Open(Player player, int mask, float extra)
        {
            if (!CameraState.IsOut(player) || GameCamera.instance == null)
                return default;
            float saved = player.m_maxPlaceDistance;
            float reach = saved + extra + CameraSettings.ExtraReach.Value;
            var scope = new Scope(saved, rayLength);
            rayLength = Mathf.Max(GameRayLength, reach);
            try
            {
                player.m_maxPlaceDistance = InReach(mask, reach) ? Unbounded : -Unbounded;
                return scope;
            }
            catch { rayLength = scope.RayLength; throw; }
        }

        public static void Close(Player player, Scope saved)
        {
            if (saved.Changed)
            {
                player.m_maxPlaceDistance = saved.Distance;
                rayLength = saved.RayLength;
            }
        }

        // Called only at the four native build/remove rays; their remaining checks run unchanged.
        internal static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float distance, int mask, Player player) =>
            Physics.Raycast(origin, direction, out hit, CameraState.IsOut(player) ? Mathf.Max(distance, rayLength) : distance, mask);

        private static bool InReach(int mask, float reach)
        {
            Transform view = GameCamera.instance.transform;
            return Physics.Raycast(view.position, view.forward, out RaycastHit hit, rayLength, mask) && hit.distance < reach;
        }
    }
}
