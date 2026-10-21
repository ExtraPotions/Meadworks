// Threshold - ExtraPotions Meadworks portals.
// Portions from ValheimModLibs AreaLoading (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: ValheimModLibs/AreaLoading/QuickWait.cs (Seconds, Speed, MinSeconds,
// MapDistance), used by Wayfare 0.3.2 Quick Portals (Wayfare/Wayfare/src/QuickJumps/JumpTiming.cs: GameArrival, MoveAt,
// PreMoveMax, Hurry, Advance and Hold).
// Changed by expDARE, 2026: Unity-free (Mathf replaced by System.Math), the game's 8 s long-jump wait as the fixed full
// wait; the timer steps as pure functions of the timer (Step, HoldLanding) so the plugin only reads and writes it. Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Threshold.Portals;

namespace ExtraPotions.Threshold.Routing
{
    /// <summary>A jump wait that grows with distance: the shortest wait at 0 m, the game's full wait at the range and beyond,
    /// in proportion between. The plugin runs the game's teleport timer faster by <see cref="Speed"/> rather than change the
    /// seconds, so the game's own "is the area loaded" checks stay.</summary>
    public static class JumpRules
    {
        /// <summary>The game's seconds before a long jump may land.</summary>
        public const float GameArrival = 8f;

        /// <summary>No timer runs more than this many times faster than the game's, so a wait never collapses to nothing.</summary>
        private const float MinSeconds = 0.05f;

        public static float Seconds(float metres, float range, float shortest)
        {
            float share = range > 0f ? Clamp01(metres / range) : 1f;
            float from = Math.Min(Math.Max(shortest, 0f), GameArrival);
            return from + (GameArrival - from) * share;
        }

        /// <summary>How many times faster than the game a timer runs so that the full wait lasts <paramref name="seconds"/>.</summary>
        public static float Speed(float seconds) => Speed(seconds, GameArrival);

        /// <summary>How many times faster than the game a timer span of <paramref name="span"/> seconds runs so it lasts <paramref name="seconds"/>.</summary>
        public static float Speed(float seconds, float span) => span / Math.Max(seconds, MinSeconds);

        /// <summary>The game's timer value at which the player is moved to the target.</summary>
        public const float MoveAt = 2f;

        /// <summary>Real seconds from stepping in to the move at most (the screen is black by then).</summary>
        public const float PreMoveMax = 0.25f;

        /// <summary>The distance on the ground plane, as the game's map measures it.</summary>
        public static float MapDistance(MapPoint a, MapPoint b)
        {
            double dx = a.X - b.X, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>The teleport timer after Threshold's push for one physics step of <paramref name="dt"/>, before the game
        /// adds its own <paramref name="dt"/>: the move comes after at most a quarter of <paramref name="seconds"/> (and
        /// <see cref="PreMoveMax"/>), the landing at <paramref name="seconds"/>. The timer is never pushed past the move or
        /// past <see cref="GameArrival"/>, so the game's area check and its 15 s no-floor fallback stay.</summary>
        public static float Step(float timer, float dt, float seconds)
        {
            if (timer >= GameArrival || seconds >= GameArrival || dt <= 0f) return timer;
            float preMove = Math.Min(PreMoveMax, seconds * 0.25f);
            if (timer < MoveAt) return Math.Min(timer + dt * (Speed(preMove, MoveAt) - 1f), MoveAt);
            return Math.Min(timer + dt * (Speed(seconds - preMove, GameArrival - MoveAt) - 1f), GameArrival);
        }

        /// <summary>After the move, keeps a jump from landing while the server still sends the target's objects
        /// (<paramref name="settled"/> false), but never beyond the game's own <see cref="GameArrival"/> from the start.</summary>
        public static float HoldLanding(float timer, float dt, float sinceStart, bool settled)
        {
            if (timer < MoveAt || sinceStart >= GameArrival || settled) return timer;
            return Math.Min(timer, GameArrival - dt * 1.5f);
        }

        private static float Clamp01(float value) => float.IsNaN(value) ? 1f : Math.Min(1f, Math.Max(0f, value));
    }
}
