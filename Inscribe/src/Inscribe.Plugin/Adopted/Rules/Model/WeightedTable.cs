// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// An immutable weighted draw: entries with a positive weight and their running total, so a pick is one binary
    /// search. Built at rule load (drop tables per tier), never per kill.
    /// </summary>
    public sealed class WeightedTable<T>
    {
        public static readonly WeightedTable<T> Empty = new WeightedTable<T>(Array.Empty<T>(), Array.Empty<float>());

        private readonly T[] _items;
        private readonly float[] _cumulative;

        private WeightedTable(T[] items, float[] cumulative)
        {
            _items = items;
            _cumulative = cumulative;
        }

        public int Count => _items.Length;
        public float Total => _cumulative.Length == 0 ? 0f : _cumulative[_cumulative.Length - 1];
        public IReadOnlyList<T> Items => _items;

        /// <summary>Builds from (item, weight) pairs; weights of 0 or less are left out.</summary>
        public static WeightedTable<T> Build(IEnumerable<KeyValuePair<T, float>> entries)
        {
            List<T> items = new List<T>();
            List<float> cumulative = new List<float>();
            float total = 0f;
            foreach (KeyValuePair<T, float> entry in entries)
            {
                if (entry.Value > 0f && !float.IsInfinity(entry.Value))
                {
                    total += entry.Value;
                    items.Add(entry.Key);
                    cumulative.Add(total);
                }
            }
            return items.Count == 0 ? Empty : new WeightedTable<T>(items.ToArray(), cumulative.ToArray());
        }

        /// <summary>Picks with a uniform number in [0, 1). False when the table is empty.</summary>
        public bool TryPick(float unit, out T item)
        {
            item = default!;
            if (_items.Length == 0)
            {
                return false;
            }
            float target = Math.Max(0f, Math.Min(unit, 0.9999999f)) * Total;
            int index = Array.BinarySearch(_cumulative, target);
            index = index < 0 ? ~index : index + 1;
            item = _items[Math.Min(index, _items.Length - 1)];
            return true;
        }
    }
}
