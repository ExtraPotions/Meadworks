// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// Which attack the Executioner's animator is playing and how far into its clip, the same on every peer (the game
    /// syncs the animator's triggers and states): the attack blending in if there is one, else the current state's.
    /// Everything the clips cannot do is drawn by this clock, so it needs no network of its own.
    /// </summary>
    public static class HeadsmanClock
    {
        public static (HeadsmanMove? move, float time) Read(Animator animator)
        {
            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                HeadsmanMove? incoming = HeadsmanMoves.ByState(next.shortNameHash);
                if (incoming != null)
                {
                    return (incoming, Seconds(next, incoming));
                }
            }
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            HeadsmanMove? move = HeadsmanMoves.ByState(state.shortNameHash);
            return move == null ? (null, -1f) : (move, Seconds(state, move));
        }

        private static float Seconds(AnimatorStateInfo state, HeadsmanMove move) => Mathf.Min(state.normalizedTime, 1f) * move.Length;

        /// <summary>Whether `at` was passed between two readings of the same attack (`from` is -1 on its first).</summary>
        public static bool Crossed(float from, float time, float at) => at >= 0f && from < at && time >= at;
    }
}
