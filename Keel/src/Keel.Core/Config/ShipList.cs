// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the cheat ship lists (design 4.6): prefab names separated by commas, or * for every ship.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Keel.Config
{
    /// <summary>
    /// A list of ship prefab names, or every ship (<c>*</c>). Names are trimmed and matched exactly (prefab names are
    /// case-sensitive in the game); empty parts and duplicates are dropped; a <c>*</c> anywhere means every ship.
    /// </summary>
    public sealed class ShipList
    {
        private readonly HashSet<string> names;

        private ShipList(bool all, IReadOnlyList<string> ordered)
        {
            All = all;
            Names = ordered;
            names = new HashSet<string>(ordered, StringComparer.Ordinal);
        }

        public static ShipList Empty { get; } = new ShipList(false, Array.Empty<string>());

        public static ShipList Every { get; } = new ShipList(true, Array.Empty<string>());

        /// <summary>Every ship.</summary>
        public bool All { get; }

        /// <summary>The named ships in list order; empty when <see cref="All"/>.</summary>
        public IReadOnlyList<string> Names { get; }

        public bool IsEmpty => !All && Names.Count == 0;

        public static ShipList Parse(string? text)
        {
            var ordered = new List<string>();
            foreach (var part in (text ?? "").Split(','))
            {
                var name = part.Trim();
                if (name.Length == 0) continue;
                if (name == "*") return Every;
                if (!ordered.Contains(name, StringComparer.Ordinal)) ordered.Add(name);
            }
            return ordered.Count == 0 ? Empty : new ShipList(false, ordered);
        }

        /// <summary>Whether the ship with this prefab name is on the list.</summary>
        public bool Matches(string? prefab) => !string.IsNullOrEmpty(prefab) && (All || names.Contains(prefab!));

        /// <summary>The canonical text: <c>*</c>, names joined by <c>", "</c>, or empty.</summary>
        public override string ToString() => All ? "*" : string.Join(", ", Names);

        /// <summary>The rule a list setting normalizes with.</summary>
        public static string Normalize(string? text) => Parse(text).ToString();
    }
}