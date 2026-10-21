// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// Where a ship's ladders are, in its own space (x right, z forward), found by the game's ladder component, so a
    /// modded ship's ladders count too. The kraken's head keeps off them (the user, 2026-09-28): it comes up on the side
    /// with no ladder when only one side has any (the raft and the karve have one, on the left), and on a ship with
    /// ladders down both sides (the longships) it keeps a few metres along the hull from the ladder on its side, so the
    /// crew can always climb back aboard, and a player it has thrown into the sea can too.
    /// </summary>
    public static class ShipLadders
    {
        private const float Clear = 3f;   // metres along the hull the head keeps from a ladder on its side

        /// <summary>Every ladder on the ship, in the ship's space.</summary>
        public static List<Vector3> Of(Ship ship)
        {
            var found = new List<Vector3>();
            foreach (Ladder ladder in ship.GetComponentsInChildren<Ladder>(true))
            {
                found.Add(ship.transform.InverseTransformPoint(ladder.transform.position));
            }
            return found;
        }

        /// <summary>The side (+1 right, -1 left) with no ladder, when only one side has any; else <paramref name="otherwise"/>.</summary>
        public static int FreeSide(List<Vector3> ladders, int otherwise)
        {
            bool left = ladders.Exists(at => at.x < 0f), right = ladders.Exists(at => at.x >= 0f);
            return left == right ? otherwise : left ? 1 : -1;
        }

        /// <summary>
        /// <paramref name="along"/> moved clear of any ladder on <paramref name="side"/>, staying between
        /// <paramref name="min"/> and <paramref name="max"/>; left as it is when there is no room to move.
        /// </summary>
        public static float Clearing(List<Vector3> ladders, int side, float along, float min, float max)
        {
            foreach (Vector3 ladder in ladders)
            {
                if ((ladder.x >= 0f ? 1 : -1) == side && Mathf.Abs(along - ladder.z) < Clear)
                {
                    along = Away(ladder.z, along, min, max);
                }
            }
            return along;
        }

        // To whichever end of the ladder's clearance is nearer, if it fits.
        private static float Away(float ladder, float along, float min, float max)
        {
            float ahead = ladder + Clear, behind = ladder - Clear;
            bool aheadFits = ahead <= max, behindFits = behind >= min;
            if (aheadFits && behindFits)
            {
                return along >= ladder ? ahead : behind;
            }
            return aheadFits ? ahead : behindFits ? behind : along;
        }
    }
}
