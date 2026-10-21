// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion
{
    /// <summary>
    /// Where a tentacle's pose is drawn: an origin (its base, at the waterline), <see cref="Out"/> (level, towards
    /// whatever it reaches for), <see cref="Up"/> and <see cref="Side"/>, in the world, and the creature's size. Poses are
    /// worked out in metres in these axes (x out, y up, z side) and turned into world points by <see cref="World"/>.
    /// </summary>
    public readonly struct TentacleFrame
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Out;
        public readonly Vector3 Up;
        public readonly Vector3 Side;
        public readonly float Scale;

        public TentacleFrame(Vector3 origin, Vector3 outward, Vector3 up, float scale)
        {
            Up = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
            Vector3 level = Vector3.ProjectOnPlane(outward, Up);
            if (level.sqrMagnitude < 1e-6f)
            {
                level = Vector3.ProjectOnPlane(Vector3.forward, Up);
            }
            Out = level.normalized;
            Side = Vector3.Cross(Out, Up);
            Origin = origin;
            Scale = scale;
        }

        public Vector3 World(Vector3 local) => Origin + (Out * local.x + Up * local.y + Side * local.z) * Scale;

        /// <summary>A world direction in the frame's axes.</summary>
        public Vector3 Direction(Vector3 local) => Out * local.x + Up * local.y + Side * local.z;
    }
}
