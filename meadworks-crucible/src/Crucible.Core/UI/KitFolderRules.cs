// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Meadworks.Crucible
{
    public sealed class KitFolderLine
    {
        public string Id { get; }
        public string Text { get; }
        public int Depth { get; }
        public bool Current { get; }
        public bool Up { get; }
        public bool Renames { get; }
        public KitFolderLine(string id, string text, int depth = 0, bool current = false, bool up = false, bool renames = true)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Text = text ?? id;
            Depth = Math.Max(0, Math.Min(8, depth));
            Current = current;
            Up = up;
            Renames = renames && id.Length > 0;
        }
    }

    public static class KitFolderRules
    {
        public static void CheckRows(IReadOnlyList<KitFolderLine> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
                if (row == null || !ids.Add(row.Id)) throw new ArgumentException("Folder rows require unique ids.", nameof(rows));
        }

        // Drop whole ancestors first; keep the current name reachable even in a narrow menu.
        public static int BreadcrumbStart(float available, IReadOnlyList<float> widths)
        {
            if (widths == null || widths.Count == 0) return 0;
            float used = 0;
            int start = widths.Count - 1;
            for (int i = widths.Count - 1; i >= 0; i--)
            {
                float width = Math.Max(0, widths[i]) + (i < widths.Count - 1 ? 4 : 0);
                if (i < widths.Count - 1 && used + width > available) break;
                used += width;
                start = i;
            }
            return start;
        }

        public static string? Name(string? value, int limit)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed) || limit < 1 || trimmed!.Length > limit) return null;
            foreach (char c in trimmed) if (char.IsControl(c)) return null;
            return trimmed;
        }
    }
}
