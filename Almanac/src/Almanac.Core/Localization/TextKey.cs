// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;

namespace ExtraPotions.Almanac.Localization
{
    /// <summary>
    /// Catalog key for an English interface phrase. The window keeps writing English in source; the key is derived,
    /// so the catalogs and the source cannot drift apart without a failing test. Keys fit Crucible's key shape.
    /// </summary>
    public static class TextKey
    {
        public const int MaximumLength = 60;
        const int HashLength = 8;

        public static string For(string english)
        {
            var builder = new StringBuilder(english.Length);
            bool gap = false;
            foreach (char raw in english)
            {
                char c = char.ToLowerInvariant(raw);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    if (gap && builder.Length > 0) builder.Append('_');
                    builder.Append(c);
                    gap = false;
                }
                else gap = true;
            }
            string key = builder.Length == 0 ? "t" : builder.ToString();
            if (key[0] >= '0' && key[0] <= '9') key = "t_" + key;
            if (key.Length <= MaximumLength) return key;
            string head = key.Substring(0, MaximumLength - HashLength - 1).TrimEnd('_');
            return head + "_" + Fnv(english).ToString("x8");
        }

        static uint Fnv(string value)
        {
            uint hash = 2166136261;
            foreach (char c in value) { hash ^= c; hash *= 16777619; }
            return hash;
        }
    }
}
