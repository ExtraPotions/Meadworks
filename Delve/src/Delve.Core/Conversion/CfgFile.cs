// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: reads a BepInEx config file (sections, key = value, # comments) for the converter.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExtraPotions.Delve.Conversion
{
    public sealed class CfgFile
    {
        private readonly Dictionary<(string, string), string> values;

        private CfgFile(Dictionary<(string, string), string> values) => this.values = values;

        /// <summary>Every (section, key) in file order of first appearance.</summary>
        public IEnumerable<(string Section, string Key, string Value)> Entries
        {
            get { foreach (var kv in values) yield return (kv.Key.Item1, kv.Key.Item2, kv.Value); }
        }

        public static CfgFile Parse(string? text)
        {
            var values = new Dictionary<(string, string), string>();
            string section = "";
            foreach (var raw in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                values[(section, line.Substring(0, eq).Trim())] = line.Substring(eq + 1).Trim();
            }
            return new CfgFile(values);
        }

        public string? Text(string section, string key) => values.TryGetValue((section, key), out var v) ? v : null;

        public bool? Bool(string section, string key) => bool.TryParse(Text(section, key), out var b) ? b : (bool?)null;

        public int? Int(string section, string key) =>
            int.TryParse(Text(section, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : (int?)null;

        public float? Float(string section, string key)
        {
            var t = Text(section, key);
            return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && !float.IsNaN(f) && !float.IsInfinity(f) ? f : (float?)null;
        }

        /// <summary>The section a key is in, when it appears in exactly one section.</summary>
        public string? SectionOf(string key)
        {
            string? found = null;
            foreach (var k in values.Keys)
                if (string.Equals(k.Item2, key, StringComparison.Ordinal))
                {
                    if (found != null) return null;
                    found = k.Item1;
                }
            return found;
        }
    }
}
