// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Almanac.Index
{
    /// <summary>
    /// MeadWorks plugin IDs that were renamed. Barter (0.1.2) and Solstice (0.1.3) moved from com.extrapotions.* to
    /// com.meadworks.*, and BepInEx names each settings file after the ID. Saved profiles, pins and the remembered plugin may
    /// still hold the old ID or file name; Almanac reads them as the new one. Kept for one release, so the old and the new
    /// product versions both match.
    /// </summary>
    public static class LegacyPluginIds
    {
        static readonly Dictionary<string, string> Renamed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["com.extrapotions.barter"] = "com.meadworks.barter",
            ["com.extrapotions.solstice"] = "com.meadworks.solstice",
        };

        public static IReadOnlyDictionary<string, string> All => Renamed;

        /// <summary>The current ID for <paramref name="id"/>: the new one for a renamed plugin, otherwise unchanged.</summary>
        public static string Current(string id) => id != null && Renamed.TryGetValue(id, out var current) ? current : id ?? "";

        /// <summary>True when both IDs name the same plugin, counting a renamed plugin's old and new ID as one.</summary>
        public static bool Same(string a, string b) => string.Equals(Current(a), Current(b), StringComparison.Ordinal);

        /// <summary>
        /// A settings file key (a path relative to BepInEx/config, '/' separated) with a renamed plugin's old file name replaced by
        /// the new one. Only the top-level &lt;old id&gt;.cfg is renamed, matching BepInEx's own naming.
        /// </summary>
        public static string CurrentFile(string fileKey)
        {
            if (string.IsNullOrEmpty(fileKey) || fileKey.IndexOf('/') >= 0) return fileKey ?? "";
            foreach (var pair in Renamed)
                if (string.Equals(fileKey, pair.Key + ".cfg", StringComparison.OrdinalIgnoreCase))
                    return pair.Value + ".cfg";
            return fileKey;
        }
    }
}
