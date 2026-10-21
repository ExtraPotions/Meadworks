using UnityEditor;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Turns one staged asset folder (FBX, PNGs, manifest) into &lt;asset&gt;.prefab: the visual mesh with one material
    /// on the baked atlas, and col_* children turned into colliders. The material uses Unity's Standard shader as a
    /// carrier for the textures; the mod swaps in the game's own shader when it loads the bundle.
    /// </summary>
    public static class PrefabBuilder
    {
        public static string Build(string folder)
        {
            var info = AssetManifest.Read(folder);
            string fbx = folder + "/" + info.fbx;
            ImportSettings.Model(fbx);
            ImportSettings.Texture(folder + "/" + info.albedo, info.textureSize, false);
            if (!string.IsNullOrEmpty(info.normal))
                ImportSettings.Texture(folder + "/" + info.normal, info.textureSize, true);
            var material = CreateMaterial(folder, info);
            string path = SavePrefab(folder, info, fbx, material);
            return path;
        }

        /// <summary>The folder's &lt;asset&gt;_icon.png imported as a sprite (an item's inventory icon), or null when it has none.</summary>
        public static string Icon(string folder)
        {
            string path = folder + "/" + System.IO.Path.GetFileName(folder) + "_icon.png";
            if (!System.IO.File.Exists(path))
                return null;
            ImportSettings.Sprite(path);
            return path;
        }

        private static Material CreateMaterial(string folder, AssetManifest info)
        {
            var material = new Material(Shader.Find("Standard")) { name = info.asset };
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + info.albedo));
            material.SetFloat("_Glossiness", 0.15f);
            if (!string.IsNullOrEmpty(info.normal))
            {
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + info.normal));
                material.EnableKeyword("_NORMALMAP");
            }
            AssetDatabase.CreateAsset(material, folder + "/" + info.asset + ".mat");
            return material;
        }

        private static string SavePrefab(string folder, AssetManifest info, string fbx, Material material)
        {
            var root = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
            root.name = info.asset;
            foreach (string name in info.colliders)
                Colliders.Convert(root.transform.Find(name));
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterial = material;
            Report(root, info);
            string path = folder + "/" + info.asset + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return path;
        }

        /// <summary>Compares the imported size with Blender's, which catches unit and axis mistakes.</summary>
        private static void Report(GameObject root, AssetManifest info)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            bool match = Vector3.Distance(bounds.size, info.Size) < 0.01f;
            Log.Info($"prefab {info.asset}: size {bounds.size:F3} (Blender {info.Size:F3}) {(match ? "OK" : "MISMATCH")}, " +
                     $"center {bounds.center:F3}, {info.triangles} triangles, colliders {root.GetComponentsInChildren<Collider>().Length}");
            if (!match)
                throw new System.InvalidOperationException(info.asset + ": imported size differs from Blender's");
        }
    }
}
