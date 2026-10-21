// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Finds/FindModel.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using System.Collections.Generic;
using System.Linq;
using YamlConfig;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The parsed Saga.Finds*.yml files: under <c>biomes:</c> a table of finds per biome, under
    /// <c>trees:</c> a table per tree prefab that replaces the biome's table for that tree. A later file replaces an
    /// earlier file's table for the same biome or tree. Tables are read by <see cref="FindReader"/>.
    /// </summary>
    public sealed class FindModel : YamlModel
    {
        /// <summary>The biomes a table can be given for, as Heightmap.Biome spells them.</summary>
        public static readonly Heightmap.Biome[] Biomes =
        {
            Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest, Heightmap.Biome.Swamp, Heightmap.Biome.Mountain,
            Heightmap.Biome.Plains, Heightmap.Biome.Mistlands, Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth,
            Heightmap.Biome.Ocean,
        };

        private static readonly string BiomeNames = string.Join(", ", Biomes.Select(b => b.ToString()));

        public Dictionary<Heightmap.Biome, List<FindEntry>> ByBiome { get; } = new Dictionary<Heightmap.Biome, List<FindEntry>>();

        public Dictionary<string, List<FindEntry>> ByTree { get; } = new Dictionary<string, List<FindEntry>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>How many finds all tables hold together.</summary>
        public int Count => ByBiome.Values.Sum(t => t.Count) + ByTree.Values.Sum(t => t.Count);

        protected override void Read(YamlNode root)
        {
            foreach (KeyValuePair<string, YamlNode> entry in MapOf(root.Get("biomes")))
                ReadBiome(entry.Key, entry.Value);
            foreach (KeyValuePair<string, YamlNode> entry in MapOf(root.Get("trees")))
                ReadTree(entry.Key.Trim(), entry.Value);
        }

        protected override void Verify()
        {
            if (Errors.Count == 0 && Count == 0)
                Warnings.Add("no finds are listed under 'biomes' or 'trees'; felled trees hide nothing");
        }

        /// <summary>A biome by name, case-insensitive, spaces ignored ("Black Forest" is BlackForest).</summary>
        public static bool TryParseBiome(string name, out Heightmap.Biome biome)
        {
            string wanted = (name ?? "").Replace(" ", "");
            biome = Biomes.FirstOrDefault(b => string.Equals(b.ToString(), wanted, StringComparison.OrdinalIgnoreCase));
            return biome != Heightmap.Biome.None;
        }

        private void ReadBiome(string name, YamlNode node)
        {
            if (!TryParseBiome(name, out Heightmap.Biome biome))
            {
                node.Error($"'{name}' is not a biome; use one of {BiomeNames}");
                return;
            }
            ByBiome[biome] = FindReader.ReadTable(node);
        }

        private void ReadTree(string prefab, YamlNode node)
        {
            if (prefab.Length == 0)
            {
                node.Error("a tree needs its prefab name as the key, for example 'Oak1:'");
                return;
            }
            ByTree[prefab] = FindReader.ReadTable(node);
        }

        /// <summary>The entries of a map; nothing for a missing key or a key without a value.</summary>
        private static IReadOnlyList<KeyValuePair<string, YamlNode>> MapOf(YamlNode node) =>
            node.Kind == YamlNodeKind.Missing || node.Kind == YamlNodeKind.Null
                ? Array.Empty<KeyValuePair<string, YamlNode>>()
                : node.Entries;
    }
}
