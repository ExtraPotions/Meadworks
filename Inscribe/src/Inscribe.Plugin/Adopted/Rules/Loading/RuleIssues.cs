// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Collects the errors, warnings and notes of one family build. Every parsed node is registered with the file it
    /// came from, so a message about a node reads <c>file:line: path: message</c> even after the layers were merged.
    /// Errors reject the whole family; warnings and notes are logged and the family applies.
    /// </summary>
    internal sealed class RuleIssues
    {
        private readonly Dictionary<YamlNode, string> _origins = new Dictionary<YamlNode, string>(RefComparer.Instance);

        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Notes = new List<string>();

        public bool HasErrors => Errors.Count > 0;

        public void Register(YamlNode root, string origin)
        {
            foreach (YamlNode node in root.AllNodes)
            {
                _origins[node] = origin;
            }
        }

        public void Error(string path, YamlNode? node, string message) => Errors.Add(Format(path, node, message));

        public void Warn(string path, YamlNode? node, string message) => Warnings.Add(Format(path, node, message));

        public void Note(string message) => Notes.Add(message);

        private string Format(string path, YamlNode? node, string message)
        {
            string where = node != null && _origins.TryGetValue(node, out string origin)
                ? $"{origin}:{node.Start.Line}: "
                : "";
            return string.IsNullOrEmpty(path) ? where + message : $"{where}{path}: {message}";
        }

        private sealed class RefComparer : IEqualityComparer<YamlNode>
        {
            public static readonly RefComparer Instance = new RefComparer();

            public bool Equals(YamlNode x, YamlNode y) => ReferenceEquals(x, y);

            public int GetHashCode(YamlNode obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
