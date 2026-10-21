using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// The bone greataxe split into the pieces it is made of, for the mod (the axe breaking on impact, the new axe
    /// forming a piece at a time): the mesh is some three hundred islands, each vertebra of the haft a dozen of them, so
    /// the islands are grouped by the vertebra they sit at (22 pieces); the blade is one big island, shared out between
    /// five seeds by each face's nearest; the socket and hooked back at the top between two (the same seeds the
    /// preview breaks it along, assets/ecp_headsman/blender_shatter.py, in Unity axes). The pieces keep the axe's own
    /// frame, so laid together they are the axe; ordered bottom up by their middles. The axehead (the item the Crypt
    /// Executioner drops) is every piece above <see cref="HeadStart"/>.
    /// </summary>
    public static class HeadsmanPieces
    {
        public const float HeadStart = 0.83f;
        private const int Vertebrae = 22;
        private const float First = 0.065f, Step = 1.102f / 21f, Top = 1.19f;
        private static readonly Vector3[] BladeSeeds =
            { new Vector3(-0.16f, 0.98f, 0f), new Vector3(-0.42f, 0.95f, 0f), new Vector3(-0.26f, 1.2f, 0f), new Vector3(-0.47f, 1.28f, 0f), new Vector3(-0.22f, 1.42f, 0f) };
        private static readonly Vector3[] BackSeeds = { new Vector3(0.14f, 1.25f, 0f), new Vector3(0f, 1.25f, 0f) };

        /// <summary>(name, triangle indices of `mesh`) per piece, bottom up; `frame` takes its vertices into the axe's frame.</summary>
        public static List<(string name, int[] triangles)> Split(Mesh mesh, Matrix4x4 frame)
        {
            Vector3[] v = mesh.vertices.Select(p => frame.MultiplyPoint3x4(p)).ToArray();
            int[] tris = mesh.triangles;
            var groups = new Dictionary<string, List<int>>();
            foreach (List<int> island in Islands(v, tris))
                foreach (var (key, face) in Grouped(island, v, tris))
                    (groups.TryGetValue(key, out List<int> list) ? list : groups[key] = new List<int>()).Add(face);
            return groups.OrderBy(g => g.Value.Average(f => Middle(v, tris, f).y))
                .Select((g, i) => ($"piece_{i:00}_{g.Key}", g.Value.SelectMany(f => new[] { tris[3 * f], tris[3 * f + 1], tris[3 * f + 2] }).ToArray()))
                .ToList();
        }

        /// <summary>A mesh of the triangles only, in the axe's frame, its vertices those they use.</summary>
        public static Mesh Piece(Mesh mesh, Matrix4x4 frame, int[] triangles, string name)
        {
            int[] used = triangles.Distinct().ToArray();
            var map = used.Select((old, i) => (old, i)).ToDictionary(p => p.old, p => p.i);
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            Vector2[] uv = mesh.uv;
            var piece = new Mesh { name = name };
            piece.vertices = used.Select(i => frame.MultiplyPoint3x4(vertices[i])).ToArray();
            piece.normals = used.Select(i => frame.MultiplyVector(normals[i]).normalized).ToArray();
            piece.uv = used.Select(i => uv[i]).ToArray();
            piece.triangles = triangles.Select(i => map[i]).ToArray();
            piece.RecalculateBounds();
            return piece;
        }

        /// <summary>Each face of an island with the piece it goes to.</summary>
        private static IEnumerable<(string key, int face)> Grouped(List<int> island, Vector3[] v, int[] tris)
        {
            Vector3 middle = island.Aggregate(Vector3.zero, (sum, f) => sum + Middle(v, tris, f)) / island.Count;
            if (island.Count <= 500 && middle.y <= Top)
            {
                int slot = Mathf.Clamp(Mathf.RoundToInt((middle.y - First) / Step), 0, Vertebrae - 1);
                return island.Select(f => ($"vertebra_{slot:00}", f));
            }
            bool blade = island.Count > 500;
            Vector3[] seeds = blade ? BladeSeeds : BackSeeds;
            return island.Select(f => ($"{(blade ? "blade" : "back")}_{Nearest(seeds, Middle(v, tris, f))}", f));
        }

        private static int Nearest(Vector3[] seeds, Vector3 p) =>
            Enumerable.Range(0, seeds.Length).OrderBy(i => (seeds[i] - p).sqrMagnitude).First();

        private static Vector3 Middle(Vector3[] v, int[] tris, int face) => (v[tris[3 * face]] + v[tris[3 * face + 1]] + v[tris[3 * face + 2]]) / 3f;

        /// <summary>The faces joined where they share a corner's position (the import splits corners along UV seams).</summary>
        private static List<List<int>> Islands(Vector3[] v, int[] tris)
        {
            int faces = tris.Length / 3;
            int[] parent = Enumerable.Range(0, faces).ToArray();
            int Root(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
            var owner = new Dictionary<Vector3Int, int>();
            for (int f = 0; f < faces; f++)
                for (int c = 0; c < 3; c++)
                {
                    Vector3 p = v[tris[3 * f + c]] * 100000f;
                    var key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                    parent[Root(owner.TryGetValue(key, out int other) ? other : owner[key] = f)] = Root(f);
                }
            return Enumerable.Range(0, faces).GroupBy(Root).Select(g => g.ToList()).ToList();
        }

        /// <summary>The axe's mesh, in its prefab's frame (the prefab holds one mesh under its root).</summary>
        public static (Mesh mesh, Material material, Matrix4x4 frame) Axe()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeadsmanAxe.Prefab);
            MeshFilter filter = prefab.GetComponentsInChildren<MeshFilter>().OrderByDescending(f => f.sharedMesh.vertexCount).First();
            Matrix4x4 frame = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            return (filter.sharedMesh, filter.GetComponent<MeshRenderer>().sharedMaterial, frame);
        }
    }
}
