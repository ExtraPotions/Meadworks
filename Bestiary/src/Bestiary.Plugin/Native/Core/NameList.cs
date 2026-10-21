// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Bestiary.Runtime.Native.Core
{
    /// <summary>A setting that lists names separated by commas, like "TreasureChest_forestcrypt, TreasureChest_sunkencrypt".</summary>
    internal static class NameList
    {
        private static string? parsedList;
        private static HashSet<string> parsed = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Whether the list names <paramref name="name"/> exactly (spaces around each entry ignored). The list is split
        /// once per value: a config entry hands back the same string until it is edited, so the last one parsed is kept.
        /// </summary>
        public static bool Contains(string list, string name)
        {
            if (!ReferenceEquals(list, parsedList))
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (string entry in list.Split(','))
                {
                    names.Add(entry.Trim());
                }
                parsed = names;
                parsedList = list;
            }
            return parsed.Contains(name);
        }
    }
}
