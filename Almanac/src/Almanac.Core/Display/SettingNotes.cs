// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Almanac.Display
{
    /// <summary>When an accepted change takes effect, as far as the owning mod's description says.</summary>
    public enum ChangeTiming { Live, Restart, WorldReload, NewItemsOnly }

    public static class SettingNotes
    {
        static readonly string[] SyncSuffixes = { " [Synced with Server]", " [Not Synced with Server]" };
        static readonly string[] RestartPhrases = { "requires restart", "restart required", "after restart", "relog", "restart the game" };
        static readonly string[] WorldPhrases = { "next time you enter a world", "re-enter the world", "re-entering the world" };
        static readonly string[] NewItemPhrases = { "applies only to items", "applies to items created after" };

        /// <summary>The description without the binder's trailing sync marker; Almanac shows owner and scope separately.</summary>
        public static string WithoutSyncSuffix(string? description)
        {
            string text = description ?? "";
            foreach (string suffix in SyncSuffixes)
                if (text.EndsWith(suffix, StringComparison.Ordinal)) return text.Substring(0, text.Length - suffix.Length);
            return text;
        }

        /// <summary>A restart phrase wins over a world reload, which wins over a new-items-only note.</summary>
        public static ChangeTiming Classify(string? description, IEnumerable<string>? extraRestartPhrases = null)
        {
            string text = description ?? "";
            if (Contains(text, RestartPhrases)) return ChangeTiming.Restart;
            if (extraRestartPhrases != null)
            {
                int seen = 0;
                foreach (string phrase in extraRestartPhrases)
                {
                    if (++seen > 256) break;
                    string trimmed = phrase?.Trim() ?? "";
                    if (trimmed.Length > 0 && text.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0) return ChangeTiming.Restart;
                }
            }
            if (Contains(text, WorldPhrases)) return ChangeTiming.WorldReload;
            if (Contains(text, NewItemPhrases)) return ChangeTiming.NewItemsOnly;
            return ChangeTiming.Live;
        }

        static bool Contains(string text, string[] phrases)
        {
            foreach (string phrase in phrases)
                if (text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
