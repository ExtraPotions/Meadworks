// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// The low-level reads over a YamlDotNet document: finding a child by key, and turning scalars and sequences into
    /// numbers. Every failure is recorded against the offending line so the reload can report a bad file line by line
    /// rather than throwing. A value that will not parse is skipped and its default kept.
    /// </summary>
    internal static class YamlRead
    {
        /// <summary>The value node under a mapping key, or null when the key is absent.</summary>
        public static YamlNode? Child(YamlMappingNode map, string key)
        {
            foreach (KeyValuePair<YamlNode, YamlNode> pair in map.Children)
            {
                if (pair.Key is YamlScalarNode scalar && scalar.Value == key)
                {
                    return pair.Value;
                }
            }
            return null;
        }

        public static YamlMappingNode? Map(YamlNode? node, List<string> errors, string what)
        {
            if (node is YamlMappingNode map)
            {
                return map;
            }
            AddError(errors, node, $"{what} should be a block of keyed values");
            return null;
        }

        public static float[]? Floats(YamlNode? node, List<string> errors, string what)
        {
            if (!(node is YamlSequenceNode seq))
            {
                AddError(errors, node, $"{what} should be a list like [1, 2, 3]");
                return null;
            }
            List<float> values = new List<float>();
            foreach (YamlNode item in seq.Children)
            {
                if (TryFloat(item, out float value))
                {
                    values.Add(value);
                }
                else
                {
                    AddError(errors, item, $"{what} has a value that is not a number");
                }
            }
            return values.Count > 0 ? values.ToArray() : null;
        }

        public static bool TryFloat(YamlNode? node, out float value)
        {
            value = 0f;
            return node is YamlScalarNode scalar
                && float.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryBool(YamlNode? node, out bool value)
        {
            value = false;
            return node is YamlScalarNode scalar && bool.TryParse(scalar.Value, out value);
        }

        public static bool Bool(YamlMappingNode map, string key, bool fallback, List<string> errors)
        {
            YamlNode? node = Child(map, key);
            if (node == null)
            {
                return fallback;
            }
            if (node is YamlScalarNode scalar && bool.TryParse(scalar.Value, out bool value))
            {
                return value;
            }
            AddError(errors, node, $"'{key}' should be true or false");
            return fallback;
        }

        public static int Int(YamlMappingNode map, string key, int fallback, List<string> errors)
        {
            YamlNode? node = Child(map, key);
            if (node == null)
            {
                return fallback;
            }
            if (TryFloat(node, out float value))
            {
                return (int)value;
            }
            AddError(errors, node, $"'{key}' should be a whole number");
            return fallback;
        }

        public static string? Scalar(YamlMappingNode map, string key) =>
            YamlRead.Child(map, key) is YamlScalarNode scalar ? scalar.Value : null;

        public static void AddError(List<string> errors, YamlNode? node, string message)
        {
            long line = node?.Start.Line ?? 0;
            errors.Add($"line {line}: {message}");
        }
    }
}
