// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ExtraPotions.Inscribe.Adopted.Core
{
    /// <summary>
    /// Reads the text resources compiled into the DLL: the built-in YAML defaults (<c>ExtraPotions.Inscribe.Adopted.config.*</c>) and
    /// the English words (<c>ExtraPotions.Inscribe.Adopted.translations.*</c>). Names are the csproj's LogicalName, never paths.
    /// </summary>
    public static class Embedded
    {
        private static readonly Assembly Self = typeof(Embedded).Assembly;

        /// <summary>The resource's text, or null when there is no such resource.</summary>
        public static string? Text(string logicalName)
        {
            using Stream? stream = Self.GetManifestResourceStream(logicalName);
            if (stream == null)
            {
                return null;
            }
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <summary>The resource's bytes (an image), or null when there is no such resource.</summary>
        public static byte[]? Bytes(string logicalName)
        {
            using Stream? stream = Self.GetManifestResourceStream(logicalName);
            if (stream == null)
            {
                return null;
            }
            using MemoryStream copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }

        /// <summary>Every resource name starting with the prefix and ending with the suffix, in ordinal order.</summary>
        public static List<string> Names(string prefix, string suffix)
        {
            List<string> names = new List<string>();
            foreach (string name in Self.GetManifestResourceNames())
            {
                if (name.StartsWith(prefix, System.StringComparison.Ordinal)
                    && name.EndsWith(suffix, System.StringComparison.Ordinal))
                {
                    names.Add(name);
                }
            }
            names.Sort(System.StringComparer.Ordinal);
            return names;
        }
    }
}
