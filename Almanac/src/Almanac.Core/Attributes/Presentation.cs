// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Almanac.Attributes
{
    public enum Visibility { Visible, HiddenNotBrowsable, HiddenByServerPolicy, HiddenAdvanced }

    /// <summary>Visibility, naming, grouping and ordering rules from the attribute table (design 4.1, Q5).</summary>
    public static class Presentation
    {
        /// <param name="matchedBySearch">The current search matches this entry (reveals advanced entries, never hidden ones).</param>
        /// <param name="hiddenByServerPolicy">ConditionalConfigSync <c>IsHidden</c>: always hidden (Q5).</param>
        public static Visibility Decide(EntryAttributes a, bool showAdvanced, bool matchedBySearch, bool hiddenByServerPolicy)
        {
            if (a.Browsable == false) return Visibility.HiddenNotBrowsable;
            if (hiddenByServerPolicy) return Visibility.HiddenByServerPolicy;
            if (a.IsAdvanced == true && !showAdvanced && !matchedBySearch) return Visibility.HiddenAdvanced;
            return Visibility.Visible;
        }

        public static string DisplayName(EntryAttributes a, string key) => string.IsNullOrEmpty(a.DispName) ? key : a.DispName!;

        /// <summary>The heading the entry is listed under: <c>Category</c> when set, else the file section.</summary>
        public static string Heading(EntryAttributes a, string section) => string.IsNullOrEmpty(a.Category) ? section : a.Category!;

        /// <summary><c>HideSettingName</c> applies only while a custom drawer is drawn; the name is needed for search and gamepad focus otherwise.</summary>
        public static bool ShowsSettingName(EntryAttributes a, bool drawingCustom) => !(drawingCustom && a.HideSettingName == true);

        public static bool ShowsResetButton(EntryAttributes a) => a.HideDefaultButton != true;

        public static object? ResetTarget(EntryAttributes a, object? entryDefault) => a.HasDefaultOverride ? a.DefaultValue : entryDefault;

        /// <summary>
        /// ConfigurationManager ordering within a heading: higher <c>Order</c> first (unset counts as 0), then display name.
        /// Names compare ordinal ignoring case so the order is the same in every game language.
        /// </summary>
        public static IEnumerable<T> Sort<T>(IEnumerable<T> rows, Func<T, int?> order, Func<T, string> displayName)
            => rows.OrderByDescending(r => order(r) ?? 0).ThenBy(displayName, StringComparer.OrdinalIgnoreCase).ThenBy(displayName, StringComparer.Ordinal);
    }
}
