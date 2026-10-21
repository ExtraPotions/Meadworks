using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Workshop.Crossbow
{
    /// <summary>
    /// Records what the renderers of a scene look like frame by frame, for Blender: each renderer's mesh once (triangles,
    /// UVs, its texture's file) and its vertices in the world every frame, written as a point cache (.pc2, which Blender's
    /// Mesh Cache modifier plays), all in Blender's axes ((x, y, z) = (-ux, -uz, uy), triangles rewound for the mirror).
    /// A skinned mesh is baked as the Animator left it; a renderer that is hidden in a frame (the bolt in the fingers
    /// outside the reload) is shrunk to its origin there. The mesh itself is the first frame the renderer shows.
    /// </summary>
    public sealed class XbowCache
    {
        private sealed class Part
        {
            public Renderer Renderer;
            public Mesh Mesh;
            public string Name;
            public readonly List<Vector3[]> Samples = new List<Vector3[]>();
        }

        [Serializable]
        public sealed class MeshData
        {
            public string name, cache, texture;
            public float[] vertices, uvs, color;
            public int[] triangles;
            public bool glow;
        }

        [Serializable]
        public sealed class SceneData
        {
            public int frames;
            public float fps;
            public MeshData[] parts;
            public string[] markerNames;
            public int[] markerFrames;
        }

        private readonly List<Part> parts;
        private readonly Mesh baked = new Mesh();
        private readonly List<(string name, int frame)> markers = new List<(string, int)>();

        public XbowCache(IEnumerable<Renderer> renderers)
        {
            parts = renderers.Select((r, i) => new Part { Renderer = r, Mesh = MeshOf(r), Name = $"{i:00}_{r.name}" })
                .Where(p => p.Mesh != null).ToList();
        }

        public int Frames => parts.Count == 0 ? 0 : parts[0].Samples.Count;

        /// <summary>Named transforms recorded frame by frame beside the meshes (tracks.json).</summary>
        public XbowTracks Tracks { get; } = new XbowTracks();

        /// <summary>A named moment at the next frame to be captured.</summary>
        public void Mark(string name) => markers.Add((name, Frames));

        /// <summary>One frame: every renderer's vertices, the followed transforms, and the seconds into the fire clip.</summary>
        public void Capture(float fireTime)
        {
            foreach (Part part in parts)
                part.Samples.Add(World(part));
            Tracks.Capture(fireTime);
        }

        public void Write(string folder, float fps)
        {
            Directory.CreateDirectory(folder);
            var scene = new SceneData { frames = Frames, fps = fps, parts = parts.Select(p => Write(folder, p)).ToArray() };
            scene.markerNames = markers.Select(m => m.name).ToArray();
            scene.markerFrames = markers.Select(m => m.frame).ToArray();
            File.WriteAllText(Path.Combine(folder, "scene.json"), JsonUtility.ToJson(scene));
            Tracks.Write(folder);
            Log.Info($"blender bake: {parts.Count} parts, {Frames} frames, {markers.Count} markers into {folder}");
        }

        private static Mesh MeshOf(Renderer renderer) =>
            renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;

        private Vector3[] World(Part part)
        {
            Transform t = part.Renderer.transform;
            if (!part.Renderer.gameObject.activeInHierarchy || !part.Renderer.enabled)
                return Enumerable.Repeat(Blender(t.position), part.Mesh.vertexCount).ToArray();
            if (part.Renderer is SkinnedMeshRenderer skin)
            {
                skin.BakeMesh(baked, false);   // world-sized, in the renderer's own position and rotation
                return baked.vertices.Select(v => Blender(t.position + t.rotation * v)).ToArray();
            }
            Matrix4x4 m = t.localToWorldMatrix;
            return part.Mesh.vertices.Select(v => Blender(m.MultiplyPoint3x4(v))).ToArray();
        }

        private static Vector3 Blender(Vector3 u) => new Vector3(-u.x, -u.z, u.y);

        private static MeshData Write(string folder, Part part)
        {
            Vector3[] shown = part.Samples.FirstOrDefault(s => s.Distinct().Skip(1).Any()) ?? part.Samples[0];
            int[] tris = part.Mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
                (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            Material material = part.Renderer.sharedMaterial;
            string cache = part.Name + ".pc2";
            PointCache(Path.Combine(folder, cache), part.Samples);
            return new MeshData
            {
                name = part.Name, cache = cache, triangles = tris, texture = TextureFile(material),
                vertices = shown.SelectMany(v => new[] { v.x, v.y, v.z }).ToArray(),
                uvs = part.Mesh.uv.SelectMany(uv => new[] { uv.x, uv.y }).ToArray(),
                color = material == null ? new[] { 1f, 1f, 1f } : new[] { material.color.r, material.color.g, material.color.b },
                glow = material != null && material.IsKeywordEnabled("_EMISSION"),
            };
        }

        private static string TextureFile(Material material)
        {
            string asset = material == null || material.mainTexture == null ? "" : AssetDatabase.GetAssetPath(material.mainTexture);
            return string.IsNullOrEmpty(asset) ? "" : Path.GetFullPath(asset).Replace('\\', '/');
        }

        /// <summary>The PC2 layout: "POINTCACHE2\0", version 1, points, start frame, sample rate, samples, then xyz floats.</summary>
        private static void PointCache(string path, List<Vector3[]> samples)
        {
            using var writer = new BinaryWriter(File.Create(path), Encoding.ASCII);
            writer.Write(Encoding.ASCII.GetBytes("POINTCACHE2\0"));
            writer.Write(1);
            writer.Write(samples[0].Length);
            writer.Write(0f);
            writer.Write(1f);
            writer.Write(samples.Count);
            foreach (Vector3[] sample in samples)
                foreach (Vector3 v in sample)
                {
                    writer.Write(v.x);
                    writer.Write(v.y);
                    writer.Write(v.z);
                }
        }
    }
}
