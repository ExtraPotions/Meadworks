// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion
{
    /// <summary>
    /// The top of a ship along the line a tentacle strikes: a height every <see cref="Step"/> metres out from the
    /// tentacle's base, in metres above the waterline there, NaN where the line is over open water.
    /// </summary>
    public readonly struct DeckProfile
    {
        public const float Water = -0.25f;
        private const float RailReach = 2.5f;

        public readonly float Step;
        public readonly float[] Heights;

        public DeckProfile(float step, float[] heights)
        {
            Step = step;
            Heights = heights;
        }

        /// <summary>The surface at <paramref name="x"/> metres out; just under the water where there is no ship.</summary>
        public float Height(float x)
        {
            if (Heights == null || Heights.Length == 0)
            {
                return Water;
            }
            float h = Heights[Mathf.Clamp(Mathf.RoundToInt(x / Step), 0, Heights.Length - 1)];
            return float.IsNaN(h) ? Water : h;
        }

        /// <summary>The top of the hull beside the base: the highest point of the ship within a couple of metres.</summary>
        public float Rail()
        {
            float rail = float.NegativeInfinity;
            for (int i = 0; Heights != null && i < Heights.Length && i * Step <= RailReach; i++)
            {
                if (!float.IsNaN(Heights[i]))
                {
                    rail = Mathf.Max(rail, Heights[i]);
                }
            }
            return float.IsNegativeInfinity(rail) ? 0.4f : rail;
        }
    }

    /// <summary>
    /// A tentacle lying across a ship: up out of the water beside the hull, over the rail and along the deck, following
    /// its surface (benches, chests and the far rail included) for as far as it reaches, and down into the sea beyond.
    /// Whatever the visible part does not use of the tentacle's length hangs straight down under the water, so a short
    /// reach (a tentacle gripping the rail) keeps the rest of it out of sight.
    /// </summary>
    public static class TentacleDeck
    {
        private const float ArcRadius = 0.7f;
        private const int ArcSteps = 6;
        private const float Clearance = 0.08f;
        private const float MinUnder = 0.8f;
        private const float MaxDrop = 1.1f;   // metres down per metre along: it drapes, it does not fold

        /// <summary>The path being built, one list reused by every tentacle on every frame (all on the main thread).</summary>
        private static readonly List<Vector3> Path = new List<Vector3>();

        public static void Build(TentacleFrame frame, DeckProfile deck, float reach, Vector3[] into)
        {
            List<Vector3> path = Visible(deck, reach);
            float under = Mathf.Max(MinUnder, TentacleSpec.Length - TentacleChain.Length(path));
            path.Insert(0, new Vector3(0f, -under, 0f));
            TentacleChain.Resample(path, frame, into);
        }

        /// <summary>
        /// A tentacle lying on a deck squirming: the part past the rail shifts from side to side in a wave running to the
        /// tip, <paramref name="amount"/> of the full squirm (0 still).
        /// </summary>
        public static void Writhe(TentacleFrame frame, float amount, float time, Vector3[] points)
        {
            if (amount <= 0f)
            {
                return;
            }
            for (int i = 1; i < points.Length; i++)
            {
                float u = TentacleSpec.Share(i);
                float shift = 0.14f * amount * Ease.Smooth(0.4f, 1f, u) * Mathf.Sin(time * 4.5f - u * 9f);
                points[i] += frame.Side * (shift * frame.Scale);
            }
        }

        private static List<Vector3> Visible(DeckProfile deck, float reach)
        {
            float top = Mathf.Max(deck.Rail() + TentacleSpec.Radius(0.25f) + Clearance, 0.1f);
            List<Vector3> path = Path;
            path.Clear();
            path.Add(Vector3.zero);
            path.Add(new Vector3(0f, top, 0f));
            for (int i = 1; i <= ArcSteps; i++)
            {
                float a = Mathf.PI - i * (Mathf.PI / 2f) / ArcSteps;
                path.Add(new Vector3(ArcRadius + ArcRadius * Mathf.Cos(a), top + ArcRadius * Mathf.Sin(a), 0f));
            }
            Run(path, deck, reach);
            return path;
        }

        // Along the surface from the end of the arc, `reach` metres at most and never longer than the tentacle.
        private static void Run(List<Vector3> path, DeckProfile deck, float reach)
        {
            float used = TentacleChain.Length(path);
            Vector3 last = path[path.Count - 1];
            for (float along = 0f; along < reach && used < TentacleSpec.Length - MinUnder; along += deck.Step)
            {
                float x = last.x + deck.Step;
                float lying = deck.Height(x) + TentacleSpec.Radius((used + MinUnder) / TentacleSpec.Length) * 0.8f + Clearance;
                var point = new Vector3(x, Mathf.Max(lying, last.y - MaxDrop * deck.Step), 0f);
                used += Vector3.Distance(last, point);
                path.Add(point);
                last = point;
            }
        }
    }
}
