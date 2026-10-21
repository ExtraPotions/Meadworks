using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Workshop.Kraken
{
    /// <summary>
    /// The head's column spine (kh_body_1..3): bent 25 degrees at each bone about X and about Z, the skin must follow
    /// smoothly (no edge between neighbouring vertices stretching by more than 0.3 m), its lower end must ride kh_body_3
    /// rigidly, and nothing above y -0.8 may move. The culling box must hold every pose the mod may use: the column bent
    /// 3 x 30 degrees any way, with the neck leaned 0 or 40 degrees either way.
    /// </summary>
    public static class KrakenBodyCheck
    {
        private const float MaxStretch = 0.3f;

        public static void Run(GameObject head, SkinnedMeshRenderer skin, Action<string> fail)
        {
            Mesh mesh = skin.sharedMesh;
            Vector3[] rest = mesh.vertices;
            var edges = Edges(mesh.triangles);
            foreach (var axis in new[] { Vector3.right, Vector3.forward })
            {
                KrakenPose.Body(head, axis * 25f);
                Vector3[] bent = KrakenCheck.Baked(skin);
                Bend(head, rest, bent, edges, axis == Vector3.right ? "X" : "Z", fail);
                KrakenPose.Body(head, Vector3.zero);
            }
            Culling(head, skin, fail);
        }

        private static void Bend(GameObject head, Vector3[] rest, Vector3[] bent, (int a, int b)[] edges, string axis, Action<string> fail)
        {
            float stretch = edges.Max(e => Vector3.Distance(bent[e.a], bent[e.b]) - Vector3.Distance(rest[e.a], rest[e.b]));
            float still = Enumerable.Range(0, rest.Length).Where(i => rest[i].y >= -0.8f).Max(i => Vector3.Distance(bent[i], rest[i]));
            Transform last = KrakenCheck.Find(head, "kh_body_3");
            Vector3 lastRest = new Vector3(0f, -3.8f, 0f);
            int[] low = Enumerable.Range(0, rest.Length).Where(i => rest[i].y < -3.85f).ToArray();
            float rigid = low.Max(i => Vector3.Distance(bent[i], head.transform.InverseTransformPoint(last.TransformPoint(rest[i] - lastRest))));
            float bottom = low.Max(i => Vector3.Distance(bent[i], rest[i]));
            Log.Info($"column bent 25 deg about {axis} at kh_body_1..3: max edge stretch {stretch:F3} m, {low.Length} vertices below " +
                     $"y -3.85 ride kh_body_3 within {rigid * 1000f:F2} mm (moved up to {bottom:F2} m), above y -0.8 moved {still * 1000f:F2} mm");
            if (stretch > MaxStretch)
                fail($"bending the column about {axis} stretches an edge by {stretch:F2} m");
            if (rigid > 0.001f || bottom < 0.5f)
                fail($"the column's lower end does not follow kh_body_3 when bent about {axis}");
            if (still > 0.001f)
                fail($"bending the column about {axis} moves the skin above y -0.8 by {still:F3} m");
        }

        private static (int, int)[] Edges(int[] triangles)
        {
            var edges = new HashSet<(int, int)>();
            for (int t = 0; t < triangles.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = triangles[t + k], b = triangles[t + (k + 1) % 3];
                    edges.Add(a < b ? (a, b) : (b, a));
                }
            return edges.ToArray();
        }

        /// <summary>Every vertex of every renderer stays inside its culling box in the extreme poses.</summary>
        private static void Culling(GameObject head, SkinnedMeshRenderer skin, Action<string> fail)
        {
            var renderers = head.GetComponentsInChildren<SkinnedMeshRenderer>();
            var bends = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back, new Vector3(1, 0, 1).normalized };
            float worst = float.MaxValue;
            foreach (var bend in bends)
                foreach (float lean in new[] { -40f, 0f, 40f })
                {
                    KrakenPose.Body(head, bend * 30f);
                    KrakenPose.Neck(head, lean);
                    foreach (var r in renderers)
                        worst = Mathf.Min(worst, Margin(r.localBounds, KrakenCheck.Baked(r)));
                }
            KrakenPose.Body(head, Vector3.zero);
            KrakenPose.Neck(head, 0f);
            Log.Info($"culling box {skin.localBounds.min:F1}..{skin.localBounds.max:F1}: holds the column bent 3 x 30 deg any way " +
                     $"with the neck at 0 or +/-40 deg, {worst:F2} m to spare");
            if (worst < 0f)
                fail($"the head's culling box is too small for a bent column (short by {-worst:F2} m)");
        }

        /// <summary>The smallest distance from any vertex to the box's faces; negative when a vertex is outside.</summary>
        private static float Margin(Bounds box, Vector3[] vertices) =>
            vertices.Min(p => Mathf.Min(Mathf.Min(p.x - box.min.x, box.max.x - p.x),
                                        Mathf.Min(Mathf.Min(p.y - box.min.y, box.max.y - p.y), Mathf.Min(p.z - box.min.z, box.max.z - p.z))));
    }
}
