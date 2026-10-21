// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Bring Tames (T18). The idea was seen in OdinPlus TeleportEverything (no license found; clean-room,
// built from its one-line feature description only). Followers are the game's own follow command (MonsterAI follow
// target), which Shepherd extends to every tameable; Threshold only moves them after a trip it routed.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Portals;

namespace ExtraPotions.Threshold.Routing
{
    /// <summary>A creature near the traveler when the trip starts. <see cref="Id"/> is the caller's handle.</summary>
    public readonly struct TameCandidate
    {
        public TameCandidate(int id, MapPoint position, bool tamed, bool followsTraveller, bool dead)
        {
            Id = id;
            Position = position;
            Tamed = tamed;
            FollowsTraveller = followsTraveller;
            Dead = dead;
        }

        public int Id { get; }
        public MapPoint Position { get; }
        public bool Tamed { get; }

        /// <summary>The game's follow command targets the traveler.</summary>
        public bool FollowsTraveller { get; }
        public bool Dead { get; }
    }

    public static class TameRules
    {
        /// <summary>Meters between the exit point and the first arc of tames, and between arcs.</summary>
        public const float Spacing = 2.5f;

        /// <summary>Tames per arc in front of the exit.</summary>
        const int PerArc = 6;

        /// <summary>Half the arc's spread either side of the destination's facing, in degrees.</summary>
        const float HalfArc = 75f;

        /// <summary>Horizontal meters from the exit point within which the traveler counts as landed.</summary>
        const float LandedRange = 6f;

        /// <summary>Living tamed followers of the traveler within <paramref name="radius"/>, nearest first (ties by id),
        /// at most <paramref name="max"/>. Radius and count are normalized by their settings.</summary>
        public static IReadOnlyList<int> Select(IEnumerable<TameCandidate> candidates, MapPoint traveller, float radius, int max)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            float r = ThresholdSettings.Tames.TameRadius.Normalize(radius);
            int count = ThresholdSettings.Tames.MaxTames.Normalize(max);
            return candidates
                .Where(c => c.Tamed && c.FollowsTraveller && !c.Dead && c.Position.IsFinite)
                .Select(c => (c.Id, Distance: Distance(c.Position, traveller)))
                .Where(c => c.Distance <= r)
                .OrderBy(c => c.Distance).ThenBy(c => c.Id)
                .Take(count)
                .Select(c => c.Id)
                .ToList();
        }

        /// <summary>Meters above the traveler's feet a floor under a tame's spot may be (a step up a slope or stair).</summary>
        public const float MaxStepUp = 2f;

        /// <summary>Meters below the traveler's feet a floor under a tame's spot may be (beyond it: a cliff or a hole).</summary>
        public const float MaxStepDown = 4f;

        /// <summary>Where tame <paramref name="index"/> of <paramref name="count"/> is placed before it is settled: on arcs
        /// in front of <paramref name="anchor"/> (the destination's facing <paramref name="yaw"/>, degrees), at the anchor's
        /// height. One tame lands straight ahead. The anchor is where the traveler finally landed.</summary>
        public static MapPoint Place(MapPoint anchor, float yaw, int index, int count)
        {
            int arc = Math.Max(0, index) / PerArc;
            int onArc = Math.Min(PerArc, Math.Max(1, count) - arc * PerArc);
            int slot = Math.Max(0, index) % PerArc;
            float offset = onArc <= 1 ? 0f : -HalfArc + 2f * HalfArc * slot / (onArc - 1);
            double radians = (yaw + offset) * Math.PI / 180.0;
            float distance = Spacing * (arc + 1);
            return new MapPoint((float)(anchor.X + Math.Sin(radians) * distance), anchor.Y, (float)(anchor.Z + Math.Cos(radians) * distance));
        }

        /// <summary>Height the floor probe for a tame's spot starts from: a little above the traveler's feet, so a roof
        /// over the landing is not taken for the floor.</summary>
        public static float ProbeHeight(MapPoint traveller) => traveller.Y + MaxStepUp;

        /// <summary>
        /// A tame's final position: its spot on <paramref name="floor"/> (the floor found under the spot, null when
        /// none) when that floor is within a step of the traveler's landing height; otherwise where the traveler
        /// stands, which the game already proved solid. A tame is never placed at the exit's raw height, which can be
        /// under the ground when the game had to lift the traveler onto it.
        /// </summary>
        public static MapPoint Settle(MapPoint spot, MapPoint traveller, float? floor)
        {
            if (floor is float y && !float.IsNaN(y) && !float.IsInfinity(y)
                && y <= traveller.Y + MaxStepUp && y >= traveller.Y - MaxStepDown)
                return new MapPoint(spot.X, y, spot.Z);
            return traveller;
        }

        /// <summary>The traveler stands at the exit (height ignored): the jump landed rather than being sent back.</summary>
        public static bool Landed(MapPoint traveller, MapPoint exit)
        {
            double dx = traveller.X - exit.X, dz = traveller.Z - exit.Z;
            return Math.Sqrt(dx * dx + dz * dz) <= LandedRange;
        }

        static double Distance(MapPoint a, MapPoint b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
