// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Unified-file adapter around CreatureManager 1.2.5 level/modifier parsing and source layers.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;
using ExtraPotions.Wildblood.Adopted.Rules;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using RootPlugin = ExtraPotions.Wildblood.Runtime.Plugin;
namespace ExtraPotions.Wildblood.AdoptedCm;

internal static class NativeModifierRules
{
    static long generation = -1;
    static bool committed;
    internal static bool Ready => committed && generation == CruciblePlugin.Session.Generation && RootPlugin.NativeRulesReady;
    internal static Dictionary<string, ModifierDefinition> EnforcerModifiers { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    internal static bool EnforcerModifiersCleared { get; private set; }
    internal static void Commit(NativeModifierRuleAuthority.Snapshot snapshot)
    {
        CreatureLevelManager.Load(snapshot.Definitions);
        EnforcerModifiers = snapshot.EnforcerModifiers;
        EnforcerModifiersCleared = snapshot.EnforcerModifiersCleared;
        generation = CruciblePlugin.Session.Generation;
        committed = true;
    }
    internal static void Invalidate()
    {
        committed = false; generation = -1;
        CreatureLevelManager.Load(new List<LevelDefinition>());
        EnforcerModifiers = new Dictionary<string, ModifierDefinition>(StringComparer.OrdinalIgnoreCase);
        EnforcerModifiersCleared = false;
    }
}

// The owning NativeRuleAuthority controls one disk file, C01 carrier and AS05 lease.
// Every section is prepared successfully before any section is committed.
internal static class NativeModifierRuleAuthority
{
    internal sealed class Snapshot
    {
        internal readonly List<LevelDefinition> Definitions;
        internal readonly Dictionary<string, ModifierDefinition> EnforcerModifiers;
        internal readonly bool EnforcerModifiersCleared;
        internal Snapshot(List<LevelDefinition> definitions, Dictionary<string, ModifierDefinition> enforcer, bool cleared)
        { Definitions = definitions; EnforcerModifiers = enforcer; EnforcerModifiersCleared = cleared; }
    }
    static readonly HashSet<string> LevelFields = new(StringComparer.OrdinalIgnoreCase)
    { "level", "damage", "damagePerLevel", "health", "healthPerLevel", "scalePerLevel", "distanceScaling", "modifierDistanceScaling", "modifiers" };
    internal static IDisposable Register(NativeRuleAuthority authority)
    {
        var section = authority.RegisterSection("modifier power", Prepare, NativeModifierRules.Commit, BuildDefaultSectionYaml());
        NativeModifierRules.Invalidate();
        return new Lease(section);
    }
    sealed class Lease : IDisposable
    {
        IDisposable? section;
        internal Lease(IDisposable section) { this.section = section; }
        public void Dispose()
        {
            var owned = section; section = null;
            if (owned == null) return;
            owned.Dispose(); NativeModifierRules.Invalidate();
        }
    }
    internal static Snapshot Prepare(string fullText)
    {
        var root = Mapping(fullText, "rules.yml");
        var defaults = ParseSource(NativeModifierDefaults.BuildDefaultLevelOverrideYaml(), "pinned CM defaults");
        var definitions = new List<LevelDefinition>(defaults);
        var enforcer = ReadEnforcer(DefaultEnforcerNode(), "pinned CM Enforcer defaults", out _);
        bool enforcerCleared = false;
        if (Child(root, "modifier power") is YamlNode powerNode)
        {
            if (powerNode is not YamlMappingNode power) throw Error(powerNode, "modifier power must be a mapping.");
            foreach (var entry in power.Children)
            {
                string target = Scalar(entry.Key, "modifier power scope");
                if (!new[] { "Global", "Boss", "Enforcer" }.Contains(target, StringComparer.OrdinalIgnoreCase))
                    throw Error(entry.Key, "modifier power supports Global, Boss and Enforcer.");
                if (target.Equals("Enforcer", StringComparison.OrdinalIgnoreCase))
                {
                    var overrides = ReadEnforcer(ModifierNames(entry.Value), "modifier power.Enforcer", out bool cleared);
                    if (cleared) { enforcer.Clear(); enforcerCleared = true; }
                    Overlay(enforcer, overrides);
                }
                else definitions.AddRange(ParseTarget(target, new YamlMappingNode { { "modifiers", ModifierNames(entry.Value) } }, "modifier power." + target));
            }
        }
        var groupMembers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (Child(root, "groups") is YamlNode groupsNode)
        {
            if (groupsNode is not YamlMappingNode groups) throw Error(groupsNode, "groups must be a named mapping.");
            foreach (var group in groups.Children)
            {
                string target = Scalar(group.Key, "group name");
                var row = group.Value as YamlMappingNode;
                var members = group.Value as YamlSequenceNode ?? (row == null ? null : Child(row, "prefabs") as YamlSequenceNode);
                if (members == null) throw Error(group.Value, "a modifier group needs a prefabs list.");
                var names = members.Children.Select(member => Scalar(member, "group prefab")).ToList();
                if (names.Count == 0) throw Error(members, "a modifier group needs at least one prefab.");
                groupMembers.Add(target, names);
                if (row != null) definitions.AddRange(ParseTarget(target, Project(row), "groups." + target));
            }
        }
        ReadScopes(root, "biomes", definitions);
        ReadScopes(root, "creatures", definitions);
        foreach (var definition in definitions)
            if (definition.Target != null && groupMembers.TryGetValue(definition.Target, out var prefabs)) definition.Prefabs = prefabs;
        if (!CreatureYaml.ValidateDefinitions(definitions, "rules.yml modifier power")) throw new InvalidDataException("Invalid modifier definitions; see the named source parser diagnostic.");
        return new Snapshot(definitions, enforcer, enforcerCleared);
    }
    static void ReadScopes(YamlMappingNode root, string key, List<LevelDefinition> definitions)
    {
        if (Child(root, key) is not YamlNode node) return;
        if (node is not YamlSequenceNode sequence) throw Error(node, key + " must be a list of match blocks.");
        foreach (var item in sequence.Children)
        {
            if (item is not YamlMappingNode row) throw Error(item, key + " entry must be a mapping.");
            string target = Scalar(Child(row, "match"), key + " match");
            definitions.AddRange(ParseTarget(target, Project(row), key + "." + target));
        }
    }
    static YamlNode DefaultEnforcerNode()
    {
        var source = Mapping(CreatureKarmaManager.BuildDefaultYaml(), "pinned CM Karma defaults");
        if (Child(source, "Enforcer") is not YamlMappingNode enforcer || Child(enforcer, "modifiers") is not YamlNode modifiers)
            throw new InvalidDataException("Pinned CM Karma defaults are missing Enforcer.modifiers.");
        return modifiers;
    }
    static Dictionary<string, ModifierDefinition> ReadEnforcer(YamlNode node, string source, out bool cleared)
    {
        if (!CreatureYaml.TryReadModifierBlock(node, source, "Enforcer", CreatureYaml.ModifierYamlContext.Karma, out var modifiers, out cleared))
            throw new InvalidDataException("Invalid " + source + "; see source parser diagnostic.");
        return Clone(modifiers);
    }
    static YamlMappingNode Project(YamlMappingNode row)
    {
        var result = new YamlMappingNode();
        foreach (var entry in row.Children)
        {
            string key = Scalar(entry.Key, "rule field");
            if (LevelFields.Contains(key)) result.Add(entry.Key, key.Equals("modifiers", StringComparison.OrdinalIgnoreCase) ? ModifierNames(entry.Value) : entry.Value);
            else if (entry.Value is YamlMappingNode nested && CreatureKarmaManager.TryResolveBiomeName(key, out _)) result.Add(entry.Key, Project(nested));
        }
        return result;
    }
    internal static YamlNode ModifierNames(YamlNode node)
    {
        if (node is not YamlMappingNode map) return node;
        var result = new YamlMappingNode();
        foreach (var entry in map.Children)
        {
            string key = Scalar(entry.Key, "modifier name");
            // The public trait/blob name maps to the existing source knockback implementation.
            var mapped = key.Equals("crushing", StringComparison.OrdinalIgnoreCase) ? new YamlScalarNode("juggernaut") : entry.Key;
            if (result.Children.Keys.OfType<YamlScalarNode>().Any(existing => string.Equals(existing.Value, ((YamlScalarNode)mapped).Value, StringComparison.OrdinalIgnoreCase)))
                throw Error(entry.Key, "duplicate modifier name after Crushing/juggernaut alias resolution.");
            result.Add(mapped, entry.Value);
        }
        return result;
    }
    static List<LevelDefinition> ParseTarget(string target, YamlMappingNode row, string source)
    {
        if (row.Children.Count == 0) return new List<LevelDefinition>();
        return ParseSource(Write(new YamlMappingNode { { target, row } }), source);
    }
    static List<LevelDefinition> ParseSource(string text, string source)
    {
        if (!CreatureYaml.TryReadLevelDefinitions(text, source, out var definitions)) throw new InvalidDataException("Invalid " + source + "; see source parser diagnostic.");
        return definitions;
    }
    static YamlMappingNode Mapping(string text, string source)
    {
        var stream = new YamlStream(); stream.Load(new StringReader(text));
        if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root) throw new InvalidDataException(source + " must contain one mapping document.");
        CreatureYaml.ValidateUniqueMappingKeys(root, source, "root");
        return root;
    }
    static YamlNode? Child(YamlMappingNode map, string key) => map.Children.FirstOrDefault(entry => entry.Key is YamlScalarNode scalar && string.Equals(scalar.Value, key, StringComparison.OrdinalIgnoreCase)).Value;
    static string Scalar(YamlNode? node, string label) => node is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value) ? scalar.Value! : throw new InvalidDataException(label + " needs a non-empty scalar.");
    static InvalidDataException Error(YamlNode node, string message) => new("line " + node.Start.Line + ": " + message);
    static string Write(YamlMappingNode root)
    {
        var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        new YamlStream(new YamlDocument(root)).Save(writer, false); return writer.ToString();
    }
    static Dictionary<string, ModifierDefinition> Clone(Dictionary<string, ModifierDefinition>? source)
    {
        var result = new Dictionary<string, ModifierDefinition>(StringComparer.OrdinalIgnoreCase); Overlay(result, source); return result;
    }
    static void Overlay(Dictionary<string, ModifierDefinition> target, Dictionary<string, ModifierDefinition>? source)
    {
        if (source == null) return;
        foreach (var entry in source)
        {
            if (target.TryGetValue(entry.Key, out var current)) current.OverlayFrom(entry.Value);
            else target[entry.Key] = entry.Value.Clone();
        }
    }
    internal static string BuildDefaultSectionYaml()
    {
        var source = Mapping(NativeModifierDefaults.BuildDefaultLevelOverrideYaml(), "pinned CM defaults");
        var scopes = new YamlMappingNode();
        foreach (string scope in new[] { "Global", "Boss" })
        {
            var row = (YamlMappingNode)Child(source, scope)!;
            scopes.Add(scope, Child(row, "modifiers")!);
        }
        scopes.Add("Enforcer", DefaultEnforcerNode());
        // Default fragments concatenate into the single unified mapping, not separate YAML documents.
        return string.Join("\n", Write(new YamlMappingNode { { "modifier power", scopes } })
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Where(line => line != "---" && line != "..."));
    }
}
