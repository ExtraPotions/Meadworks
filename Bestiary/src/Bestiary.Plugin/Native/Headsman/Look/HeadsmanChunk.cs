// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// A faceted stone a unit across: an icosahedron with each corner pushed in or out a little, every face flat, its
    /// texture laid across it from the side (so the game's rock texture shows on it, not one texel).
    /// </summary>
    public static class HeadsmanChunk
    {
        private static readonly float T = (1f + Mathf.Sqrt(5f)) / 2f;

        private static readonly Vector3[] Ico =
        {
            new Vector3(-1, T, 0), new Vector3(1, T, 0), new Vector3(-1, -T, 0), new Vector3(1, -T, 0),
            new Vector3(0, -1, T), new Vector3(0, 1, T), new Vector3(0, -1, -T), new Vector3(0, 1, -T),
            new Vector3(T, 0, -1), new Vector3(T, 0, 1), new Vector3(-T, 0, -1), new Vector3(-T, 0, 1),
        };

        private static readonly int[] Faces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        /// <summary>A chunk shaped by `seed`, a slab's proportions (0.8 wide, 0.5 thick, 1.15 long).</summary>
        public static Mesh Mesh(int seed)
        {
            var random = new System.Random(seed);
            var corners = new Vector3[Ico.Length];
            for (int i = 0; i < Ico.Length; i++)
            {
                corners[i] = Vector3.Scale(Ico[i].normalized * (0.75f + 0.4f * (float)random.NextDouble()), new Vector3(0.4f, 0.25f, 0.575f));
            }
            var vertices = new Vector3[Faces.Length];
            var uv = new Vector2[Faces.Length];
            var triangles = new int[Faces.Length];
            for (int i = 0; i < Faces.Length; i++)
            {
                vertices[i] = corners[Faces[i]];
                uv[i] = new Vector2(vertices[i].x + vertices[i].z, vertices[i].y) * 2f;
                triangles[i] = i;
            }
            var mesh = new Mesh { name = "ecp_headsman_stone_" + seed, vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
