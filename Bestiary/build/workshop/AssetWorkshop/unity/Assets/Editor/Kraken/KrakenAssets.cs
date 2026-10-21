using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Workshop.Kraken
{
    /// <summary>
    /// The kraken's assets in the staging folder: textures with their import settings, the placeholder materials
    /// (Standard shader carrying the baked albedo and normal map, as the mod expects of workshop models) and the meshes
    /// built from the JSON, bound to the prefab's bones.
    /// </summary>
    public static class KrakenAssets
    {
        public static Texture2D Texture(string path, int size, bool normal)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>A screen overlay: sRGB, straight alpha, no mipmaps, clamped.</summary>
        public static Texture2D Overlay(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Material Material(string folder, KrakenPart part)
        {
            var material = new Material(Shader.Find("Standard")) { name = part.material };
            material.SetTexture("_MainTex", Texture(folder + "/" + part.albedo, part.textureSize, false));
            material.SetFloat("_Glossiness", part.smoothness);
            if (!string.IsNullOrEmpty(part.normal))
            {
                material.SetTexture("_BumpMap", Texture(folder + "/" + part.normal, part.normalSize, true));
                material.EnableKeyword("_NORMALMAP");
            }
            AssetDatabase.CreateAsset(material, folder + "/" + part.material + ".mat");
            return material;
        }

        /// <summary>The part's mesh, bound to `bones` as they stand now (the rest pose) relative to `skin`.</summary>
        public static Mesh Mesh(string folder, string asset, KrakenPart part, Transform[] bones, Transform skin)
        {
            int count = part.VertexCount;
            var mesh = new Mesh { name = asset + "_" + part.name, indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = Vectors(part.positions, count);
            mesh.normals = Vectors(part.normals, count);
            var uv = new Vector2[count];
            for (int i = 0; i < count; i++)
                uv[i] = new Vector2(part.uvs[2 * i], part.uvs[2 * i + 1]);
            mesh.uv = uv;
            mesh.triangles = part.triangles;
            mesh.boneWeights = Weights(part, count);
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                bind[i] = bones[i].worldToLocalMatrix * skin.localToWorldMatrix;
            mesh.bindposes = bind;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, folder + "/" + mesh.name + ".asset");
            return mesh;
        }

        private static Vector3[] Vectors(float[] flat, int count)
        {
            var result = new Vector3[count];
            for (int i = 0; i < count; i++)
                result[i] = new Vector3(flat[3 * i], flat[3 * i + 1], flat[3 * i + 2]);
            return result;
        }

        private static BoneWeight[] Weights(KrakenPart part, int count)
        {
            var result = new BoneWeight[count];
            for (int i = 0; i < count; i++)
            {
                int[] b = part.boneIndex;
                float[] w = part.boneWeight;
                int k = 4 * i;
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
