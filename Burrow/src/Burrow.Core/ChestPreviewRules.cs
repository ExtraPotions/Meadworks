// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (parity choices #19 and #59, the behavior described by ChestPreview's README and settings; no code
// taken): the rows of the chest preview panel - combining, sorting, the entry limit, the columns and the capacity text.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Burrow
{
    public enum PreviewSort { Value, Weight, Name, Amount }

    /// <summary>One stack of the chest as the preview sees it. <paramref name="name"/> is the game's $token.</summary>
    public sealed class PreviewItem
    {
        public PreviewItem(string name, string prefab, int quality, int variant, int count, float unitValue, float unitWeight)
        {
            Name = name ?? "";
            Prefab = prefab ?? "";
            Quality = quality;
            Variant = variant;
            Count = count;
            UnitValue = unitValue;
            UnitWeight = unitWeight;
        }

        public string Name { get; }
        public string Prefab { get; }
        public int Quality { get; }
        public int Variant { get; }
        public int Count { get; }
        public float UnitValue { get; }
        public float UnitWeight { get; }
        /// <summary>Set by the caller to find the stack's icon again; not part of the rules.</summary>
        public object? Tag { get; set; }
    }

    /// <summary>A row of the panel: one stack, or every stack of one item at one quality and variant combined.</summary>
    public sealed class PreviewRow
    {
        public PreviewRow(PreviewItem first, int count)
        {
            First = first;
            Count = count;
        }

        public PreviewItem First { get; }
        public int Count { get; }
        public string Name => First.Name;
        public string Prefab => First.Prefab;
        public int Quality => First.Quality;
        public float Value => First.UnitValue * Count;
        public float Weight => First.UnitWeight * Count;
    }

    public static class ChestPreviewRules
    {
        public const int MaxColumns = 3;

        /// <summary>Whether the panel shows a chest holding <paramref name="items"/> stacks; an empty one only when not hidden.</summary>
        public static bool ShowsChest(int items, bool hideEmpty) => !hideEmpty || items > 0;

        /// <param name="maxEntries">0 shows every row; otherwise the first rows, and <paramref name="hidden"/> counts the rest.</param>
        public static List<PreviewRow> Rows(IEnumerable<PreviewItem> items, bool combine, PreviewSort sort, bool descending, int maxEntries, out int hidden)
        {
            List<PreviewRow> rows = new List<PreviewRow>();
            if (combine)
            {
                foreach (IGrouping<string, PreviewItem> group in (items ?? Enumerable.Empty<PreviewItem>())
                    .GroupBy(i => i.Prefab + "\u0001" + i.Name + "\u0001" + i.Quality.ToString(CultureInfo.InvariantCulture) + "\u0001" + i.Variant.ToString(CultureInfo.InvariantCulture)))
                    rows.Add(new PreviewRow(group.First(), group.Sum(i => i.Count)));
            }
            else
            {
                foreach (PreviewItem item in items ?? Enumerable.Empty<PreviewItem>())
                    rows.Add(new PreviewRow(item, item.Count));
            }
            rows.Sort((a, b) => Compare(a, b, sort, descending));
            hidden = 0;
            if (maxEntries > 0 && rows.Count > maxEntries)
            {
                hidden = rows.Count - maxEntries;
                rows.RemoveRange(maxEntries, hidden);
            }
            return rows;
        }

        private static int Compare(PreviewRow a, PreviewRow b, PreviewSort sort, bool descending)
        {
            int primary;
            switch (sort)
            {
                case PreviewSort.Weight: primary = a.Weight.CompareTo(b.Weight); break;
                case PreviewSort.Name: primary = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); break;
                case PreviewSort.Amount: primary = a.Count.CompareTo(b.Count); break;
                default: primary = a.Value.CompareTo(b.Value); break;
            }
            if (descending)
                primary = -primary;
            if (primary != 0)
                return primary;
            int quality = b.Quality.CompareTo(a.Quality);
            if (quality != 0)
                return quality;
            int name = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            return name != 0 ? name : b.Count.CompareTo(a.Count);
        }

        /// <summary>Rows per column, left columns first; 1 to 3 columns, never an empty one.</summary>
        public static int[] Columns(int rows, int columns)
        {
            int count = Math.Max(1, Math.Min(MaxColumns, columns));
            if (rows <= 0)
                return new[] { 0 };
            count = Math.Min(count, rows);
            int[] split = new int[count];
            for (int i = 0; i < count; i++)
                split[i] = rows / count + (i < rows % count ? 1 : 0);
            return split;
        }

        /// <summary>The capacity beside the chest's name: {0} used slots, {1} all slots, {2} percent used. Empty hides it.</summary>
        public static string Capacity(int used, int total, string format)
        {
            if (string.IsNullOrEmpty(format))
                return "";
            int percent = total > 0 ? (int)Math.Round(100.0 * used / total) : 0;
            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, used, total, percent);
            }
            catch (FormatException)
            {
                return "";
            }
        }

        /// <summary>"name ★3 x12": the quality star only above 1 and when shown, the count only above 1.</summary>
        public static string Label(string name, int quality, int count, bool showQuality)
        {
            string label = name ?? "";
            if (showQuality && quality > 1)
                label += " ★" + quality.ToString(CultureInfo.InvariantCulture);
            if (count > 1)
                label += " x" + count.ToString(CultureInfo.InvariantCulture);
            return label;
        }
    }
}
