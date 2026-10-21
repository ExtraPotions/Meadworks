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
    /// The deaths kept for watching, newest first, for as long as the game runs. A death opens a pending recap with the
    /// hits so far; the video keeps recording for <see cref="Tail"/> seconds (the fall and the ragdoll), and once the
    /// last pictures are encoded the recap takes its frames and joins the list. Respawning is never held up: the game
    /// waits ten seconds before it respawns anyway, and none of this touches the game's own death.
    /// </summary>
    internal static class RecapStore
    {
        /// <summary>Seconds of video kept after the moment of death.</summary>
        public const float Tail = 2f;

        /// <summary>The longest a recap waits for late pictures before it is made with what has arrived.</summary>
        private const float MaxWait = 3f;

        private static DeathRecap? _pending;

        public static readonly List<DeathRecap> Deaths = new List<DeathRecap>();

        /// <summary>A recap joined the list.</summary>
        public static event Action? Changed;

        /// <summary>The screen should be recorded: the local player is alive, or died less than <see cref="Tail"/> ago.</summary>
        public static bool Recording => _pending != null
            ? Time.time <= _pending.Died + Tail
            : Player.m_localPlayer != null && !Player.m_localPlayer.IsDead();

        internal static void CancelPending() { _pending?.Forget(); _pending = null; }
        internal static void Reset()
        {
            CancelPending();
            foreach (DeathRecap death in Deaths) death.Forget();
            Deaths.Clear();
        }

        public static void OnDeath(Player player)
        {
            if (_pending != null)
            {
                Finish(_pending);
            }
            float now = Time.time;
            DeathRecap recap = new DeathRecap
            {
                Died = now,
                Hits = HitLog.Take(now - RecapSettings.Seconds.Value),
                Clock = DateTime.Now,
                Day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0,
            };
            Blame(recap, player);
            _pending = recap;
        }

        /// <summary>Every frame: makes the pending recap once its last pictures are in.</summary>
        public static void Update()
        {
            DeathRecap? recap = _pending;
            if (recap == null)
            {
                return;
            }
            float since = Time.time - recap.Died - Tail;
            bool settled = FrameEncoder.Pending == 0 && FrameGrabber.InFlight == 0;
            if ((since >= 0f && settled) || since >= MaxWait)
            {
                Finish(recap);
            }
        }

        private static void Finish(DeathRecap recap)
        {
            _pending = null;
            recap.Frames = FrameRing.Take(recap.Died - RecapSettings.Seconds.Value, recap.Died + Tail);
            FrameRing.Clear();
            Span(recap);
            recap.Thumbnail = RecapPictures.Thumbnail(recap);
            Deaths.Insert(0, recap);
            while (Deaths.Count > Mathf.Clamp(RecapSettings.Kept.Value, 1, 10))
            {
                Deaths[Deaths.Count - 1].Forget();
                Deaths.RemoveAt(Deaths.Count - 1);
            }
            Changed?.Invoke();
            RecapNotice.Show(recap);
        }

        // The clip runs from its first frame to its last; without video, from the first hit (or a second before) to death.
        private static void Span(DeathRecap recap)
        {
            if (recap.HasVideo)
            {
                recap.Start = recap.Frames[0].Time;
                recap.End = recap.Frames[recap.Frames.Count - 1].Time;
                return;
            }
            recap.Start = recap.Hits.Count > 0 ? Mathf.Min(recap.Hits[0].Time, recap.Died - 1f) : recap.Died - 1f;
            recap.End = recap.Died;
        }

        // The killing blow is the last hit taken; with none logged (a console kill), the game's own last hit says how.
        private static void Blame(DeathRecap recap, Player player)
        {
            HitRecord? last = recap.Hits.Count > 0 ? recap.Hits[recap.Hits.Count - 1] : null;
            HitData? gameHit = player.m_lastHit;
            recap.Cause = last != null ? last.Source : gameHit != null ? HitDescribe.Source(gameHit) : "";
            string dealer = last != null ? last.Attacker : HitDescribe.Attacker(gameHit?.GetAttacker());
            recap.Dealt = dealer.Length > 0;
            recap.Killer = recap.Dealt ? dealer : Window.RecapText.Capital(recap.Cause.Length > 0 ? recap.Cause : "unknown");
        }
    }
}
