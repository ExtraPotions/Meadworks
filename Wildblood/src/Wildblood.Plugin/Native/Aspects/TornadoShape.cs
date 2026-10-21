// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// A Nightfall tornado's funnel: `base width` across where it touches the ground, `top width` across at its top,
    /// `height` tall, staying narrow low down and flaring toward the top the way a funnel cloud does. The look and the
    /// hit both measure it here, so the funnel a player sees is the funnel that hurts.
    /// </summary>
    internal readonly struct TornadoShape
    {
        /// <summary>How sharply the funnel flares toward the top: 1 would be a straight cone.</summary>
        private const float Flare = 1.4f;

        public readonly float BaseRadius;
        public readonly float TopRadius;
        public readonly float Height;

        public TornadoShape(float baseWidth, float topWidth, float height)
        {
            BaseRadius = baseWidth * 0.5f;
            TopRadius = topWidth * 0.5f;
            Height = height;
        }

        /// <summary>The funnel's radius at <paramref name="share"/> of its height (0 the ground, 1 the top).</summary>
        public float RadiusAt(float share) => Between(BaseRadius, TopRadius, share);

        /// <summary>A radius from <paramref name="low"/> to <paramref name="high"/> along the funnel's flare.</summary>
        public static float Between(float low, float high, float share) =>
            Mathf.Lerp(low, high, Mathf.Pow(Mathf.Clamp01(share), Flare));
    }
}
