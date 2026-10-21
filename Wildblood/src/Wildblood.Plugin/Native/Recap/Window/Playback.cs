// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>
    /// Where the viewer is in the selected recap: the time into the clip, playing or paused, and the speed. Pure state;
    /// the window advances it every frame by real time (the game's own pause does not stop a replay).
    /// </summary>
    internal sealed class Playback
    {
        public static readonly float[] Speeds = { 0.25f, 0.5f, 1f, 2f };

        public DeathRecap? Recap { get; private set; }
        public float Time { get; private set; }
        public bool Playing { get; private set; }
        public float Speed { get; private set; } = 1f;

        /// <summary>A recap from its start, playing; null clears the viewer.</summary>
        public void Load(DeathRecap? recap)
        {
            Recap = recap;
            Time = 0f;
            Playing = recap != null;
        }

        public void Advance(float seconds)
        {
            if (!Playing || Recap == null)
            {
                return;
            }
            Time += seconds * Speed;
            if (Time >= Recap.Duration)
            {
                Time = Recap.Duration;
                Playing = false;
            }
        }

        public void Seek(float time)
        {
            if (Recap != null)
            {
                Time = Mathf.Clamp(time, 0f, Recap.Duration);
            }
        }

        /// <summary>Play or pause; play at the end starts again from the beginning.</summary>
        public void Toggle()
        {
            if (Recap == null)
            {
                return;
            }
            if (!Playing && Time >= Recap.Duration - 0.01f)
            {
                Time = 0f;
            }
            Playing = !Playing;
        }

        public void Pause() => Playing = false;

        public void SetSpeed(float speed) => Speed = speed;

        /// <summary>Paused, one video frame back or forward (with no video, a tenth of a second).</summary>
        public void Step(int frames)
        {
            if (Recap == null)
            {
                return;
            }
            Playing = false;
            if (!Recap.HasVideo)
            {
                Seek(Time + frames * 0.1f);
                return;
            }
            int index = Mathf.Clamp(Mathf.Max(0, Recap.FrameAt(Time)) + frames, 0, Recap.Frames.Count - 1);
            Seek(Recap.FrameTime(index));
        }
    }
}
