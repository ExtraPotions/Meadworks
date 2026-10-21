using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Brings single files from the local reference export of the game (rip-reference.ps1) into Assets/Reference, so
    /// previews in this project can show the game's own chest and textures. Assets/Reference is gitignored and never
    /// goes into a bundle: the mod borrows the same assets from the running game instead.
    /// </summary>
    public static class ReferenceAssets
    {
        public const string Folder = "Assets/Reference";

        private static string Root => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ValheimReference", "ExportedProject", "Assets");

        /// <summary>
        /// Copies the file and its .meta (keeping the game's GUID) and returns its project path. `subfolder` keeps one
        /// creature's files apart where their names collide with another's (the Troll's Throw.anim, the Greydwarf's).
        /// </summary>
        public static string Import(string referencePath, string subfolder = null)
        {
            string source = Path.Combine(Root, referencePath);
            if (!File.Exists(source))
                throw new FileNotFoundException("not in the reference export; run rip-reference.ps1", source);
            string folder = subfolder == null ? Folder : Folder + "/" + subfolder;
            Directory.CreateDirectory(folder);
            string target = folder + "/" + Path.GetFileName(referencePath);
            File.Copy(source, target, true);
            if (File.Exists(source + ".meta"))
                File.Copy(source + ".meta", target + ".meta", true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            return target;
        }

        /// <summary>A game texture, point-filtered like the game, imported as a normal map when asked.</summary>
        public static Texture2D Texture(string referencePath, bool normal)
        {
            string path = Import(referencePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Mesh Mesh(string referencePath) => AssetDatabase.LoadAssetAtPath<Mesh>(Import(referencePath));

        /// <summary>One of the game's fonts, for preview HUDs; null if it is not in the reference export.</summary>
        public static Font Font(string referencePath)
        {
            try
            {
                return AssetDatabase.LoadAssetAtPath<Font>(Import(referencePath));
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }
    }
}
