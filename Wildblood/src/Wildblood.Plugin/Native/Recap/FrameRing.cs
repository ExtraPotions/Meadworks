// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Adopted.Recap
{
    /// <summary>One recorded picture of the screen: a JPEG, stamped with the game time it was taken.</summary>
    internal sealed class Frame
    {
        public float Time;
        public byte[] Jpeg = null!;
    }

    /// <summary>
    /// The last seconds of the screen as JPEG frames, oldest first. Filled by the encoder thread, read by the main thread
    /// when a recap is made, so every access holds the lock. Only the newest seconds are kept: about 7 MB at the defaults.
    /// </summary>
    internal static class FrameRing
    {
        private static readonly object _lock = new object();
        private static readonly List<Frame> _frames = new List<Frame>();

        /// <summary>Adds a frame and drops every frame more than <paramref name="keep"/> seconds older than it.</summary>
        public static void Add(Frame frame, float keep)
        {
            lock (_lock)
            {
                int at = _frames.Count;
                while (at > 0 && _frames[at - 1].Time > frame.Time)
                {
                    at--;
                }
                _frames.Insert(at, frame);
                int old = 0;
                while (old < _frames.Count && _frames[old].Time < frame.Time - keep)
                {
                    old++;
                }
                _frames.RemoveRange(0, old);
            }
        }

        /// <summary>The frames taken from <paramref name="from"/> to <paramref name="to"/>, in order.</summary>
        public static List<Frame> Take(float from, float to)
        {
            lock (_lock)
            {
                return _frames.FindAll(f => f.Time >= from && f.Time <= to);
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _frames.Clear();
            }
        }
    }
}
