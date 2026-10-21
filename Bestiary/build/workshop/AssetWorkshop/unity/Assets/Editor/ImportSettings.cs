using UnityEditor;
using UnityEngine;

namespace Workshop
{
    /// <summary>Importer settings for the Blender output: metres in, no FBX materials, compressed textures.</summary>
    public static class ImportSettings
    {
        public static void Model(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.preserveHierarchy = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        public static void Texture(string path, int size, bool normal)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        /// <summary>An inventory icon: a single sprite, transparent, uncompressed, no mipmaps (the UI draws it at one size).</summary>
        public static void Sprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    /// <summary>
    /// col_box_* children become a BoxCollider fitted to their mesh, col_mesh_* a MeshCollider and col_convex_* a
    /// convex one; their renderer goes, so they are invisible in game.
    /// </summary>
    public static class Colliders
    {
        public static void Convert(Transform part)
        {
            if (part == null)
                throw new System.InvalidOperationException("a collider named in the manifest is missing from the FBX");
            var filter = part.GetComponent<MeshFilter>();
            Mesh mesh = filter.sharedMesh;
            if (part.name.StartsWith("col_box_"))
            {
                var box = part.gameObject.AddComponent<BoxCollider>();
                box.center = mesh.bounds.center;
                box.size = mesh.bounds.size;
            }
            else
            {
                var collider = part.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = part.name.StartsWith("col_convex_");
            }
            Object.DestroyImmediate(part.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(filter);
        }
    }
}
