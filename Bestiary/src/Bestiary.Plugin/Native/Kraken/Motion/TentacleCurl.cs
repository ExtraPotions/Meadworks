// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion
{
    /// <summary>How a free tentacle bends: leaning at the base, curling towards the frame's Out further up, swaying.</summary>
    public struct CurlShape
    {
        public float Lean;       // degrees from upright at the base; negative leans away from Out
        public float Curl;       // degrees it bends towards Out along its length
        public float CurlFrom;   // share of the length where the curl starts
        public float Sway;       // degrees of sway towards and away from Out, growing to the tip
        public float SideSway;   // degrees of sway to the sides, growing to the tip
        public float Flick;      // degrees the last quarter curls and uncurls, quicker than the sway
        public float BaseDepth;  // metres of its base under the water
        public float Pace;       // how fast it all moves; 0 or less is 1

        /// <summary>
        /// This shape for tentacle <paramref name="index"/>: each leans, curls and flicks a little differently, so six
        /// together read as a living thing rather than copies.
        /// </summary>
        public CurlShape For(int index)
        {
            float a = Mathf.Sin(index * 12.9898f) * 0.5f, b = Mathf.Sin(index * 78.233f + 1f) * 0.5f;
            CurlShape shape = this;
            shape.Lean += a * 8f;
            shape.Curl *= 1f + b * 0.2f;
            shape.CurlFrom += a * 0.06f;
            shape.Flick *= 1f + a * 0.4f;
            return shape;
        }

        /// <summary>This shape straightened towards upright by <paramref name="share"/> (0 unchanged, 1 straight up).</summary>
        public CurlShape Straightened(float share)
        {
            CurlShape shape = this;
            float keep = 1f - Mathf.Clamp01(share);
            (shape.Lean, shape.Curl, shape.Flick) = (shape.Lean * keep, shape.Curl * keep, shape.Flick * keep);
            return shape;
        }
    }

    /// <summary>
    /// Free-standing tentacle poses built bone by bone from the base (each segment turned a little further than the one
    /// below): standing out of the water swaying, its curl breathing and its tip flicking, or reared back high before it
    /// strikes. A tentacle rising out of the water comes up straight and curls over as it clears the surface
    /// (<see cref="Rising"/>); <see cref="Emerge"/> sinks any pose straight down under the water.
    /// </summary>
    public static class TentacleCurl
    {
        /// <summary>
        /// Up out of the water beside the ship, leaning out, the top hooked over towards the deck, flailing: big, quick
        /// and restless, never quite repeating.
        /// </summary>
        public static readonly CurlShape Idle = new CurlShape
        {
            Lean = -12f, Curl = 115f, CurlFrom = 0.35f, Sway = 24f, SideSway = 26f, Flick = 42f, BaseDepth = 1f, Pace = 1.8f,
        };

        /// <summary>Reared back and high, curled over towards its target: the moment before a strike.</summary>
        public static readonly CurlShape Raised = new CurlShape
        {
            Lean = -20f, Curl = 160f, CurlFrom = 0.5f, Sway = 3f, SideSway = 3f, Flick = 10f, BaseDepth = 0.6f, Pace = 1f,
        };

        public static void Build(TentacleFrame frame, CurlShape shape, float time, float phase, Vector3[] into)
        {
            Vector3 at = new Vector3(0f, -shape.BaseDepth, 0f);
            into[0] = frame.World(at);
            for (int i = 0; i < TentacleSpec.Bones; i++)
            {
                float u = (i + 0.5f) / TentacleSpec.Bones;
                at += Direction(shape, u, time, phase) * TentacleSpec.Segment;
                into[i + 1] = frame.World(at);
            }
        }

        /// <summary>
        /// The pose of a tentacle <paramref name="emerged"/> of the way out of the water (0 wholly under, 1 as built):
        /// straight while it rises, curling over only as it clears the surface.
        /// </summary>
        public static void Rising(TentacleFrame frame, CurlShape shape, float emerged, float time, float phase, Vector3[] into)
        {
            float e = Mathf.Clamp01(emerged);
            Build(frame, shape.Straightened(1f - e * e), time, phase, into);
            Emerge(frame, e, into);
        }

        /// <summary>The pose sunk by the part not yet out of the water: 0 wholly under, 1 as built.</summary>
        public static void Emerge(TentacleFrame frame, float emerged, Vector3[] points)
        {
            float depth = (1f - Mathf.Clamp01(emerged)) * (TentacleSpec.Length + 1.5f) * frame.Scale;
            TentacleChain.Shift(points, -frame.Up * depth);
        }

        private static Vector3 Direction(CurlShape shape, float u, float time, float phase)
        {
            time *= shape.Pace > 0f ? shape.Pace : 1f;
            float breathe = 1f + 0.12f * Mathf.Sin(time * 0.6f + phase * 2.1f);
            float restless = 0.6f * Mathf.Sin(time * 1.3f + phase + u * 3f) + 0.4f * Mathf.Sin(time * 2.9f + phase * 3.1f + u * 5f);
            float bend = shape.Lean + shape.Curl * breathe * Ease.Smooth(shape.CurlFrom, 1f, u)
                + shape.Sway * u * restless
                + shape.Flick * Ease.Smooth(0.7f, 1f, u) * Mathf.Sin(time * 2.3f + phase * 1.9f - u * 6f);
            float side = shape.SideSway * u * (0.7f * Mathf.Sin(time * 0.9f + phase * 1.7f - u * 2.2f) + 0.3f * Mathf.Sin(time * 2.1f + phase * 0.7f));
            float b = bend * Mathf.Deg2Rad;
            float s = side * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(b) * Mathf.Cos(s), Mathf.Cos(b) * Mathf.Cos(s), Mathf.Sin(s));
        }
    }
}
