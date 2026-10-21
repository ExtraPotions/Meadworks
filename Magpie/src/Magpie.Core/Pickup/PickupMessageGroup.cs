// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;

namespace ExtraPotions.Magpie.Pickup
{
    /// <summary>One pickup line to show: the item key, the summed amount and what the plugin needs to show it (icon, name).</summary>
    public sealed class PickupLine<T>
    {
        public PickupLine(string key, int amount, T payload, double opened)
        {
            Key = key;
            Amount = amount;
            Payload = payload;
            Opened = opened;
        }

        public string Key { get; }
        public int Amount { get; internal set; }
        public T Payload { get; }
        public double Opened { get; }
    }

    /// <summary>
    /// Design A11 (Meadworks rule): takes of the same item within one second show as one line with the summed amount.
    /// A line opens with its first take and is shown one second later; the next take of that item opens a new line.
    /// With grouping off each take is due at once. Lines come out in the order they opened.
    /// </summary>
    public sealed class PickupMessageGroup<T>
    {
        public const double Window = 1.0;

        private readonly List<PickupLine<T>> open = new List<PickupLine<T>>();
        private readonly List<bool> immediate = new List<bool>();

        public int Pending => open.Count;

        public void Add(string key, int amount, double now, T payload, bool grouping)
        {
            if (amount <= 0) return;
            if (grouping)
            {
                for (var i = 0; i < open.Count; i++)
                {
                    if (!immediate[i] && open[i].Key == key)
                    {
                        open[i].Amount += amount;
                        return;
                    }
                }
            }
            open.Add(new PickupLine<T>(key, amount, payload, now));
            immediate.Add(!grouping);
        }

        public IReadOnlyList<PickupLine<T>> Flush(double now)
        {
            var due = new List<PickupLine<T>>();
            for (var i = 0; i < open.Count;)
            {
                if (immediate[i] || now - open[i].Opened >= Window)
                {
                    due.Add(open[i]);
                    open.RemoveAt(i);
                    immediate.RemoveAt(i);
                }
                else i++;
            }
            return due;
        }

        public void Clear()
        {
            open.Clear();
            immediate.Clear();
        }
    }
}
