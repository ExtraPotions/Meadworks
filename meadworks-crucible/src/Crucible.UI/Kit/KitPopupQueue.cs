// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// Engine-free arrival-order queue for notices that wait for room on screen. The head shows when it gets a place, or
    /// is forced into one when nothing else is shown (nothing left to free a place), so no notice is ever dropped. A head
    /// that has to wait holds every later notice, which keeps the arrival order.
    /// </summary>
    public sealed class KitPopupQueue<T> where T : class
    {
        readonly List<T> waiting = new List<T>();

        public int Count => waiting.Count;
        public bool Contains(T item) => item != null && waiting.Contains(item);

        public void Enqueue(T item)
        {
            if (item != null && !waiting.Contains(item)) waiting.Add(item);
        }

        /// <summary>Forgets waiting notices that no longer exist.</summary>
        public void Prune(Func<T, bool> gone) => waiting.RemoveAll(item => gone(item));

        /// <summary>
        /// Shows waiting notices from the head while <paramref name="place"/> takes them. Its second argument is true
        /// when <paramref name="anyShown"/> reports nothing on screen, and the head must then be taken once it is ready.
        /// Returns how many were shown.
        /// </summary>
        public int Release(Func<T, bool, bool> place, Func<bool> anyShown, Action<T> show)
        {
            int shown = 0;
            while (waiting.Count > 0)
            {
                var head = waiting[0];
                if (!place(head, !anyShown())) break;
                waiting.RemoveAt(0);
                show(head);
                shown++;
            }
            return shown;
        }

        /// <summary>Shows every waiting notice at once, in arrival order.</summary>
        public void ReleaseAll(Action<T> show)
        {
            var all = waiting.ToArray();
            waiting.Clear();
            foreach (var item in all) show(item);
        }
    }
}
