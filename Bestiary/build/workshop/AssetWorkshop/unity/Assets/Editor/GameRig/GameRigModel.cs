using System;
using System.IO;
using UnityEngine;

namespace Workshop.GameRig
{
    /// <summary>
    /// A body on a game skeleton as Blender writes it (blender/workshop/gamerig_export.py): the game's transforms from
    /// Visual down with their exact local values, the new sockets, the mesh in root space and Unity's axes, the
    /// material's textures, and the game assets the preview stages (never bundled).
    /// </summary>
    [Serializable]
    public class GameRigModel
    {
        public string asset;
        public string source;
        public string category;
        public string boneOrder;
        public string renderer;
        public string rootBone;
        public GameRigTransform[] transforms;
        public string[] bones;
        public string[] sockets;
        public GameRigRest[] rest;
        public GameRigMesh mesh;
        public GameRigMaterial material;
        public GameRigReference reference;
        public int triangles;

        public static GameRigModel Read(string path) => JsonUtility.FromJson<GameRigModel>(File.ReadAllText(path));
    }

    /// <summary>One transform: its path below the prefab root, its parent's path ("" for the root) and local values.</summary>
    [Serializable]
    public class GameRigTransform
    {
        public string path;
        public string name;
        public string parent;
        public string kind;          // chain, bone, socket, end (the game's) or new (a socket of ours)
        public float[] position;
        public float[] rotation;     // x, y, z, w
        public float[] scale;

        public Vector3 Position => new Vector3(position[0], position[1], position[2]);
        public Quaternion Rotation => new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
        public Vector3 Scale => new Vector3(scale[0], scale[1], scale[2]);
    }

    /// <summary>Where Blender's armature has a bone or socket at rest, root space.</summary>
    [Serializable]
    public class GameRigRest
    {
        public string name;
        public float[] position;

        public Vector3 Position => new Vector3(position[0], position[1], position[2]);
    }

    /// <summary>The body mesh: flat arrays in root space (Unity axes), four weights a vertex indexing `bones`.</summary>
    [Serializable]
    public class GameRigMesh
    {
        public float[] positions;
        public float[] normals;
        public float[] uvs;
        public int[] triangles;
        public int[] boneIndex;
        public float[] boneWeight;

        public int VertexCount => positions.Length / 3;
    }

    [Serializable]
    public class GameRigMaterial
    {
        public string name;
        public string albedo;
        public string normal;
        public string regions;
        public int textureSize;
        public float smoothness;
    }

    /// <summary>Reference-export paths of the game creature the preview stages: never part of a bundle.</summary>
    [Serializable]
    public class GameRigReference
    {
        public string prefab;
        public string animatorNode;
        public string avatar;
        public string controller;
        public string[] clips;
        public string bodyMesh;
        public string bodyMaterial;
        public string bodyAlbedo;
    }
}
