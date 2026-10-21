// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Bestiary.Catalog
{
    /// <summary>
    /// Where the Crypt Executioner stands and where its axe head lands, in meters on the ground plane, so a player
    /// standing in front of it is reliably hit and one further off is not. The game's AI walks until the distance to its
    /// target (less the target's radius) is under the attack's AI range, then stops and attacks from where it came to rest.
    /// The numbers are measured in game (Bestiary 0.1.3, single player, probe frames of network-2ebf38c0): at an AI range
    /// of 3 it rested 2.9 m from a stationary player; from there the slam's edge landed 1.7 m from the player, the
    /// low sweep passed 2.0 to 3.0 m and the spin's ring 2.1 to 4.4 m, none within the axe head's 0.5 m of the player's
    /// body. 0.1.4 moves the three melee attacks' AI range in so the creature rests where the axe lands.
    /// </summary>
    public static class ExecutionerReach
    {
        /// <summary>
        /// AI range of the slam, the low sweep, the spin and the rear strike (they share one, so every pick rests at the same
        /// spot, and no longer-range attack can be equipped alone between two ranges and leave the creature waiting).
        /// </summary>
        public const float MeleeAiRange = 1.7f;

        /// <summary>Default of Axe Head Radius: meters round the blade's edge that hit.</summary>
        public const float DefaultAxeRadius = 0.6f;

        /// <summary>How far inside its AI range the creature rests (measured: AI range 3 rested at 2.9).</summary>
        public const float RestInside = 0.1f;

        /// <summary>A player's collision radius (assumed; the game's player capsule).</summary>
        public const float PlayerRadius = 0.5f;

        /// <summary>How far in front of the creature's center the slam's edge lands (measured: 1.25).</summary>
        public const float SlamAhead = 1.25f;

        /// <summary>How far in front of its center the low sweep's edge passes at the nearest (measured: 1.0).</summary>
        public const float SweepAhead = 1.0f;

        /// <summary>Radius of the ring the spin's edge draws round the creature (measured: 1.4).</summary>
        public const float SpinRing = 1.4f;

        /// <summary>The creature's own radius: the nearest a target can stand to its center.</summary>
        public const float BodyRadius = 0.5f;

        /// <summary>Where a creature using this AI range rests, in meters from a stationary target in front of it.</summary>
        public static float RestDistance(float aiRange) => aiRange - RestInside;

        /// <summary>How close the axe head's center must come to a target's axis to hit it.</summary>
        public static float HitReach(float axeRadius, float targetRadius) => axeRadius + targetRadius;

        /// <summary>The slam's miss distance for a target in front at the rest distance (hit when at most <see cref="HitReach"/>).</summary>
        public static float SlamMiss(float aiRange) => Math.Abs(RestDistance(aiRange) - SlamAhead);

        /// <summary>The low sweep's miss distance (it crosses the front at <see cref="SweepAhead"/>).</summary>
        public static float SweepMiss(float aiRange) => Math.Abs(RestDistance(aiRange) - SweepAhead);

        /// <summary>The spin's miss distance (its ring is <see cref="SpinRing"/> round the creature).</summary>
        public static float SpinMiss(float aiRange) => Math.Abs(RestDistance(aiRange) - SpinRing);

        /// <summary>The farthest from the creature a stationary target is still hit by the slam; beyond it the swing lands short.</summary>
        public static float SlamFarthest(float axeRadius, float targetRadius) => SlamAhead + HitReach(axeRadius, targetRadius);

        /// <summary>Whether all three melee attacks hit a target resting in front at this AI range.</summary>
        public static bool HitsEveryMelee(float aiRange, float axeRadius, float targetRadius)
        {
            float reach = HitReach(axeRadius, targetRadius);
            return SlamMiss(aiRange) <= reach && SweepMiss(aiRange) <= reach && SpinMiss(aiRange) <= reach;
        }
    }
}
