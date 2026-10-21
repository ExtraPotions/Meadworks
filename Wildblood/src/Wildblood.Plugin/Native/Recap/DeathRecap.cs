// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Recap
{
    /// <summary>
    /// One death, kept for watching: the video frames of the last seconds and a moment after, the hits taken, who dealt
    /// the last one, and when it happened. Times inside the recap are seconds from its first frame (the clip's 0:00).
    /// </summary>
    internal sealed class DeathRecap
    {
        public List<Frame> Frames = new List<Frame>();
        public List<HitRecord> Hits = new List<HitRecord>();

        /// <summary>Game time of the clip's start, of the death, and of the clip's end.</summary>
        public float Start;
        public float Died;
        public float End;

        /// <summary>The killing blow's dealer (or its cause when nobody dealt it) and how it came.</summary>
        public string Killer = "";
        public string Cause = "";

        /// <summary>A creature or player dealt the killing blow (false: a fall, drowning, the game's other causes).</summary>
        public bool Dealt;

        public DateTime Clock;
        public int Day;

        /// <summary>The picture at the moment of death, for the list; null without video.</summary>
        public Texture2D? Thumbnail;

        public float Duration => Mathf.Max(0.01f, End - Start);
        public float DeathAt => Died - Start;
        public bool HasVideo => Frames.Count > 0;

        /// <summary>The frame showing at <paramref name="t"/> seconds into the clip: the last one taken by then.</summary>
        public int FrameAt(float t) => LastAtOrBefore(Frames.Count, i => Frames[i].Time - Start, t);

        /// <summary>The latest hit by <paramref name="t"/> seconds into the clip, -1 before the first.</summary>
        public int HitAt(float t) => LastAtOrBefore(Hits.Count, i => Hits[i].Time - Start, t);

        public float FrameTime(int index) => Frames[index].Time - Start;

        public float HitTime(int index) => Mathf.Clamp(Hits[index].Time - Start, 0f, Duration);

        public void Forget()
        {
            if (Thumbnail != null)
            {
                UnityEngine.Object.Destroy(Thumbnail);
                Thumbnail = null;
            }
            Frames.Clear();
        }

        // Binary search over times in order: the last index whose time is at most t, -1 when none is.
        private static int LastAtOrBefore(int count, Func<int, float> timeOf, float t)
        {
            int low = 0;
            int high = count - 1;
            int found = -1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (timeOf(mid) <= t)
                {
                    found = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }
            return found;
        }
    }
}
