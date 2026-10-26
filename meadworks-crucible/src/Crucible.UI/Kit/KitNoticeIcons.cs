// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    internal static class KitNoticeIcons
    {
        internal static Sprite Find(string text)
        {
            if (string.IsNullOrEmpty(text) || ObjectDB.instance == null || ObjectDB.instance.m_items == null) return null;
            Sprite best = null;
            int length = 0;
            bool ambiguous = false;
            // Read live items: ObjectDB and localization can change or finish loading after the first notice.
            foreach (var prefab in ObjectDB.instance.m_items)
            {
                if (prefab == null) continue;
                var item = prefab.GetComponent<ItemDrop>()?.m_itemData;
                var shared = item?.m_shared;
                if (shared == null || shared.m_icons == null || shared.m_icons.Length == 0 || shared.m_icons[0] == null) continue;
                string localized = Localization.instance != null ? Localization.instance.Localize(shared.m_name) : shared.m_name;
                int match = Math.Max(Match(text, prefab.name), Math.Max(Match(text, shared.m_name), Match(text, localized)));
                if (match == 0 || match < length) continue;
                if (match == length) { if (best != shared.m_icons[0]) ambiguous = true; continue; }
                best = shared.m_icons[0]; length = match; ambiguous = false;
            }
            return ambiguous ? null : best;
        }

        static int Match(string text, string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            if (string.Equals(text, name, StringComparison.OrdinalIgnoreCase)) return int.MaxValue;
            int start = 0;
            while (start < text.Length)
            {
                int at = text.IndexOf(name, start, StringComparison.OrdinalIgnoreCase);
                if (at < 0) break;
                int end = at + name.Length;
                if ((at == 0 || !Word(text[at - 1])) && (end == text.Length || !Word(text[end]))) return name.Length;
                start = at + 1;
            }
            return 0;
        }

        static bool Word(char value) => char.IsLetterOrDigit(value) || value == '_' || value == '$';
    }
}
