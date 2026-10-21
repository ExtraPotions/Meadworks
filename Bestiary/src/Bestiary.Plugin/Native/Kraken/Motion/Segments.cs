// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion
{
    /// <summary>The shortest distance between two line segments (a body and a stretch of tentacle).</summary>
    public static class Segments
    {
        private const float Tiny = 1e-8f;

        /// <summary>The distance between a0-a1 and b0-b1; <paramref name="onB"/> is the nearest point on the second.</summary>
        public static float Distance(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, out Vector3 onB)
        {
            Vector3 d1 = a1 - a0, d2 = b1 - b0;
            (float s, float t) = Nearest(d1, d2, a0 - b0);
            onB = b0 + d2 * t;
            return Vector3.Distance(a0 + d1 * s, onB);
        }

        // The shares along each segment of their nearest points (the classic clamped solution).
        private static (float s, float t) Nearest(Vector3 d1, Vector3 d2, Vector3 r)
        {
            float a = d1.sqrMagnitude, e = d2.sqrMagnitude, f = Vector3.Dot(d2, r);
            if (a <= Tiny)
            {
                return (0f, e <= Tiny ? 0f : Mathf.Clamp01(f / e));
            }
            float c = Vector3.Dot(d1, r);
            if (e <= Tiny)
            {
                return (Mathf.Clamp01(-c / a), 0f);
            }
            float b = Vector3.Dot(d1, d2), denominator = a * e - b * b;
            float s = denominator > Tiny ? Mathf.Clamp01((b * f - c * e) / denominator) : 0f;
            float t = (b * s + f) / e;
            if (t < 0f)
            {
                return (Mathf.Clamp01(-c / a), 0f);
            }
            return t > 1f ? (Mathf.Clamp01((b - c) / a), 1f) : (s, t);
        }
    }
}
