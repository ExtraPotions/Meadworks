// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: which map pins the compass shows, per world and character (design C4), stored in character custom
// data. Entries are pin types (type:Death) or single pins (pin:x,z,type with whole meters). Tombstones are on by default.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Cairn.Hud
{
    public sealed class MarkerChoices
    {
        public const string Header = "cairn-markers 1";

        private readonly SortedSet<string> entries;

        private MarkerChoices(IEnumerable<string> entries) => this.entries = new SortedSet<string>(entries, StringComparer.Ordinal);

        /// <summary>A fresh character: tombstones only (TimeWeatherHud's default).</summary>
        public static MarkerChoices Default => new MarkerChoices(new[] { "type:Death" });

        public static MarkerChoices Empty => new MarkerChoices(Array.Empty<string>());

        public IReadOnlyList<string> Entries => entries.ToList();

        public bool Contains(string entry) => entries.Contains(entry);

        public MarkerChoices With(string entry)
        {
            if (!IsEntry(entry)) throw new ArgumentException("Not a marker entry: " + entry, nameof(entry));
            return new MarkerChoices(entries.Concat(new[] { entry }));
        }

        public MarkerChoices Without(string entry) => new MarkerChoices(entries.Where(e => e != entry));

        /// <summary>Character custom data key for one world.</summary>
        public static string DataKey(long worldId) => CairnCore.DataPrefix + "compassMarkers." + worldId.ToString(CultureInfo.InvariantCulture);

        /// <summary>A single pin's entry, its position rounded to whole meters.</summary>
        public static string PinKey(float x, float z, string pinType) =>
            "pin:" + Math.Round(x, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + ","
            + Math.Round(z, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "," + pinType;

        /// <summary><c>type:&lt;PinType&gt;</c> or <c>pin:&lt;x&gt;,&lt;z&gt;,&lt;PinType&gt;</c>.</summary>
        public static bool IsEntry(string? entry)
        {
            if (string.IsNullOrEmpty(entry)) return false;
            if (entry!.StartsWith("type:", StringComparison.Ordinal)) return IsName(entry.Substring(5));
            if (!entry.StartsWith("pin:", StringComparison.Ordinal)) return false;
            var parts = entry.Substring(4).Split(',');
            return parts.Length == 3 && int.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _)
                && int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _) && IsName(parts[2]);
        }

        public string Serialize() => string.Join("\n", new[] { Header }.Concat(entries));

        /// <summary>Stored text back to choices; nothing stored reads the default; unknown lines are skipped.</summary>
        public static MarkerChoices Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Default;
            var lines = text!.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            if (lines.Count == 0 || lines[0] != Header) return Default;
            return new MarkerChoices(lines.Skip(1).Where(IsEntry));
        }

        private static bool IsName(string name) => name.Length > 0 && name.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}
