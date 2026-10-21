// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The owner's rolling record of the damage, by type, that players and their allies dealt an Adaptive boss in the
    /// last `window` seconds. Every hit adds one entry per type it carried; entries older than the window drop off
    /// the front, and the totals are summed afresh from what is left, so nothing drifts over a long fight. The
    /// dominant type is the one with the most in the window: a tie keeps the type already resisted, so the colour
    /// never flickers between two equal types, and an empty window means none. Local to the owner and never sent: a
    /// new owner starts its own (see <see cref="AdaptiveBehaviour"/>).
    /// </summary>
    internal sealed class AdaptiveWindow
    {
        private readonly struct Entry
        {
            public readonly float Time;
            public readonly AdaptiveType Type;
            public readonly float Amount;

            public Entry(float time, AdaptiveType type, float amount)
            {
                Time = time;
                Type = type;
                Amount = amount;
            }
        }

        private readonly Queue<Entry> _entries = new Queue<Entry>();
        private readonly float[] _totals = new float[AdaptiveTypes.Last + 1];

        public void Add(float time, AdaptiveType type, float amount)
        {
            if (type != AdaptiveType.None && amount > 0f)
            {
                _entries.Enqueue(new Entry(time, type, amount));
            }
        }

        public void Clear() => _entries.Clear();

        /// <summary>The type with the most in the last `window` seconds; a tie keeps the current one.</summary>
        public AdaptiveType Dominant(float now, float window, AdaptiveType current)
        {
            if (window <= 0f)
            {
                Clear(); // a window of nothing tracks nothing: the boss never adapts
            }
            while (_entries.Count > 0 && _entries.Peek().Time < now - window)
            {
                _entries.Dequeue();
            }
            Sum();
            return Pick(current);
        }

        private void Sum()
        {
            System.Array.Clear(_totals, 0, _totals.Length);
            foreach (Entry entry in _entries)
            {
                _totals[(int)entry.Type] += entry.Amount;
            }
        }

        private AdaptiveType Pick(AdaptiveType current)
        {
            AdaptiveType best = AdaptiveType.None;
            float most = 0f;
            for (int i = 1; i <= AdaptiveTypes.Last; i++)
            {
                if (_totals[i] > most)
                {
                    best = (AdaptiveType)i;
                    most = _totals[i];
                }
            }
            return current != AdaptiveType.None && most > 0f && _totals[(int)current] >= most ? current : best;
        }
    }
}
