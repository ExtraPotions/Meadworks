// OpenKeep TabPicks. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/Blueprints/Tab/TabPicks.cs.
// Changed by expDARE, 2026: opaque ids, explicit ordered view and no Unity, product storage or rendering.
using System;
using System.Collections.Generic;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Explorer-style multi-selection, separate from the single piece chosen for building.</summary>
    public sealed class KitBuildTabPicks {
        readonly HashSet<string> picked = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> order = new List<string>();
        string? anchor;
        public bool Contains(string id) => id != null && picked.Contains(id);
        public IReadOnlyList<string> Snapshot() => order.FindAll(picked.Contains).AsReadOnly();
        /// <summary>Only these current, non-action entries are eligible; vanished entries and anchor are pruned.</summary>
        public void SetOrder(IReadOnlyList<string> shown) {
            KitBuildTabRules.CheckEntries(shown);
            order.Clear(); order.AddRange(shown);
            var present = new HashSet<string>(order, StringComparer.Ordinal);
            picked.RemoveWhere(id => !present.Contains(id));
            if (anchor != null && !present.Contains(anchor)) anchor = null;
        }
        public void Click(string id, bool ctrl, bool shift) {
            if (!order.Contains(id)) return;
            int from = anchor != null ? order.IndexOf(anchor) : -1, until = order.IndexOf(id);
            if (shift && from >= 0) {
                if (!ctrl) picked.Clear();
                for (int i = Math.Min(from,until); i <= Math.Max(from,until); i++) picked.Add(order[i]);
                return;
            }
            if (!picked.Remove(id)) picked.Add(id);
            anchor = id;
        }
        public void Box(IReadOnlyList<string> kept, IReadOnlyList<string> touched) {
            picked.Clear();
            foreach (string id in kept) if (order.Contains(id)) picked.Add(id);
            foreach (string id in touched) if (order.Contains(id)) picked.Add(id);
        }
        public void Clear() { picked.Clear(); anchor = null; }
    }
}
