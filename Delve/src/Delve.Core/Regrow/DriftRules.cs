// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: a drifting world object keeps one spot. A second spot at the old coordinates would be restored
// while the object that moved is still in the world.
using System;
using System.Collections.Generic;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Regrow
{
    /// <summary>A remembered spot of one drifting prefab. <see cref="Id"/> is the caller's id.</summary>
    public readonly struct DriftSpot
    {
        public DriftSpot(int id, Vec3 position)
        {
            Id = id;
            Position = position;
        }

        public int Id { get; }
        public Vec3 Position { get; }
    }

    /// <summary>A spire standing now. <see cref="Whole"/> is false when it is damaged or leaving.</summary>
    public readonly struct DriftObject
    {
        public DriftObject(int id, Vec3 position, bool whole)
        {
            Id = id;
            Position = position;
            Whole = whole;
        }

        public int Id { get; }
        public Vec3 Position { get; }
        public bool Whole { get; }
    }

    /// <summary>Move <see cref="SpotId"/> onto the spire <see cref="ObjectId"/>.</summary>
    public readonly struct DriftMove
    {
        public DriftMove(int spotId, int objectId)
        {
            SpotId = spotId;
            ObjectId = objectId;
        }

        public int SpotId { get; }
        public int ObjectId { get; }
    }

    /// <summary>What to do with one drifting prefab before any missing spot is restored.</summary>
    public sealed class DriftSettlement
    {
        public DriftSettlement(IReadOnlyList<DriftMove> moves, IReadOnlyList<int> newObjects, bool blockMissingRestore)
        {
            Moves = moves ?? throw new ArgumentNullException(nameof(moves));
            NewObjects = newObjects ?? throw new ArgumentNullException(nameof(newObjects));
            BlockMissingRestore = blockMissingRestore;
        }

        public IReadOnlyList<DriftMove> Moves { get; }

        /// <summary>Whole spires that match no spot and are not a spot that drifted. The caller remembers them.</summary>
        public IReadOnlyList<int> NewObjects { get; }

        /// <summary>A whole spire still exists and a spot is uncovered. Do not grow that spot.</summary>
        public bool BlockMissingRestore { get; }
    }

    /// <summary>
    /// Pairs drifting spires with remembered spots inside <see cref="SpotIndex.DriftRadius"/>. One uncovered spot and one
    /// spire outside that radius is the same spire: the spot moves. A missing spot is not restored while any whole spire
    /// of this prefab still exists. A whole spire that sits on its own spot does not become a second spot.
    /// </summary>
    public static class DriftRules
    {
        public static DriftSettlement Settle(IReadOnlyList<DriftSpot> spots, IReadOnlyList<DriftObject> objects, float radius = SpotIndex.DriftRadius)
        {
            if (spots == null) throw new ArgumentNullException(nameof(spots));
            if (objects == null) throw new ArgumentNullException(nameof(objects));
            if (!(radius > 0f) || float.IsInfinity(radius)) throw new ArgumentException("Expected a positive drift radius.", nameof(radius));

            var pairs = new List<(int Spot, int Object, float Distance)>();
            for (int s = 0; s < spots.Count; s++)
            {
                for (int o = 0; o < objects.Count; o++)
                {
                    float distance = Vec3.Flat(spots[s].Position, objects[o].Position);
                    if (distance < radius) pairs.Add((s, o, distance));
                }
            }
            pairs.Sort((a, b) =>
            {
                int byDistance = a.Distance.CompareTo(b.Distance);
                if (byDistance != 0) return byDistance;
                int bySpot = a.Spot.CompareTo(b.Spot);
                return bySpot != 0 ? bySpot : a.Object.CompareTo(b.Object);
            });
            var spotUsed = new bool[spots.Count];
            var objectUsed = new bool[objects.Count];
            foreach (var pair in pairs)
            {
                if (spotUsed[pair.Spot] || objectUsed[pair.Object]) continue;
                spotUsed[pair.Spot] = true;
                objectUsed[pair.Object] = true;
            }

            var uncovered = new List<int>();
            for (int s = 0; s < spots.Count; s++)
                if (!spotUsed[s]) uncovered.Add(s);
            var unmatched = new List<int>();
            bool anyWhole = false;
            for (int o = 0; o < objects.Count; o++)
            {
                if (objects[o].Whole) anyWhole = true;
                if (!objectUsed[o]) unmatched.Add(o);
            }

            var moves = new List<DriftMove>();
            var born = new List<int>();
            if (uncovered.Count == 1 && unmatched.Count == 1)
                moves.Add(new DriftMove(spots[uncovered[0]].Id, objects[unmatched[0]].Id));
            else if (uncovered.Count == 0)
            {
                foreach (int index in unmatched)
                    if (objects[index].Whole) born.Add(objects[index].Id);
            }

            bool block = anyWhole && uncovered.Count - moves.Count > 0;
            return new DriftSettlement(moves, born, block);
        }
    }
}
