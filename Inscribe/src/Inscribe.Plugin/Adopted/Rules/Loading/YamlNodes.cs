// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>Scalar-level reads over YamlDotNet nodes. Culture-invariant, never throwing.</summary>
    internal static class YamlNodes
    {
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

        /// <summary>A plain <c>~</c>, <c>null</c> or empty scalar: "remove this key" in a later layer.</summary>
        public static bool IsNull(YamlNode? node)
        {
            if (!(node is YamlScalarNode scalar) || scalar.Style == YamlDotNet.Core.ScalarStyle.SingleQuoted
                || scalar.Style == YamlDotNet.Core.ScalarStyle.DoubleQuoted)
            {
                return false;
            }
            string? v = scalar.Value;
            return string.IsNullOrEmpty(v) || v == "~" || v == "null" || v == "Null" || v == "NULL";
        }

        public static string? Text(YamlNode? node) => node is YamlScalarNode scalar ? scalar.Value : null;

        public static string KeyText(YamlNode key) => (key as YamlScalarNode)?.Value ?? "";

        public static bool TryFloat(YamlNode? node, out float value) => Numbers.TryFloat(Text(node), out value);

        public static bool TryInt(YamlNode? node, out int value) => Numbers.TryInt(Text(node), out value);

        /// <summary>Same content: scalars by text, maps key by key in any order, sequences item by item.</summary>
        public static bool Same(YamlNode? a, YamlNode? b)
        {
            switch (a)
            {
                case YamlScalarNode sa:
                    return b is YamlScalarNode sb && sa.Value == sb.Value;
                case YamlMappingNode ma:
                    return b is YamlMappingNode mb && SameMaps(ma, mb);
                case YamlSequenceNode qa:
                    return b is YamlSequenceNode qb && SameSequences(qa, qb);
                default:
                    return a == null && b == null;
            }
        }

        private static bool SameMaps(YamlMappingNode a, YamlMappingNode b)
        {
            if (a.Children.Count != b.Children.Count)
            {
                return false;
            }
            foreach (KeyValuePair<YamlNode, YamlNode> pair in a.Children)
            {
                if (!Same(pair.Value, Child(b, KeyText(pair.Key))))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool SameSequences(YamlSequenceNode a, YamlSequenceNode b)
        {
            if (a.Children.Count != b.Children.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Children.Count; i++)
            {
                if (!Same(a.Children[i], b.Children[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool TryBool(YamlNode? node, out bool value)
        {
            value = false;
            string? text = Text(node);
            return text != null && bool.TryParse(text, out value);
        }
    }
}
