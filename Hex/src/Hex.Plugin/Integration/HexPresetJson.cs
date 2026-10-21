// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExtraPotions.Hex.Runtime;

internal sealed class HexPresetDocument
{
    internal string Selected = "";
    internal readonly Dictionary<string, Dictionary<string, object>> Presets = new(StringComparer.OrdinalIgnoreCase);
    internal HexPresetDocument Copy()
    {
        var copy = new HexPresetDocument { Selected = Selected };
        foreach (var pair in Presets) copy.Presets.Add(pair.Key, new Dictionary<string, object>(pair.Value, StringComparer.Ordinal));
        return copy;
    }
}

internal static class HexPresetJson
{
    internal const int MaximumCharacters = 131072, MaximumPresets = 32;
    internal static bool NameValid(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 64
        && name.Trim() == name && !name.Any(char.IsControl);

    internal static HexPresetDocument Read(string text, Func<string, object, bool> valid)
    {
        if (text == "") return new HexPresetDocument();
        if (text.Length > MaximumCharacters) throw new FormatException("Preset file exceeds its size limit.");
        using var input = new StringReader(text);
        using var reader = new JsonTextReader(input) { MaxDepth = 8, DateParseHandling = DateParseHandling.None };
        var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (reader.Read()) throw new FormatException("Trailing preset content.");
        if (root.Properties().Any(p => p.Name != "version" && p.Name != "selected" && p.Name != "presets")
            || root["version"]?.Type != JTokenType.Integer || root["version"]!.Value<long>() != 1
            || root["selected"]?.Type != JTokenType.String || !(root["presets"] is JObject presets))
            throw new FormatException("Unsupported preset document.");
        var document = new HexPresetDocument { Selected = root["selected"]!.Value<string>()! };
        if (presets.Count > MaximumPresets) throw new FormatException("Too many presets.");
        foreach (var preset in presets.Properties())
        {
            if (!NameValid(preset.Name) || !(preset.Value is JObject values) || document.Presets.ContainsKey(preset.Name))
                throw new FormatException("Invalid or duplicate preset name.");
            var snapshot = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var value in values.Properties())
            {
                object parsed;
                if (value.Value.Type == JTokenType.Boolean) parsed = value.Value.Value<bool>();
                else if (value.Value.Type == JTokenType.Integer)
                {
                    var integer = value.Value.Value<long>();
                    if (integer < int.MinValue || integer > int.MaxValue) throw new FormatException("Preset integer outside range.");
                    parsed = (int)integer;
                }
                else throw new FormatException("Preset values must be booleans or integers.");
                if (!valid(value.Name, parsed)) throw new FormatException("Unknown or invalid preset preference.");
                snapshot.Add(value.Name, parsed);
            }
            document.Presets.Add(preset.Name, snapshot);
        }
        if (document.Selected != "" && !document.Presets.ContainsKey(document.Selected)) throw new FormatException("Selected preset missing.");
        return document;
    }

    internal static string Write(HexPresetDocument document)
    {
        var presets = new JObject();
        foreach (var pair in document.Presets.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            var values = new JObject();
            foreach (var entry in pair.Value.OrderBy(p => p.Key, StringComparer.Ordinal)) values.Add(entry.Key, JToken.FromObject(entry.Value));
            presets.Add(pair.Key, values);
        }
        return new JObject { ["version"] = 1, ["selected"] = document.Selected, ["presets"] = presets }.ToString(Formatting.Indented) + "\n";
    }
}
