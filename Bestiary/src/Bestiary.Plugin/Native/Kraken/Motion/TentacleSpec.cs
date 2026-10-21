// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion
{
    /// <summary>
    /// The long tentacle as the workshop builds it (AssetWorkshop assets/ecp_kraken): a chain of <see cref="Bones"/>
    /// bones of <see cref="Segment"/> metres, straight along +Z at rest with the suckers on -Y, thick at the base and
    /// thin at the tip so a player can step out of its way. A pose is <see cref="Points"/> points: the head of each bone
    /// and the tip.
    /// </summary>
    public static class TentacleSpec
    {
        public const int Bones = 16;
        public const int Points = Bones + 1;
        public const float Segment = 0.5f;
        public const float Length = Bones * Segment;

        private static readonly float[] At = { 0f, 0.15f, 0.35f, 0.60f, 0.85f, 1f };
        private static readonly float[] Radii = { 0.50f, 0.40f, 0.28f, 0.17f, 0.09f, 0.035f };

        /// <summary>The radius in metres at a share of the length, 0 at the base and 1 at the tip.</summary>
        public static float Radius(float u)
        {
            u = Mathf.Clamp01(u);
            for (int i = 1; i < At.Length; i++)
            {
                if (u <= At[i])
                {
                    return Mathf.Lerp(Radii[i - 1], Radii[i], (u - At[i - 1]) / (At[i] - At[i - 1]));
                }
            }
            return Radii[Radii.Length - 1];
        }

        /// <summary>The share of the length at a point of a pose.</summary>
        public static float Share(int point) => point / (float)Bones;
    }
}
