// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion
{
    /// <summary>
    /// Tentacles away from a ship: trailing behind the swimming head in rippling waves (the frame's Out points
    /// backwards from where it swims), or lying limp on the water around a dead one.
    /// </summary>
    public static class TentacleSwim
    {
        /// <summary>Behind the head and a little under it, rippling from base to tip.</summary>
        public static void Trail(TentacleFrame frame, float time, float phase, Vector3[] into)
        {
            Vector3 at = Vector3.zero;
            into[0] = frame.World(at);
            for (int i = 0; i < TentacleSpec.Bones; i++)
            {
                float u = (i + 0.5f) / TentacleSpec.Bones;
                float pitch = (-9f + 12f * Mathf.Sin(time * 3f - u * 5f + phase)) * Mathf.Deg2Rad;
                float yaw = 20f * Mathf.Sin(time * 2.2f - u * 4f + phase * 1.3f) * Mathf.Deg2Rad;
                at += Heading(pitch, yaw) * TentacleSpec.Segment;
                into[i + 1] = frame.World(at);
            }
        }

        /// <summary>
        /// Spread out from the base across the water, just under its surface (<paramref name="water"/> metres above the
        /// frame's origin), curling slowly.
        /// </summary>
        public static void Limp(TentacleFrame frame, float water, float time, float phase, Vector3[] into)
        {
            Vector3 at = Vector3.zero;
            into[0] = frame.World(at);
            for (int i = 0; i < TentacleSpec.Bones; i++)
            {
                float u = (i + 0.5f) / TentacleSpec.Bones;
                float yaw = (25f * u + 10f * Mathf.Sin(time * 0.6f + u * 3f + phase)) * Mathf.Deg2Rad;
                at += Heading(0f, yaw) * TentacleSpec.Segment;
                at.y = Mathf.MoveTowards(at.y, water - TentacleSpec.Radius(u) * 0.5f, TentacleSpec.Segment);
                into[i + 1] = frame.World(at);
            }
        }

        private static Vector3 Heading(float pitch, float yaw) =>
            new Vector3(Mathf.Cos(pitch) * Mathf.Cos(yaw), Mathf.Sin(pitch), Mathf.Cos(pitch) * Mathf.Sin(yaw));
    }
}
