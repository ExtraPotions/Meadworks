// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Nightfall tornadoes kept for reuse, so each wave raises tornadoes already built (<see cref="TornadoParts"/>)
    /// instead of building their particle systems again. A tornado that has broken up comes back here switched off; up
    /// to <see cref="Keep"/> wait for the next wave and any more are destroyed. One the scene took away with it (a
    /// logout) is skipped.
    /// </summary>
    internal static class TornadoPool
    {
        private const int Keep = 8;

        private static readonly Stack<TornadoVisual> Idle = new Stack<TornadoVisual>();

        /// <summary>A tornado raised at <paramref name="at"/>, <paramref name="age"/> seconds into its life.</summary>
        public static TornadoVisual Raise(Vector3 at, TornadoShape shape, float form, float age)
        {
            TornadoVisual tornado = Take();
            tornado.Begin(at, shape, form, age);
            return tornado;
        }

        private static TornadoVisual Take()
        {
            while (Idle.Count > 0)
            {
                TornadoVisual kept = Idle.Pop();
                if (kept != null)
                {
                    return kept;
                }
            }
            return TornadoParts.Build();
        }

        /// <summary>Broken up: put away for the next wave, or destroyed when enough are already waiting.</summary>
        public static void Return(TornadoVisual tornado)
        {
            tornado.Rest();
            if (Idle.Count < Keep)
            {
                Idle.Push(tornado);
            }
            else
            {
                Object.Destroy(tornado.gameObject);
            }
        }
    }
}
