using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ExtraPotions.Burrow.Embedded.SyncedConfig;
using ExtraPotions.Burrow.Embedded.YamlConfig;

namespace Burrow.Capacity
{
    /// <summary>
    /// Fills the freshly written Burrow.Stations.yml with every station prefab of the scene and its vanilla caps,
    /// all commented out, the first time a world loads. Only on the author, and only while the file on disk still
    /// equals the embedded default, so a file the player has touched is never rewritten. A cap the station does not
    /// use is left out of its line; a station using neither is not listed.
    /// </summary>
    public static class StationTemplate
    {
        private const string StationsKey = "stations:";

        public static void GenerateIfDefault()
        {
            SyncedConfiguration synced = CapacityModule.Synced;
            YamlFileSet set = StationsFile.Set;
            if (synced == null || set == null || !synced.Yaml.IsAuthor || ZNetScene.instance == null)
                return;
            string path = ContainerTemplate.MainFilePath(synced, set);
            if (!File.Exists(path))
                return;
            string template = ContainerTemplate.DefaultText(set);
            string current;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader reader = new StreamReader(stream, true))
                    current = reader.ReadToEnd();
            }
            catch (IOException error)
            {
                Plugin.Log.LogWarning($"Burrow: could not read {path}: {error.GetType().Name}");
                return;
            }
            catch (UnauthorizedAccessException error)
            {
                Plugin.Log.LogWarning($"Burrow: could not read {path}: {error.GetType().Name}");
                return;
            }
            if (template == null || Normalize(current) != Normalize(template))
                return;
            string generated = Generate(template, StationPrefabs.All());
            synced.Yaml.Replace(set, new Dictionary<string, string> { [path] = generated }, saveToDisk: true);
            Plugin.Log.LogInfo($"Burrow: listed every station prefab in {path}.");
        }

        private static string Normalize(string text) => text.TrimStart('﻿').Replace("\r\n", "\n").TrimEnd();

        /// <summary>The template up to and including the stations: line, then one commented line per prefab.</summary>
        public static string Generate(string template, Dictionary<string, Smelter> prefabs)
        {
            StringBuilder text = new StringBuilder();
            foreach (string line in Normalize(template).Split('\n'))
            {
                text.Append(line).Append('\n');
                if (line.TrimEnd() == StationsKey)
                    break;
            }
            if (!text.ToString().Contains(StationsKey + "\n"))
                text.Append(StationsKey).Append('\n');
            foreach (KeyValuePair<string, Smelter> prefab in prefabs.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                string entry = Entry(prefab.Key, VanillaCaps.Remember(prefab.Key, prefab.Value));
                if (entry != null)
                    text.Append("  # ").Append(entry).Append('\n');
            }
            return text.ToString();
        }

        /// <summary>"smelter: { items: 10, fuel: 20 }" with only the caps the station uses; null when it uses neither.</summary>
        private static string Entry(string name, StationCaps vanilla)
        {
            List<string> caps = new List<string>();
            if (vanilla.Items > 0)
                caps.Add("items: " + vanilla.Items);
            if (vanilla.Fuel > 0)
                caps.Add("fuel: " + vanilla.Fuel);
            return caps.Count == 0 ? null : name + ": { " + string.Join(", ", caps) + " }";
        }
    }
}
