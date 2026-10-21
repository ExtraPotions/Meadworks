using System;
using System.IO;
using UnityEngine;

namespace Workshop.Kraken
{
    /// <summary>
    /// One kraken piece as Blender writes it (assets/ecp_kraken/bake_export.py): the rig and markers at their rest
    /// positions and the skinned meshes, all in Unity's axes and metres. Rest rotations are identity throughout.
    /// </summary>
    [Serializable]
    public class KrakenModel
    {
        public string asset;
        public KrakenJoint[] bones;
        public KrakenJoint[] markers;
        public KrakenPart[] parts;

        public static KrakenModel Read(string path) => JsonUtility.FromJson<KrakenModel>(File.ReadAllText(path));
    }

    [Serializable]
    public class KrakenJoint
    {
        public string name;
        public string parent;
        public float[] position;

        public Vector3 Position => new Vector3(position[0], position[1], position[2]);
    }

    /// <summary>A SkinnedMeshRenderer's mesh: flat arrays, four bone weights per vertex (indices into the model's bones).</summary>
    [Serializable]
    public class KrakenPart
    {
        public string name;
        public string material;
        public string albedo;
        public string normal;
        public int textureSize;
        public int normalSize;
        public float smoothness;
        public float[] positions;
        public float[] normals;
        public float[] uvs;
        public int[] triangles;
        public int[] boneIndex;
        public float[] boneWeight;

        public int VertexCount => positions.Length / 3;
    }
}
