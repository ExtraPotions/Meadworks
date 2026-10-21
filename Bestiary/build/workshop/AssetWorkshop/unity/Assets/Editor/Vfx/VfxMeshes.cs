using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Workshop.Vfx
{
    /// <summary>
    /// Small meshes for mesh particles, made in code like the game's debris: a knobbly chunk (stone, ice, bone bits,
    /// 20 to 80 triangles), a shard (a long pointed crystal, 8 to 16), a splinter (a thin pointed stick, 12). Flat
    /// shaded, about a metre across; the particle's size scales them.
    /// </summary>
    public static class VfxMeshes
    {
        public static Mesh Make(string folder, MeshSpec spec)
        {
            var random = new System.Random(spec.seed + 1);
            Mesh mesh = spec.kind == "shard" ? Shard(random) : spec.kind == "splinter" ? Splinter(random) : Chunk(random, spec.detail);
            Vector3 size = VfxCurves.Vector(spec.size);
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = Vector3.Scale(vertices[i], size);
            mesh.vertices = vertices;
            Finish(mesh, spec.name);
            AssetDatabase.CreateAsset(mesh, folder + "/" + spec.name + ".asset");
            return mesh;
        }

        /// <summary>An octahedron (detail 0) or its once-subdivided sphere (detail 1), each corner pushed in or out.</summary>
        private static Mesh Chunk(System.Random random, int detail)
        {
            var points = new List<Vector3> { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            var faces = new List<int> { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
            for (int level = 0; level < detail; level++)
                faces = Subdivide(points, faces);
            for (int i = 0; i < points.Count; i++)
                points[i] = Vector3.Scale(points[i].normalized * (0.38f + 0.14f * (float)random.NextDouble()), new Vector3(1f, 0.75f, 0.9f));
            return Flat(points, faces);
        }

        private static List<int> Subdivide(List<Vector3> points, List<int> faces)
        {
            var next = new List<int>();
            var middles = new Dictionary<long, int>();
            int Middle(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (!middles.TryGetValue(key, out int index))
                {
                    index = points.Count;
                    points.Add(((points[a] + points[b]) * 0.5f).normalized);
                    middles[key] = index;
                }
                return index;
            }
            for (int i = 0; i < faces.Count; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2], ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                next.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
            }
            return next;
        }

        /// <summary>A four-sided bipyramid, long along Y, its waist off-centre.</summary>
        private static Mesh Shard(System.Random random)
        {
            float waist = 0.15f + 0.15f * (float)random.NextDouble();
            var points = new List<Vector3> { new Vector3(0, 0.5f, 0), new Vector3(0, -0.5f, 0) };
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI / 2 + 0.3f * (float)random.NextDouble();
                points.Add(new Vector3(Mathf.Cos(angle) * 0.14f, waist - 0.2f, Mathf.Sin(angle) * 0.1f));
            }
            var faces = new List<int> { 0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 2, 5, 1, 2, 3, 1, 3, 4, 1, 4, 5, 1, 5, 2 };
            return Flat(points, faces);
        }

        /// <summary>A thin triangular stick, pointed at both ends.</summary>
        private static Mesh Splinter(System.Random random)
        {
            float bend = 0.04f * (float)random.NextDouble();
            var points = new List<Vector3> { new Vector3(0, 0.5f, bend), new Vector3(0, -0.5f, 0),
                new Vector3(0.05f, 0, 0), new Vector3(-0.04f, 0.02f, 0.03f), new Vector3(-0.02f, -0.01f, -0.05f) };
            var faces = new List<int> { 0, 2, 3, 0, 3, 4, 0, 4, 2, 1, 3, 2, 1, 4, 3, 1, 2, 4 };
            return Flat(points, faces);
        }

        /// <summary>Every triangle its own three vertices, so normals are flat like the game's low-poly debris.</summary>
        private static Mesh Flat(List<Vector3> points, List<int> faces)
        {
            var vertices = new Vector3[faces.Count];
            var uv = new Vector2[faces.Count];
            var triangles = new int[faces.Count];
            for (int i = 0; i < faces.Count; i++)
            {
                vertices[i] = points[faces[i]];
                uv[i] = new Vector2(vertices[i].x + 0.5f, vertices[i].y + 0.5f);
                triangles[i] = i;
            }
            return new Mesh { vertices = vertices, uv = uv, triangles = triangles };
        }

        private static void Finish(Mesh mesh, string name)
        {
            mesh.name = name;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }
    }
}
