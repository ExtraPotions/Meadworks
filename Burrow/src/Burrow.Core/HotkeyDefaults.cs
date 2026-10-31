// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the one-time move of untouched hotkeys to their new unbound defaults (settings simplification).
using System;
using System.Collections.Generic;

namespace ExtraPotions.Burrow
{
    /// <summary>
    /// Since the settings simplification only Quick Stack (Q), Store All (G), Sort Inventory (T) and the Fill Modifier
    /// (LeftShift) are bound by default; every other Burrow hotkey starts unbound and stays rebindable. A saved value that
    /// is exactly the old default was never chosen by the player, so it moves to the new (unbound) default once; any other
    /// saved value is kept. The marker makes it run once, so a key the player later binds back to the old default stays.
    /// </summary>
    public static class HotkeyDefaults
    {
        /// <summary>The marker value once the move has run.</summary>
        public const int Current = 1;

        /// <summary>The hidden per-player marker that records the move ran.</summary>
        public const string MarkerSection = "General";
        public const string MarkerKey = "Hotkey Defaults Version";

        /// <summary>The settings that are now unbound by default (Section, Key).</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> NowUnbound = new[]
        {
            Pair("1. Reach", "Pull Modifier"),
            Pair("1. Reach", "Toggle Key"),
            Pair("1. Reach", "Link Key"),
            Pair("2. Stow", "Take All Key"),
            Pair("2. Stow", "Top Up Key"),
            Pair("2. Stow", "Sort Container Key"),
            Pair("2. Stow", "Favorite Item Key"),
            Pair("2. Stow", "Favorite Slot Key"),
            Pair("2. Stow", "Junk Key"),
            Pair("2. Stow", "Trash Key"),
            Pair("2. Stow", "Destroy Junk Key"),
            Pair("2. Stow", "Feed Source Key"),
            Pair("2. Stow", "Route Modifier"),
            Pair("2. Stow", "Store One Key"),
            Pair("2. Stow", "Find Key"),
            Pair("2. Stow", "Dump Key"),
            Pair("2. Stow", "Cycle Previous Key"),
            Pair("2. Stow", "Cycle Next Key"),
        };

        /// <summary>The hotkeys that keep a default binding (Section, Key).</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> StillBound = new[]
        {
            Pair("2. Stow", "Quick Stack Key"),
            Pair("2. Stow", "Store All Key"),
            Pair("2. Stow", "Sort Inventory Key"),
            Pair("1. Reach", "Fill Modifier"),
        };

        /// <summary>
        /// The ids of the entries whose saved text equals their old default text, or none once <paramref name="marker"/>
        /// has reached <see cref="Current"/>. Texts are the config file's serialized values, compared exactly.
        /// </summary>
        public static List<string> Plan(int marker, IEnumerable<Saved>? entries)
        {
            var moved = new List<string>();
            if (marker >= Current || entries == null)
                return moved;
            foreach (Saved entry in entries)
                if (entry.Value != null && string.Equals(entry.Value, entry.OldDefault, StringComparison.Ordinal))
                    moved.Add(entry.Id);
            return moved;
        }

        /// <summary>One hotkey's current saved text and the default it had before the change.</summary>
        public readonly struct Saved
        {
            public Saved(string id, string? value, string oldDefault)
            {
                Id = id;
                Value = value;
                OldDefault = oldDefault;
            }

            public string Id { get; }
            public string? Value { get; }
            public string OldDefault { get; }
        }

        /// <summary>
        /// Plain left/right clicks already operate inventory controls. Treating them as hotkeys also ran bulk actions
        /// on every click (Feed Source Key = Mouse0 in the reported config). Modified and side-button bindings still work.
        /// </summary>
        public static bool IgnoredBareClick(string? mainKey, bool hasModifiers) =>
            !hasModifiers && (string.Equals(mainKey, "Mouse0", StringComparison.Ordinal) || string.Equals(mainKey, "Mouse1", StringComparison.Ordinal));

        private static KeyValuePair<string, string> Pair(string section, string key) => new KeyValuePair<string, string>(section, key);
    }
}
