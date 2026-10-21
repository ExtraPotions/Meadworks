using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Workshop
{
    /// <summary>
    /// One of the game's animator controllers from the reference export with every file it names by GUID (its clips and
    /// avatar masks), each copied with its .meta so the GUIDs still match, then imported in one refresh. A file whose GUID
    /// the project already has (brought in before by <see cref="ReferenceAssets"/>) is not copied again. The copies go
    /// under Assets/Reference/&lt;subfolder&gt;/&lt;their folder in the export&gt;, so same-named clips stay apart. Preview
    /// only: Assets/Reference never goes into a bundle.
    /// </summary>
    public static class ReferenceController
    {
        private static readonly string[] Scanned = { "Characters", "3rd party" };
        private static readonly Regex Guid = new Regex("guid: ([0-9a-f]{32})");

        private static string Root => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ValheimReference", "ExportedProject", "Assets");

        /// <summary>The controller's project path, with its clips and masks in the project.</summary>
        public static string Import(string referencePath, string subfolder)
        {
            string source = Path.Combine(Root, referencePath);
            var wanted = new HashSet<string>(Guid.Matches(File.ReadAllText(source)).Cast<Match>().Select(m => m.Groups[1].Value)
                .Where(g => string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(g))));
            int copied = 0;
            foreach (string meta in Scanned.SelectMany(d => Directory.EnumerateFiles(Path.Combine(Root, d), "*.meta", SearchOption.AllDirectories)))
            {
                if (wanted.Count > 0 && wanted.Remove(MetaGuid(meta)) && !Directory.Exists(meta.Substring(0, meta.Length - 5)))
                {
                    Copy(meta.Substring(0, meta.Length - 5), subfolder);
                    copied++;
                }
            }
            string target = Copy(source, subfolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Log.Info($"reference controller {referencePath}: {copied} files brought in, {wanted.Count} named files not found");
            return target;
        }

        private static string MetaGuid(string meta)
        {
            using (var reader = new StreamReader(meta))
            {
                for (int i = 0; i < 4 && !reader.EndOfStream; i++)
                {
                    Match match = Guid.Match(reader.ReadLine() ?? "");
                    if (match.Success)
                        return match.Groups[1].Value;
                }
            }
            return "";
        }

        /// <summary>The file and its .meta into the project, under its export folder; its project path.</summary>
        private static string Copy(string source, string subfolder)
        {
            string relative = Path.GetDirectoryName(source.Substring(Root.Length + 1)) ?? "";
            string folder = $"{ReferenceAssets.Folder}/{subfolder}/{relative.Replace('\\', '_').Replace('/', '_').Replace(' ', '_')}";
            Directory.CreateDirectory(folder);
            string target = folder + "/" + Path.GetFileName(source);
            File.Copy(source, target, true);
            if (File.Exists(source + ".meta"))
                File.Copy(source + ".meta", target + ".meta", true);
            return target;
        }
    }
}
