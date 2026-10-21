using System;
using System.IO;
using UnityEngine;

namespace Workshop
{
    /// <summary>The asset's &lt;name&gt;.json written by the Blender pipeline (blender/workshop/pipeline.py).</summary>
    [Serializable]
    public class AssetManifest
    {
        public string asset;
        public string fbx;
        public string visual;
        public string albedo;
        public string normal;
        public int textureSize;
        public string[] colliders;
        public float[] size;
        public int triangles;

        public static AssetManifest Read(string folder)
        {
            string name = Path.GetFileName(folder);
            string json = File.ReadAllText(Path.Combine(folder, name + ".json"));
            return JsonUtility.FromJson<AssetManifest>(json);
        }

        public Vector3 Size => new Vector3(size[0], size[1], size[2]);
    }
}
