// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Companions/Feeders.cs (Nearest)
// and OpenKeep/OpenKeep/src/Homestead/PetEating.cs (Choose, PathTries).
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Unity-free candidate list; troughs ranked before containers; one rule for both kinds.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Shepherd.Feeding
{
    /// <summary>One loaded source within range that holds a food the animal eats.</summary>
    public sealed class FeedCandidate
    {
        public FeedCandidate(string id, FeederKind kind, double distanceSquared, string food)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Kind = kind;
            DistanceSquared = distanceSquared;
            Food = food ?? throw new ArgumentNullException(nameof(food));
        }

        public string Id { get; }
        public FeederKind Kind { get; }
        public double DistanceSquared { get; }
        /// <summary>Prefab name of the first food the animal eats that this source holds.</summary>
        public string Food { get; }
    }

    /// <summary>
    /// The source a hungry animal walks to (design A1, A4): troughs first, nearest first, then containers, nearest first.
    /// Path checks are costly, so at most <see cref="PathTries"/> candidates are tested (OpenKeep); an animal that finds
    /// none searches again at the game's next consume search.
    /// </summary>
    public static class FeedSearch
    {
        public const int PathTries = 3;

        public static FeedCandidate? Choose(IEnumerable<FeedCandidate> candidates, Func<FeedCandidate, bool> havePath)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (havePath == null) throw new ArgumentNullException(nameof(havePath));
            int tries = 0;
            foreach (var candidate in candidates.OrderBy(c => c.Kind == FeederKind.Trough ? 0 : 1).ThenBy(c => c.DistanceSquared))
            {
                if (havePath(candidate)) return candidate;
                if (++tries >= PathTries) return null;
            }
            return null;
        }
    }
}
