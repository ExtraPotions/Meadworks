// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;

namespace ExtraPotions.Inscribe.Adopted.Rolling
{
    /// <summary>
    /// Random sources for rolls. On .NET Framework <c>new Random()</c> seeds from the tick count, so two contexts made
    /// in the same millisecond (a loot burst) would roll identically; <see cref="Create"/> seeds each new source from
    /// one shared generator instead. Tests pass their own seeded <see cref="Random"/>.
    /// </summary>
    public static class RollRandom
    {
        private static readonly Random Seeds = new Random(Guid.NewGuid().GetHashCode());
        private static readonly object Gate = new object();

        public static Random Create()
        {
            lock (Gate)
            {
                return new Random(Seeds.Next());
            }
        }
    }
}
