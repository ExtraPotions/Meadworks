using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Workshop.Kraken
{
    /// <summary>
    /// The numbers the mod's author needs, written to the log and to report.txt: each prefab's transform tree (local
    /// positions, rest world rotations), measured bounds and triangles, the head's markers, the tentacle's radius along
    /// its length, materials and textures, and the bundle sizes.
    /// </summary>
    public static class KrakenReport
    {
        private static readonly StringBuilder Text = new StringBuilder();

        public static void Begin() => Text.Clear();

        public static void Line(string line)
        {
            Text.AppendLine(line);
            Log.Info(line);
        }

        public static void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Text.ToString());
        }

        public static void Prefab(GameObject root)
        {
            Line($"== {root.name}");
            Tree(root.transform, root.transform, 0);
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Vector3[] v = KrakenCheck.Baked(skin);
                var bounds = new Bounds(v[0], Vector3.zero);
                foreach (var p in v)
                    bounds.Encapsulate(p);
                var mesh = skin.sharedMesh;
                Line($"  mesh {skin.name}: {mesh.triangles.Length / 3} triangles, {mesh.vertexCount} vertices, " +
                     $"{skin.bones.Length} bones, root bone {skin.rootBone.name}, material {skin.sharedMaterial.name} " +
                     $"(albedo {Name(skin.sharedMaterial, "_MainTex")}, normal {Name(skin.sharedMaterial, "_BumpMap")}); " +
                     $"rest bounds min {bounds.min:F2} max {bounds.max:F2} size {bounds.size:F2}; culling bounds " +
                     $"centre {skin.localBounds.center:F1} size {skin.localBounds.size:F1} about the root bone");
                Weights(skin);
            }
        }

        /// <summary>How many vertices have 1, 2, 3 or 4 influences, and how many each bone moves.</summary>
        private static void Weights(SkinnedMeshRenderer skin)
        {
            var influences = new int[5];
            var perBone = new int[skin.bones.Length];
            foreach (var w in skin.sharedMesh.boneWeights)
            {
                var pairs = new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) }
                    .Where(p => p.Item2 > 0f).ToArray();
                influences[pairs.Length]++;
                foreach (var (bone, _) in pairs)
                    perBone[bone]++;
            }
            string bones = string.Join(", ", Enumerable.Range(0, perBone.Length).Where(i => perBone[i] > 0).Select(i => $"{skin.bones[i].name} {perBone[i]}"));
            Line($"    weights {skin.name}: {influences[1]} vertices on 1 bone, {influences[2]} on 2, {influences[3]} on 3, " +
                 $"{influences[4]} on 4; vertices per bone: {bones}");
        }

        private static string Name(Material material, string property)
        {
            var texture = material.HasProperty(property) ? material.GetTexture(property) : null;
            return texture == null ? "none" : $"{texture.name} {texture.width}px";
        }

        private static void Tree(Transform t, Transform root, int depth)
        {
            if (depth > 0 && t.GetComponent<SkinnedMeshRenderer>() == null)
            {
                Vector3 world = root.InverseTransformPoint(t.position);
                Line($"  {new string(' ', 2 * (depth - 1))}{t.name}  local {t.localPosition:F4}  root-space {world:F4}  " +
                     $"rest rotation {t.eulerAngles:F1}");
            }
            foreach (Transform child in t)
                Tree(child, root, depth + 1);
        }

        /// <summary>The tentacle's measured radius: the top of the skin (+Y, no suckers there) at a few points along it.</summary>
        public static void Radii(GameObject tentacle)
        {
            Vector3[] v = tentacle.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.vertices;
            var parts = new List<string>();
            foreach (float u in new[] { 0.02f, 0.15f, 0.35f, 0.6f, 0.85f, 0.97f })
            {
                float z = u * KrakenContract.Length;
                var slab = v.Where(p => Mathf.Abs(p.z - z) < 0.12f).ToArray();
                float top = slab.Max(p => p.y), side = slab.Max(p => Mathf.Abs(p.x)), under = -slab.Where(p => Mathf.Abs(p.x) < 0.1f * top).Min(p => p.y);
                parts.Add($"u {u:F2} (z {z:F2}): top {top:F3}, side {side:F3}, underside {under:F3} (contract {KrakenContract.RadiusAt(u):F3})");
            }
            Line("  tentacle radius: " + string.Join("; ", parts));
        }

        public static void Bundle(string file) => Line($"bundle {file}: {new FileInfo(file).Length} bytes");
    }
}
