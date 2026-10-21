// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Almanac.Index
{
    /// <summary>
    /// Portable path identity for deduplicating files found by several routes: separators unified to '/',
    /// doubled separators collapsed, '.' and '..' segments resolved, and case folded when the file system is
    /// case-insensitive (Windows). Pure string work; never touches the disk.
    /// </summary>
    public static class ConfigPaths
    {
        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            var unified = path.Replace('\\', '/');
            var leading = unified.StartsWith("//", StringComparison.Ordinal) ? "//" : unified.StartsWith("/", StringComparison.Ordinal) ? "/" : "";
            var parts = new List<string>();
            foreach (var part in unified.Split('/'))
            {
                if (part.Length == 0 || part == ".") continue;
                if (part == ".." && parts.Count > 0 && parts[parts.Count - 1] != ".." && !IsDrive(parts[parts.Count - 1]))
                {
                    parts.RemoveAt(parts.Count - 1);
                    continue;
                }
                parts.Add(part);
            }
            return leading + string.Join("/", parts);
        }

        /// <summary>Dictionary key for a path.</summary>
        public static string Key(string path, bool caseInsensitive)
        {
            var normalized = Normalize(path);
            return caseInsensitive ? normalized.ToUpperInvariant() : normalized;
        }

        public static bool Same(string a, string b, bool caseInsensitive) => Key(a, caseInsensitive) == Key(b, caseInsensitive);

        public static string FileName(string path)
        {
            var normalized = Normalize(path);
            var slash = normalized.LastIndexOf('/');
            return slash < 0 ? normalized : normalized.Substring(slash + 1);
        }

        public static bool HasExtension(string path, params string[] extensions)
        {
            var name = FileName(path);
            foreach (var extension in extensions)
                if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) && name.Length > extension.Length)
                    return true;
            return false;
        }

        private static bool IsDrive(string segment) => segment.Length == 2 && segment[1] == ':';
    }
}
