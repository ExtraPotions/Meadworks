// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the one-time move of untouched non-hotkey settings to a changed default (playtest 2026-10-20).
using System.Collections.Generic;

namespace ExtraPotions.Burrow
{
    /// <summary>
    /// Settings whose default changed after release. A saved value equal to the old default was never chosen by the
    /// player, so it moves to the new default once (the same rule as <see cref="HotkeyDefaults"/>); any other saved value
    /// is kept, and the marker keeps a value the player sets back later.
    /// </summary>
    public static class SettingDefaults
    {
        /// <summary>The marker value once every change below has run.</summary>
        public const int Current = 1;

        public const string MarkerSection = "General";
        public const string MarkerKey = "Setting Defaults Version";

        /// <summary>A changed default: the setting's id (Section/Key) and its old and new serialized values.</summary>
        public readonly struct Change
        {
            public Change(string id, string oldDefault, string newDefault)
            {
                Id = id;
                OldDefault = oldDefault;
                NewDefault = newDefault;
            }

            public string Id { get; }
            public string OldDefault { get; }
            public string NewDefault { get; }
        }

        /// <summary>Version 1: the trash can salvages by default (playtest 2026-10-20 item 10).</summary>
        public static readonly IReadOnlyList<Change> Changes = new[]
        {
            new Change("2. Stow/Trash Uses Salvage", "false", "true"),
        };

        /// <summary>The ids whose saved text equals their old default, or none once the marker reached <see cref="Current"/>.</summary>
        public static List<string> Plan(int marker, IEnumerable<HotkeyDefaults.Saved>? entries) =>
            marker >= Current ? new List<string>() : HotkeyDefaults.Plan(0, entries);
    }
}
