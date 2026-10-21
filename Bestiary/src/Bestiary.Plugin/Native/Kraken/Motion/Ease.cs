// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion
{
    /// <summary>Easing curves for the kraken's motion: each takes and gives a share from 0 to 1.</summary>
    public static class Ease
    {
        /// <summary>0 up to <paramref name="from"/>, 1 from <paramref name="to"/> on, smooth between.</summary>
        public static float Smooth(float from, float to, float x) => InOut(Span(x, from, to));

        /// <summary>Where <paramref name="time"/> lies between two moments, as a share clamped to 0..1.</summary>
        public static float Span(float time, float start, float end) =>
            Mathf.Clamp01((time - start) / Mathf.Max(end - start, 0.0001f));

        /// <summary>Accelerating all the way: a blow that falls faster and faster.</summary>
        public static float In(float t) => t * t * t;

        /// <summary>Slow at both ends.</summary>
        public static float InOut(float t) => t * t * (3f - 2f * t);

        /// <summary>Fast at first, settling at the end.</summary>
        public static float Out(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>The share of the way to close on a target in <paramref name="dt"/> seconds at <paramref name="rate"/> per second.</summary>
        public static float Follow(float rate, float dt) => 1f - Mathf.Exp(-rate * dt);
    }
}
