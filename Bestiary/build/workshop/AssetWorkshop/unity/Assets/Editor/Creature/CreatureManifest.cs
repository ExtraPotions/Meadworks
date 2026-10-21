using System;
using System.IO;
using UnityEngine;

namespace Workshop
{
    /// <summary>A creature's &lt;name&gt;.json from its Blender export script (e.g. assets/crypt_mimic/export_unity.py).</summary>
    [Serializable]
    public class CreatureManifest
    {
        public string asset;
        public string fbx;
        public string rig;
        public string rootBone;
        public int fps;
        public ClipInfo[] clips;
        public MaterialInfo[] materials;

        public static CreatureManifest Read(string folder)
        {
            string json = File.ReadAllText(Path.Combine(folder, Path.GetFileName(folder) + ".json"));
            return JsonUtility.FromJson<CreatureManifest>(json);
        }
    }

    /// <summary>One animation: its last frame, whether it loops, the game's state tag, and the frames a bite lands on.</summary>
    [Serializable]
    public class ClipInfo
    {
        public string name;
        public int last;
        public bool loop;
        public string tag;
        public int[] events;
    }

    /// <summary>
    /// A part material as sRGB colour, smoothness and optional glow, and for parts wearing the game's textures, their
    /// paths in the reference export (albedo, normal); empty when the part is a plain colour.
    /// </summary>
    [Serializable]
    public class MaterialInfo
    {
        public string name;
        public float[] color;
        public float smoothness;
        public float[] emission;
        public float emissionStrength;
        public string albedo;
        public string normal;

        public Color Color => new Color(color[0], color[1], color[2]);
        public Color Emission => new Color(emission[0], emission[1], emission[2]) * emissionStrength;
    }
}
