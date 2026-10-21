// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gerald Glasgow
// Adopted from OpenKeep 3.0.0 / ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Upstream GPL-3.0-or-later notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: Kindle namespaces and native integration boundaries; keys a salvage-rune provider handles.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Kindle.Runtime.Salvage
{
    /// <summary>
    /// Whether an item carries custom item data another mod keeps on it (magic affixes and the like), recognised by
    /// a key prefix from the setting <see cref="SalvageSettings.ModDataPrefixes"/>. Only the keys are looked at,
    /// never the values, and nothing of the other mod is referenced. The prefix list is split again only when the
    /// setting's text changes, so checking every stack on every panel refresh allocates nothing.
    /// </summary>
    public static class ModDataCheck
    {
        private static readonly char[] separators = { ',', ';', ' ' };
        private static string parsedFrom;
        private static string[] prefixes = new string[0];

        /// <summary>
        /// True when Skip Items With Mod Data is on and one of the item's custom data keys starts with a listed prefix,
        /// leaving out the keys a present salvage-rune provider handles (<see cref="SalvageRuneLink.Handles"/>).
        /// </summary>
        public static bool Carries(ItemDrop.ItemData item)
        {
            if (!SalvageSettings.SkipModData.Value || item.m_customData == null || item.m_customData.Count == 0)
                return false;
            string[] list = Prefixes();
            if (list.Length == 0)
                return false;
            foreach (KeyValuePair<string, string> entry in item.m_customData)
            {
                if (StartsWithAny(entry.Key, list) && !SalvageRuneLink.Handles(entry.Key))
                    return true;
            }
            return false;
        }

        private static bool StartsWithAny(string key, string[] list)
        {
            if (key == null)
                return false;
            foreach (string prefix in list)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string[] Prefixes()
        {
            string text = SalvageSettings.ModDataPrefixes.Value ?? "";
            if (text != parsedFrom)
            {
                prefixes = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                parsedFrom = text;
            }
            return prefixes;
        }
    }
}
