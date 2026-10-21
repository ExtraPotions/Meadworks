// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using ExtraPotions.Wildblood.Adopted.Rules;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Wildblood.AdoptedCm;

/// <summary>Pressure uses the same validated authority snapshot as stars and modifiers.</summary>
internal static class NativeKarmaRuleAuthority
{
    internal static IDisposable Register(NativeRuleAuthority authority) =>
        authority.RegisterSection("karma and enforcers", Prepare, CreatureKarmaManager.CommitParsedConfiguration,
            DefaultSection());

    internal static CreatureKarmaManager.ParsedConfiguration Prepare(string text)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root)
            throw new InvalidDataException("One rule mapping is required.");
        CreatureYaml.ValidateUniqueMappingKeys(root, "Wildblood rules", "root");
        var selected = new YamlMappingNode();
        foreach (var row in root.Children)
        {
            if (row.Key is not YamlScalarNode name) continue;
            bool enforcer = Is(row.Key, "enforcers") || Is(row.Key, "Enforcer");
            if (!enforcer && !Is(row.Key, "karma") && !Is(row.Key, "Global") &&
                !CreatureKarmaManager.TryResolveBiomeName(name.Value ?? "", out _)) continue;
            var target = enforcer ? new YamlScalarNode("Enforcer") : row.Key;
            if (Find(selected, ((YamlScalarNode)target).Value ?? "") != null)
                throw new InvalidDataException("Duplicate pressure scope after Enforcer alias resolution.");
            selected.Add(target, Project(row.Value));
        }
        // The unified global Enforcer tuples are the source fallback, below explicit enforcers.modifiers.
        var powers = Find(root, "modifier power") as YamlMappingNode;
        var enforcerPower = powers == null ? null : Find(powers, "Enforcer");
        if (enforcerPower != null)
        {
            var enforcers = Find(selected, "Enforcer") as YamlMappingNode;
            if (enforcers == null) { enforcers = new YamlMappingNode(); selected.Add("Enforcer", enforcers); }
            if (Find(enforcers, "modifiers") == null) enforcers.Add("modifiers", NativeModifierRuleAuthority.ModifierNames(enforcerPower));
        }
        string source;
        if (selected.Children.Count == 0) source = ""; // actual source default settings, not synthesized probabilities
        else
        {
            var writer = new StringWriter();
            new YamlStream(new YamlDocument(selected)).Save(writer, false);
            source = writer.ToString();
        }
        if (!CreatureKarmaManager.TryParseYaml(source, "Wildblood rules", out var parsed))
            throw new InvalidDataException("Invalid Karma or Enforcer rules; previous settings retained.");
        return parsed;
    }

    // Retain the source biome/Global candidate tables; only the public root name differs.
    internal static string DefaultSection() => CreatureKarmaManager.BuildDefaultYaml().Replace("\nEnforcer:\n", "\nenforcers:\n");
    static YamlNode Project(YamlNode node)
    {
        if (node is YamlMappingNode map)
        {
            var result = new YamlMappingNode();
            foreach (var row in map.Children)
                result.Add(row.Key, Is(row.Key, "modifiers") ? NativeModifierRuleAuthority.ModifierNames(row.Value) : Project(row.Value));
            return result;
        }
        if (node is YamlSequenceNode sequence) return new YamlSequenceNode(sequence.Children.Select(Project));
        return node;
    }

    static bool Is(YamlNode node, string name) => node is YamlScalarNode scalar &&
        string.Equals(scalar.Value, name, StringComparison.OrdinalIgnoreCase);
    static YamlNode? Find(YamlMappingNode root, string name) =>
        root.Children.Where(row => Is(row.Key, name)).Select(row => row.Value).FirstOrDefault();
}
