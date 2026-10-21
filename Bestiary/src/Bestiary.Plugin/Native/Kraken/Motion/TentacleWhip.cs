// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion
{
    /// <summary>
    /// Moves a tentacle from one pose to another the way a whip moves rather than the way points slide: every segment
    /// turns from its old heading to its new one (bending in the frame's plane and swinging out of it), the base first
    /// and each segment further out a little later, the tip last, and the tentacle is rebuilt from its base segment by
    /// segment. So a strike rolls down the tentacle and the tip snaps onto the deck at the very end, and the length never
    /// changes on the way.
    /// </summary>
    public static class TentacleWhip
    {
        /// <param name="t">how far along the move, 0 to 1 (already eased by the caller)</param>
        /// <param name="lag">how much later the tip starts turning than the base, as a share of the move</param>
        public static void Blend(TentacleFrame frame, Vector3[] from, Vector3[] to, float t, float lag, Vector3[] into)
        {
            Vector3 at = Vector3.LerpUnclamped(from[0], to[0], t);
            float segment = TentacleSpec.Segment * frame.Scale;
            into[0] = at;
            for (int i = 0; i < TentacleSpec.Bones; i++)
            {
                float u = i / (float)(TentacleSpec.Bones - 1);
                float own = Mathf.Clamp01((t - lag * u) / Mathf.Max(1f - lag, 0.01f));
                at += Turn(frame, from[i + 1] - from[i], to[i + 1] - to[i], own) * segment;
                into[i + 1] = at;
            }
        }

        // A heading part of the way from one direction to another, by their bend and their swing, in the frame.
        private static Vector3 Turn(TentacleFrame frame, Vector3 a, Vector3 b, float t)
        {
            (float bendA, float sideA) = Angles(frame, a);
            (float bendB, float sideB) = Angles(frame, b);
            float bend = Mathf.LerpAngle(bendA, bendB, t) * Mathf.Deg2Rad;
            float side = Mathf.Lerp(sideA, sideB, t) * Mathf.Deg2Rad;
            return frame.Direction(new Vector3(Mathf.Sin(bend) * Mathf.Cos(side), Mathf.Cos(bend) * Mathf.Cos(side), Mathf.Sin(side)));
        }

        // Degrees: the bend from Up towards Out, and the swing out of that plane towards Side.
        private static (float bend, float side) Angles(TentacleFrame frame, Vector3 direction)
        {
            float x = Vector3.Dot(direction, frame.Out), y = Vector3.Dot(direction, frame.Up), z = Vector3.Dot(direction, frame.Side);
            float bend = Mathf.Atan2(x, y) * Mathf.Rad2Deg;
            float side = Mathf.Atan2(z, Mathf.Sqrt(x * x + y * y)) * Mathf.Rad2Deg;
            return (bend, side);
        }
    }
}
