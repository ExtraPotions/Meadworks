// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Typed reads of one YAML mapping, reporting every problem with its path and file line into
    /// <see cref="RuleIssues"/>. A value that does not parse is an error and the fallback is returned, so a parser can
    /// keep going and report everything wrong with a file in one pass.
    /// </summary>
    internal readonly struct MapReader
    {
        public MapReader(YamlMappingNode map, string path, RuleIssues issues)
        {
            Map = map;
            Path = path;
            Issues = issues;
        }

        public YamlMappingNode Map { get; }
        public string Path { get; }
        public RuleIssues Issues { get; }

        public string At(string key) => Path.Length == 0 ? key : Path + "." + key;

        public YamlNode? Node(string key) => YamlNodes.Child(Map, key);

        public bool Has(string key) => Node(key) != null;

        public void Error(string key, string message) => Issues.Error(At(key), Node(key) ?? Map, message);

        public void Warn(string key, string message) => Issues.Warn(At(key), Node(key) ?? Map, message);

        public string? Str(string key)
        {
            YamlNode? node = Node(key);
            if (node == null)
            {
                return null;
            }
            if (node is YamlScalarNode scalar)
            {
                return scalar.Value;
            }
            Error(key, "should be a single value");
            return null;
        }

        /// <summary>A snake_case id; an error when present but malformed.</summary>
        public string? Id(string key)
        {
            string? text = Str(key);
            if (text != null && !Ids.IsValid(text))
            {
                Error(key, $"'{text}' is not a valid id (lowercase letters, digits and _, 2-48 long, starting with a letter)");
                return null;
            }
            return text;
        }

        public bool Bool(string key, bool fallback)
        {
            YamlNode? node = Node(key);
            if (node == null)
            {
                return fallback;
            }
            if (YamlNodes.TryBool(node, out bool value))
            {
                return value;
            }
            Error(key, "should be true or false");
            return fallback;
        }

        public int Int(string key, int fallback, int min = int.MinValue, int max = int.MaxValue)
        {
            YamlNode? node = Node(key);
            if (node == null)
            {
                return fallback;
            }
            if (YamlNodes.TryInt(node, out int value) && value >= min && value <= max)
            {
                return value;
            }
            Error(key, $"should be a whole number{Range(min, max)}");
            return fallback;
        }

        public float Float(string key, float fallback, float min = float.MinValue, float max = float.MaxValue)
        {
            YamlNode? node = Node(key);
            if (node == null)
            {
                return fallback;
            }
            if (YamlNodes.TryFloat(node, out float value) && value >= min && value <= max)
            {
                return value;
            }
            Error(key, $"should be a number{Range(min, max)}");
            return fallback;
        }

        /// <summary>A snake_case enum value (see <see cref="EnumIds{T}"/>).</summary>
        public T Enum<T>(string key, T fallback) where T : struct, System.Enum
        {
            string? text = Str(key);
            if (text == null)
            {
                return fallback;
            }
            if (EnumIds<T>.TryParse(text, out T value))
            {
                return value;
            }
            Error(key, $"'{text}' is not one of: {EnumIds<T>.Joined}");
            return fallback;
        }

        /// <summary>A list of single values; one value alone is accepted as a list of one.</summary>
        public List<string>? Strings(string key)
        {
            YamlNode? node = Node(key);
            if (node == null)
            {
                return null;
            }
            if (node is YamlScalarNode scalar)
            {
                return new List<string> { scalar.Value ?? "" };
            }
            return node is YamlSequenceNode seq ? SeqStrings(key, seq) : ErrorNull<List<string>>(key, "should be a list like [a, b]");
        }

        /// <summary>A list of numbers of exactly <paramref name="length"/> entries when length is above 0.</summary>
        public float[]? Floats(string key, int length = 0)
        {
            YamlNode? node = Node(key);
            return node == null ? null : YamlLists.Floats(node, At(key), Issues, length);
        }

        /// <summary>The child mapping, or null (with an error when the key holds something else).</summary>
        public MapReader? Sub(string key)
        {
            YamlNode? node = Node(key);
            if (node == null)
            {
                return null;
            }
            if (node is YamlMappingNode map)
            {
                return new MapReader(map, At(key), Issues);
            }
            Error(key, "should be a block of keyed values");
            return null;
        }

        public YamlSequenceNode? Seq(string key)
        {
            YamlNode? node = Node(key);
            if (node == null || node is YamlSequenceNode)
            {
                return node as YamlSequenceNode;
            }
            Error(key, "should be a list");
            return null;
        }

        /// <summary>Warns about every key not in <paramref name="known"/> (a typo usually).</summary>
        public void Unknown(params string[] known)
        {
            foreach (KeyValuePair<YamlNode, YamlNode> pair in Map.Children)
            {
                string key = YamlNodes.KeyText(pair.Key);
                if (System.Array.IndexOf(known, key) < 0)
                {
                    Issues.Warn(At(key), pair.Key, "unknown key, ignored");
                }
            }
        }

        private List<string>? SeqStrings(string key, YamlSequenceNode seq)
        {
            List<string> list = new List<string>();
            foreach (YamlNode item in seq.Children)
            {
                if (item is YamlScalarNode s)
                {
                    list.Add(s.Value ?? "");
                }
                else
                {
                    Issues.Error(At(key), item, "list entries should be single values");
                }
            }
            return list;
        }

        private T? ErrorNull<T>(string key, string message) where T : class
        {
            Error(key, message);
            return null;
        }

        private static string Range(float min, float max)
        {
            if (min <= float.MinValue / 2 && max >= float.MaxValue / 2)
            {
                return "";
            }
            if (max >= float.MaxValue / 2 || max >= int.MaxValue)
            {
                return $" of at least {Numbers.Format(min)}";
            }
            return $" from {Numbers.Format(min)} to {Numbers.Format(max)}";
        }
    }
}
