// Adopted from OpenKeep 2.0.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: OpenKeep/OpenKeep/src/Capacity/ContainerTemplate.cs at 31d8ee35ac4ccfd14c83aa930e4b602ac4dfa230.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026-10-10 (parity choice #8): the listing keeps the template's chests: line and says which prefabs it sizes.
// Changed by expDARE, 2026-10-16: append missing prefab comments without rewriting existing YAML or companion files.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using ExtraPotions.Burrow.Embedded.SyncedConfig;
using ExtraPotions.Burrow.Embedded.YamlConfig;
using ExtraPotions.Burrow;

namespace Burrow.Capacity
{
    /// <summary>Lists newly discovered containers as comments; existing bytes and size choices stay intact.</summary>
    public static class ContainerTemplate
    {
        private static readonly IDeserializer parser = new DeserializerBuilder().Build();
        private static readonly Regex commentedKey = new Regex(@"^\s*#\s*(""(?:\\.|[^""])*""|'(?:''|[^'])*'|[^:#\r\n]+)\s*:");

        public static void GenerateIfDefault()
        {
            SyncedConfiguration synced = CapacityModule.Synced;
            YamlFileSet set = CapacityModule.Set;
            if (synced == null || set == null || !synced.Yaml.IsAuthor || ZNetScene.instance == null)
                return;
            string path = MainFilePath(synced, set);
            if (!File.Exists(path))
                return;
            try
            {
                var originals = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Encoding encoding = null;
                var paths = set.Files.Keys.Concat(Directory.GetFiles(Path.GetDirectoryName(path) ?? ".", set.FilePattern)).Append(path);
                foreach (string file in paths.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    byte[] bytes = File.ReadAllBytes(file);
                    using (var reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false, true), true))
                    {
                        files[file] = reader.ReadToEnd();
                        if (string.Equals(file, path, StringComparison.OrdinalIgnoreCase)) encoding = reader.CurrentEncoding;
                    }
                    originals[file] = bytes;
                }
                if (!synced.Yaml.TryBuild(set, files, out _, "container discovery")) return;
                string current = files[path];
                string generated = Generate(current, ContainerPrefabs.All(), files.Values);
                if (generated == current) return;
                files[path] = generated;
                if (!synced.Yaml.TryBuild(set, files, out _, "container discovery")) return;
                byte[] suffix = encoding.GetBytes(generated.Substring(current.Length));
                if (!AppendUnchanged(path, originals, suffix)) return;
                synced.Yaml.Replace(set, files, saveToDisk: false);
                Plugin.Log.LogInfo($"Burrow: appended newly discovered container prefabs to {path}.");
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("Burrow: container discovery failed: " + error.Message);
            }
        }

        internal static bool AppendUnchanged(string path, IReadOnlyDictionary<string, byte[]> originals, byte[] suffix)
        {
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            bool created = false;
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true;
                    byte[] original = originals[path];
                    stream.Write(original, 0, original.Length);
                    stream.Write(suffix, 0, suffix.Length);
                    stream.Flush(true);
                }
                // A pending editor save must win over discovery's earlier snapshot.
                foreach (var file in originals)
                    if (!File.ReadAllBytes(file.Key).SequenceEqual(file.Value)) return false;
                File.Replace(temp, path, null);
                return true;
            }
            finally
            {
                if (created && File.Exists(temp)) File.Delete(temp);
            }
        }

        internal static string MainFilePath(SyncedConfiguration synced, YamlFileSet set)
        {
            string known = set.Files.Keys.FirstOrDefault(k => string.Equals(Path.GetFileName(k), set.MainFileName, StringComparison.OrdinalIgnoreCase));
            return known ?? Path.Combine(Path.GetDirectoryName(synced.Config.ConfigFilePath) ?? "", set.MainFileName);
        }

        internal static string DefaultText(YamlFileSet set)
        {
            byte[] bytes = set.DefaultContent?.Invoke();
            return bytes != null ? new UTF8Encoding(false).GetString(bytes) : null;
        }

        public static string Generate(string template, Dictionary<string, Container> prefabs) => Generate(template, prefabs, new[] { template });

        internal static string Generate(string template, Dictionary<string, Container> prefabs, IEnumerable<string> contents)
        {
            string[] files = contents.ToArray();
            var model = new ContainersModel();
            model.LoadAll(files.Select((text, index) => new KeyValuePair<string, string>(index.ToString(), text)));
            if (model.Errors.Count > 0) return template;
            var known = new HashSet<string>(model.Specs.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
                foreach (string line in file.Split('\n', '\r'))
                {
                    Match match = commentedKey.Match(line);
                    if (!match.Success) continue;
                    try { known.Add(parser.Deserialize<string>(match.Groups[1].Value.Trim())); }
                    catch (YamlException) { }
                }
            int firstNewline = template.IndexOf('\n');
            string newline = firstNewline > 0 && template[firstNewline - 1] == '\r' ? "\r\n" : template.Contains("\r") ? "\r" : "\n";
            StringBuilder appended = new StringBuilder();
            HashSet<string> chests = ContainerSizes.BuildableChests(prefabs);
            foreach (KeyValuePair<string, Container> prefab in prefabs.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!known.Add(prefab.Key)) continue;
                ContainerSize vanilla = VanillaSizes.Remember(prefab.Key, prefab.Value);
                appended.Append("  # ").Append(QuoteKey(prefab.Key)).Append(": { width: ").Append(vanilla.Width).Append(", height: ").Append(vanilla.Height).Append(" }");
                if (chests.Contains(prefab.Key))
                    appended.Append("   # a buildable chest: sized by the ").Append(ContainerSizeDefaults.ChestsKey).Append(": line unless listed here");
                appended.Append(newline);
            }
            if (appended.Length == 0) return template;
            return template + (template.Length > 0 && !template.EndsWith("\n") && !template.EndsWith("\r") ? newline : "") + appended;
        }

        private static string QuoteKey(string name)
        {
            if (name.Length > 0 && name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')) return name;
            var quoted = new StringBuilder("\"");
            foreach (char c in name)
            {
                if (c == '"' || c == '\\') quoted.Append('\\').Append(c);
                else if (char.IsControl(c)) quoted.Append("\\u").Append(((int)c).ToString("x4"));
                else quoted.Append(c);
            }
            return quoted.Append('"').ToString();
        }
    }
}
