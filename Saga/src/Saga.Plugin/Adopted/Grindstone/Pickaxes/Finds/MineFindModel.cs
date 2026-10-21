// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Finds/MineFindModel.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using System.Collections.Generic;
using System.Linq;
using YamlConfig;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The parsed Saga.MineFinds*.yml files: under <c>biomes:</c> a table of finds per biome, under
    /// <c>deposits:</c> a table per kind of rock (<see cref="RockInfo.Kind"/>: the prefab name without "_frac") that
    /// replaces the biome's table for that kind. A later file replaces an earlier file's table for the same biome or
    /// kind. Tables are read by <see cref="FindReader"/> and biomes by <see cref="FindModel.TryParseBiome"/>, exactly as
    /// the Woodcutting finds. Parsed on every machine; it holds only names, so it is safe before the game's databases
    /// exist.
    /// </summary>
    public sealed class MineFindModel : YamlModel
    {
        private static readonly string BiomeNames = string.Join(", ", FindModel.Biomes.Select(b => b.ToString()));

        public Dictionary<Heightmap.Biome, List<FindEntry>> ByBiome { get; } = new Dictionary<Heightmap.Biome, List<FindEntry>>();

        /// <summary>Tables by rock kind, case-insensitive; a key written with "_frac" is stored as its kind.</summary>
        public Dictionary<string, List<FindEntry>> ByDeposit { get; } = new Dictionary<string, List<FindEntry>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>How many finds all tables hold together.</summary>
        public int Count => ByBiome.Values.Sum(t => t.Count) + ByDeposit.Values.Sum(t => t.Count);

        protected override void Read(YamlNode root)
        {
            foreach (KeyValuePair<string, YamlNode> entry in MapOf(root.Get("biomes")))
                ReadBiome(entry.Key, entry.Value);
            foreach (KeyValuePair<string, YamlNode> entry in MapOf(root.Get("deposits")))
                ReadDeposit(entry.Key.Trim(), entry.Value);
        }

        protected override void Verify()
        {
            if (Errors.Count == 0 && Count == 0)
                Warnings.Add("no finds are listed under 'biomes' or 'deposits'; broken rock hides nothing");
        }

        private void ReadBiome(string name, YamlNode node)
        {
            if (!FindModel.TryParseBiome(name, out Heightmap.Biome biome))
            {
                node.Error($"'{name}' is not a biome; use one of {BiomeNames}");
                return;
            }
            ByBiome[biome] = FindReader.ReadTable(node);
        }

        /// <summary>"rock4_copper_frac" and "rock4_copper" are one kind, so either spelling of the key works.</summary>
        private void ReadDeposit(string kind, YamlNode node)
        {
            if (kind.Length == 0)
            {
                node.Error("a deposit needs its kind as the key, for example 'rock4_copper:'");
                return;
            }
            ByDeposit[RockCatalog.KindOf(kind)] = FindReader.ReadTable(node);
        }

        /// <summary>The entries of a map; nothing for a missing key or a key without a value.</summary>
        private static IReadOnlyList<KeyValuePair<string, YamlNode>> MapOf(YamlNode node) =>
            node.Kind == YamlNodeKind.Missing || node.Kind == YamlNodeKind.Null
                ? Array.Empty<KeyValuePair<string, YamlNode>>()
                : node.Entries;
    }
}
