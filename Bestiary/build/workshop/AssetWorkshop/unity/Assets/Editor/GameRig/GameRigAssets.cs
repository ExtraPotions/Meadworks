using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Workshop.GameRig
{
    /// <summary>
    /// The body's assets in its staging folder: the baked textures with the game's own import settings for creature
    /// textures (point filtered, mipmapped), the placeholder material (Standard, carrying the baked albedo and normal
    /// map, as BundlePrefabs.GameMaterials.Dress expects of workshop models), and the mesh built from the contract,
    /// moved into the renderer's own space and bound to the skeleton as it stands (the rest pose).
    /// </summary>
    public static class GameRigAssets
    {
        public static Texture2D Texture(string path, int size, bool normal)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Point;
            importer.maxTextureSize = Mathf.Max(32, size);
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Material Material(string folder, GameRigMaterial spec)
        {
            var material = new Material(Shader.Find("Standard")) { name = spec.name };
            material.SetTexture("_MainTex", Texture(folder + "/" + spec.albedo, spec.textureSize, false));
            material.SetFloat("_Glossiness", spec.smoothness);
            if (!string.IsNullOrEmpty(spec.normal))
            {
                material.SetTexture("_BumpMap", Texture(folder + "/" + spec.normal, spec.textureSize, true));
                material.EnableKeyword("_NORMALMAP");
            }
            AssetDatabase.CreateAsset(material, folder + "/" + spec.name + ".mat");
            return material;
        }

        /// <summary>A regions mask (flat colours, one per paint region) kept exact: no filtering, no compression.</summary>
        public static Texture2D Mask(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>The body mesh, its vertices moved from root space into `skin`'s space, bound to `bones` at rest.</summary>
        public static Mesh Mesh(string folder, GameRigModel model, Transform[] bones, Transform skin)
        {
            GameRigMesh data = model.mesh;
            int count = data.VertexCount;
            var mesh = new Mesh { name = model.asset + "_body", indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            Matrix4x4 into = skin.worldToLocalMatrix, normals = into.inverse.transpose;
            mesh.vertices = Points(data.positions, count, v => into.MultiplyPoint3x4(v));
            mesh.normals = Points(data.normals, count, n => normals.MultiplyVector(n).normalized);
            mesh.uv = Uvs(data.uvs, count);
            mesh.triangles = data.triangles;
            mesh.boneWeights = Weights(data, count);
            mesh.bindposes = BindPoses(bones, skin);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, folder + "/" + mesh.name + ".asset");
            return mesh;
        }

        /// <summary>Each bone's bind pose: from the renderer's space into the bone's, as they stand now.</summary>
        public static Matrix4x4[] BindPoses(Transform[] bones, Transform skin)
        {
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                bind[i] = bones[i].worldToLocalMatrix * skin.localToWorldMatrix;
            return bind;
        }

        private static Vector3[] Points(float[] flat, int count, System.Func<Vector3, Vector3> map)
        {
            var result = new Vector3[count];
            for (int i = 0; i < count; i++)
                result[i] = map(new Vector3(flat[3 * i], flat[3 * i + 1], flat[3 * i + 2]));
            return result;
        }

        private static Vector2[] Uvs(float[] flat, int count)
        {
            var result = new Vector2[count];
            for (int i = 0; i < count; i++)
                result[i] = new Vector2(flat[2 * i], flat[2 * i + 1]);
            return result;
        }

        private static BoneWeight[] Weights(GameRigMesh data, int count)
        {
            var result = new BoneWeight[count];
            int[] b = data.boneIndex;
            float[] w = data.boneWeight;
            for (int i = 0, k = 0; i < count; i++, k += 4)
            {
                result[i] = new BoneWeight
                {
                    boneIndex0 = b[k], boneIndex1 = b[k + 1], boneIndex2 = b[k + 2], boneIndex3 = b[k + 3],
                    weight0 = w[k], weight1 = w[k + 1], weight2 = w[k + 2], weight3 = w[k + 3],
                };
            }
            return result;
        }
    }
}
