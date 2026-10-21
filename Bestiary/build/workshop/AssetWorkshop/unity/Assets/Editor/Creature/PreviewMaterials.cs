using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Puts the game's textures on the creature in the preview scene only: each renderer's bare material is swapped, on
    /// the scene's copy, for a textured one built from the reference export (point-filtered like the game). The prefab
    /// and its model keep their bare materials, so nothing here can reach a bundle.
    /// </summary>
    public static class PreviewMaterials
    {
        public static void Dress(GameObject creature, CreatureManifest info)
        {
            foreach (var renderer in creature.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.StartsWith("chest_"))
                    continue;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Textured(m, info)).ToArray();
            }
        }

        private static Material Textured(Material bare, CreatureManifest info)
        {
            var part = info.materials.FirstOrDefault(m => m.name == bare.name);
            if (part == null || string.IsNullOrEmpty(part.albedo))
                return bare;
            string path = ReferenceAssets.Folder + "/" + part.name + "_preview.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            var material = new Material(bare) { color = Color.white };
            material.SetTexture("_MainTex", ReferenceAssets.Texture(part.albedo, false));
            if (!string.IsNullOrEmpty(part.normal))
            {
                material.SetTexture("_BumpMap", ReferenceAssets.Texture(part.normal, true));
                material.EnableKeyword("_NORMALMAP");
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
