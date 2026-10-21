// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Converter-specific file identity and retained-layer composition. Parsing uses the pinned native schemas.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace ExtraPotions.Edge.Secondaries.Adopted;

internal static class RuleImportDocuments
{
    internal static Dictionary<string, string> Compose(IReadOnlyDictionary<string, string> existing, IReadOnlyDictionary<string, string> incoming)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in existing)
            if (!files.TryAdd(Path.GetFileName(pair.Key), pair.Value)) throw new FormatException("Ambiguous Edge rule filename: " + Path.GetFileName(pair.Key));
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in incoming)
        {
            string name = Path.GetFileName(pair.Key);
            if (!names.Add(name)) throw new FormatException("Two source files map to the same Edge rule filename: " + name);
            // A case-only replacement keeps the existing Windows filename and its ordinal layer order.
            files[name] = pair.Value;
        }
        if (!RuleDocuments.TryMerge(files, out var merged) || !SecondaryAttackConfigLoader.TryCompileSnapshot(1,
                new SecondaryAttackYamlTexts(merged), out _)) throw new FormatException("Imported rules conflict with the retained Edge layers or have an invalid schema.");
        return files;
    }
}
