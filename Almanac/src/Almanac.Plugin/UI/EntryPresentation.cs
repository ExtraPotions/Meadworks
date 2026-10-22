// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Almanac.Attributes;
using ExtraPotions.Almanac.Display;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Almanac.Runtime.UI
{
    internal static class EntryPresentation
    {
        internal static ConfigPresentationText Resolve(NativeEntry entry, string language, Func<string,string> text)
            => MeadworksConfig.Presentation(entry.Entry, language,
                text(Presentation.DisplayName(entry.Attributes, entry.Entry.Definition.Key)),
                text(Presentation.Heading(entry.Attributes, entry.Entry.Definition.Section)),
                SettingNotes.WithoutSyncSuffix(entry.Attributes.Description ?? entry.Entry.Description.Description));

        // Choice-list heading: the row's localized setting name. Other mods keep the raw display name.
        internal static string ChoiceDialogTitle(NativeEntry entry, string language, Func<string,string> text)
            => Resolve(entry, language, text).Label;

        internal static string SectionTitle(string fallback, IEnumerable<string> labels)
        {
            string? selected = null;
            foreach (string label in labels)
            {
                if (string.IsNullOrWhiteSpace(label) || string.Equals(label, fallback, StringComparison.Ordinal)) continue;
                if (selected != null && !string.Equals(selected, label, StringComparison.Ordinal)) return fallback;
                selected = label;
            }
            return selected ?? fallback;
        }
    }
}
