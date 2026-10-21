// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion
{
    /// <summary>
    /// Operations on a tentacle pose, <see cref="TentacleSpec.Points"/> world points from base to tip: blending two
    /// poses, keeping every segment its length after a blend, moving a pose, and laying a pose along a path.
    /// </summary>
    public static class TentacleChain
    {
        public static Vector3[] New() => new Vector3[TentacleSpec.Points];

        public static void Copy(Vector3[] from, Vector3[] into) => Array.Copy(from, into, TentacleSpec.Points);

        public static void Lerp(Vector3[] a, Vector3[] b, float t, Vector3[] into)
        {
            for (int i = 0; i < into.Length; i++)
            {
                into[i] = Vector3.LerpUnclamped(a[i], b[i], t);
            }
        }

        public static void Shift(Vector3[] points, Vector3 by)
        {
            for (int i = 0; i < points.Length; i++)
            {
                points[i] += by;
            }
        }

        /// <summary>
        /// Keeps the base where it is and gives every segment its length again, each pointing at where the blend put the
        /// next point (a blend of two bent poses would otherwise shrink the tentacle).
        /// </summary>
        public static void Straighten(Vector3[] points, float segment)
        {
            Vector3 last = Vector3.up;
            for (int i = 1; i < points.Length; i++)
            {
                Vector3 direction = points[i] - points[i - 1];
                direction = direction.sqrMagnitude > 1e-8f ? direction : last;
                last = direction;
                points[i] = points[i - 1] + direction.normalized * segment;
            }
        }

        public static float Length(List<Vector3> path)
        {
            float length = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                length += Vector3.Distance(path[i - 1], path[i]);
            }
            return length;
        }

        /// <summary>A pose laid along a path (in the frame's own metres): a point every segment from the path's start.</summary>
        public static void Resample(List<Vector3> path, TentacleFrame frame, Vector3[] into)
        {
            Vector3 at = path[0];
            int next = 1;
            into[0] = frame.World(at);
            for (int i = 1; i < into.Length; i++)
            {
                at = Walk(path, ref next, at, TentacleSpec.Segment);
                into[i] = frame.World(at);
            }
        }

        // From `at` (on the stretch ending at path[next]) on along the path; past its end, straight on.
        private static Vector3 Walk(List<Vector3> path, ref int next, Vector3 at, float distance)
        {
            while (next < path.Count)
            {
                float left = Vector3.Distance(at, path[next]);
                if (left >= distance)
                {
                    return Vector3.MoveTowards(at, path[next], distance);
                }
                distance -= left;
                at = path[next];
                next++;
            }
            Vector3 end = path.Count > 1 ? path[path.Count - 1] - path[path.Count - 2] : Vector3.up;
            return at + end.normalized * distance;
        }
    }
}
