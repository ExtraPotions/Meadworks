// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Edge file-layer and C01 transport boundaries. Actual schemas and normalization remain adopted source code.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ExtraPotions.Edge.Config;
using YamlDotNet.RepresentationModel;
namespace ExtraPotions.Edge.Secondaries.Adopted;

internal static class RuleDocuments
{
    private const string Marker = "# Edge snapshot: ";

    internal static bool TryMerge(IReadOnlyDictionary<string, string> files, out Dictionary<SecondaryAttackYamlDomainId, string> texts)
    {
        texts = new();
        foreach (RuleFamily family in Enum.GetValues(typeof(RuleFamily)))
        {
            var merged = new YamlMappingNode();
            foreach (var path in EdgeDataFiles.Layers(family, files.Keys))
            {
                if (!SecondaryAttackConfigLoader.TryReadLayer((SecondaryAttackYamlDomainId)family, files[path], out var layer)) return false;
                // Prefab names follow the source's ordinal-ignore-case identity, while schema field names remain exact.
                foreach (var pair in layer.Children.ToArray())
                {
                    var text = (pair.Key as YamlScalarNode)?.Value ?? "";
                    var canonical = merged.Children.Keys.OfType<YamlScalarNode>()
                        .FirstOrDefault(k => string.Equals(k.Value, text, StringComparison.OrdinalIgnoreCase));
                    if (canonical == null || canonical.Value == text) continue;
                    layer.Children.Remove(pair.Key);
                    layer.Children.Add(new YamlScalarNode(canonical.Value), pair.Value);
                }
                merged = RuleMapMerge.MergeMaps(merged, layer);
            }
            using var writer = new StringWriter();
            new YamlStream(new YamlDocument(merged)).Save(writer, assignAnchors: false);
            texts[(SecondaryAttackYamlDomainId)family] = writer.ToString();
        }
        return true;
    }

    // A common generation on all three carriers prevents a client from accepting a partially delivered C01 push.
    internal static Dictionary<SecondaryAttackYamlDomainId, string> Stamp(IReadOnlyDictionary<SecondaryAttackYamlDomainId, string> texts)
    {
        var body = new StringBuilder();
        foreach (SecondaryAttackYamlDomainId family in Enum.GetValues(typeof(SecondaryAttackYamlDomainId)))
        {
            var text = texts[family];
            body.Append((int)family).Append(':').Append(text.Length).Append(':').Append(text);
        }
        using var sha = SHA256.Create();
        var token = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(body.ToString()))).Replace("-", "");
        return texts.ToDictionary(p => p.Key, p => Marker + token + "\n" + p.Value);
    }

    internal static bool TryReadCarriers(IReadOnlyDictionary<SecondaryAttackYamlDomainId, string> carriers, out SecondaryAttackYamlTexts? texts)
    {
        texts = null;
        var unwrapped = new Dictionary<SecondaryAttackYamlDomainId, string>();
        string? generation = null;
        foreach (SecondaryAttackYamlDomainId family in Enum.GetValues(typeof(SecondaryAttackYamlDomainId)))
        {
            if (!carriers.TryGetValue(family, out var text) || !text.StartsWith(Marker, StringComparison.Ordinal)) return false;
            var end = text.IndexOf('\n');
            if (end < 0) return false;
            var id = text.Substring(Marker.Length, end - Marker.Length);
            if (id.Length != 64 || (generation != null && generation != id)) return false;
            generation = id;
            unwrapped[family] = text.Substring(end + 1);
        }
        // Also validate bytes, so a manual carrier edit with an old marker fails closed.
        var expected = Stamp(unwrapped);
        if (expected.Any(p => p.Value != carriers[p.Key])) return false;
        texts = new SecondaryAttackYamlTexts(unwrapped);
        return true;
    }
}
