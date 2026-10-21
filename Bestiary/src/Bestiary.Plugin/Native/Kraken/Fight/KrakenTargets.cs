// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// Where the kraken's blows go, worked out in the held ship's own space: which player each tentacle strikes at, which
    /// side of the ship and how far along it the head comes up, who is within the bite and who gets the ink, which
    /// tentacle smashes the ship. The owner decides with these; every machine uses the same shapes to draw.
    /// </summary>
    public static class KrakenTargets
    {
        public const float BesideGap = 1.8f;      // metres from the hull's widest point to the head's base: in reach of a blade at the rail
        public const float GripSpread = 3f;       // metres along the hull from the head to each gripping tentacle
        public const float BesideDepth = 0.6f;    // metres the head's base sits under the water
        private const float Reach = TentacleSpec.Length - 2f;
        private const float BiteReach = 2.6f;
        private const float LungeReach = 4.8f;    // metres in from the rail the whole head can throw itself
        private const float InkRange = 28f;
        private const float ChestHeight = 1.1f;

        /// <summary>Where the head's base comes up beside the ship, in the world, <paramref name="depth"/> under the water.</summary>
        public static Vector3 HeadSpot(Ship ship, int side, float along, float depth)
        {
            ShipHull hull = ShipHull.Of(ship);
            return ship.transform.TransformPoint(new Vector3(side * (hull.HalfWidth + BesideGap), KrakenShips.Waterline(ship) - depth, along));
        }

        /// <summary>
        /// Where along the hull tentacle <paramref name="i"/> grips the rail while the head is up, or null if it stays
        /// under. Of the three on the head's side, the one nearest the head stays under (the head is where it would rise);
        /// the other two grip the rail <see cref="GripSpread"/> metres to either side of the head, the one further forward
        /// in front, kept on the hull.
        /// </summary>
        public static float? GripAlong(Ship? ship, int side, float along, int i)
        {
            if (ship == null || ShipHull.SideOf(i) != side)
            {
                return null;
            }
            ShipHull hull = ShipHull.Of(ship);
            int first = side < 0 ? 0 : 3, nearest = first;
            for (int k = first + 1; k < first + 3; k++)
            {
                nearest = Mathf.Abs(hull.Anchor(k).z - along) < Mathf.Abs(hull.Anchor(nearest).z - along) ? k : nearest;
            }
            if (i == nearest)
            {
                return null;
            }
            float end = hull.HalfLength * 0.9f;
            float spot = along + (hull.Anchor(i).z > hull.Anchor(nearest).z ? GripSpread : -GripSpread);
            return Mathf.Clamp(spot, hull.MiddleZ - end, hull.MiddleZ + end);
        }

        /// <summary>What tentacle <paramref name="i"/> slams at: the crew member nearest its base in reach, or straight across.</summary>
        public static Vector3 SlamTarget(Ship ship, int i, List<Player> crew)
        {
            Vector3 anchor = ShipHull.Of(ship).Anchor(i);
            Player? nearest = Nearest(ship, anchor, crew, Reach);
            return nearest != null ? Local(ship, nearest, 0f) : new Vector3(0f, 0f, anchor.z);
        }

        /// <summary>
        /// The side (+1 right, -1 left) and the place along the ship where the head comes up: level with the most of the
        /// crew, on the side with no ladder when only one side has any (else the crew's side), and off the ladders
        /// (<see cref="ShipLadders"/>).
        /// </summary>
        public static (int side, float along) HeadPlace(Ship ship, List<Player> crew)
        {
            ShipHull hull = ShipHull.Of(ship);
            Vector3 middle = Vector3.zero;
            crew.ForEach(player => middle += Local(ship, player, 0f));
            middle = crew.Count > 0 ? middle / crew.Count : new Vector3(Random.Range(-1f, 1f), 0f, hull.MiddleZ);
            float min = hull.MiddleZ - hull.HalfLength * 0.45f, max = hull.MiddleZ + hull.HalfLength * 0.45f;
            List<Vector3> ladders = ShipLadders.Of(ship);
            int side = ShipLadders.FreeSide(ladders, middle.x >= 0f ? 1 : -1);
            return (side, ShipLadders.Clearing(ladders, side, Mathf.Clamp(middle.z, min, max), min, max));
        }

        /// <summary>The crew member nearest the beak's reach over the rail, or null.</summary>
        public static Player? Biteable(Ship ship, int side, float along, List<Player> crew) =>
            Nearest(ship, Rail(ship, side, along), crew, BiteReach);

        /// <summary>The crew member nearest the rail in front of the head within a lunge of the whole head, or null.</summary>
        public static Player? Lungeable(Ship ship, int side, float along, List<Player> crew) =>
            Nearest(ship, Rail(ship, side, along), crew, LungeReach);

        /// <summary>The crew member the ink goes to: the nearest to the head beyond the beak's reach, or null.</summary>
        public static Player? Inkable(Ship ship, int side, float along, List<Player> crew)
        {
            Player? close = Biteable(ship, side, along, crew);
            Vector3 head = Rail(ship, side, along);
            Player? best = null;
            foreach (Player player in crew)
            {
                float distance = KrakenShips.Level(Local(ship, player, 0f) - head);
                if (player != close && distance <= InkRange && (best == null || distance < KrakenShips.Level(Local(ship, best, 0f) - head)))
                {
                    best = player;
                }
            }
            return best;
        }

        /// <summary>A tentacle on the far side from the head, to smash across the ship towards it.</summary>
        public static int SmashArm(int side) => (side > 0 ? 0 : 3) + Random.Range(0, 3);

        /// <summary>Where a smash goes: across the deck from its base to the head's side.</summary>
        public static Vector3 SmashTarget(Ship ship, int i, int side)
        {
            ShipHull hull = ShipHull.Of(ship);
            return new Vector3(side * hull.HalfWidth * 0.6f, 0f, hull.Anchor(i).z);
        }

        /// <summary>A player's place in the ship's space, <paramref name="up"/> metres above their feet.</summary>
        public static Vector3 Local(Ship ship, Player player, float up) =>
            ship.transform.InverseTransformPoint(player.transform.position + Vector3.up * up);

        /// <summary>A player's chest in the ship's space: what the head aims at.</summary>
        public static Vector3 Chest(Ship ship, Player player) => Local(ship, player, ChestHeight);

        // The rail in front of the head: the middle of the beak's reach.
        private static Vector3 Rail(Ship ship, int side, float along) =>
            new Vector3(side * (ShipHull.Of(ship).HalfWidth + 0.3f), 0f, along);

        private static Player? Nearest(Ship ship, Vector3 point, List<Player> crew, float within)
        {
            Player? best = null;
            float bestDistance = within;
            foreach (Player player in crew)
            {
                float distance = KrakenShips.Level(Local(ship, player, 0f) - point);
                if (distance <= bestDistance)
                {
                    (best, bestDistance) = (player, distance);
                }
            }
            return best;
        }
    }
}
