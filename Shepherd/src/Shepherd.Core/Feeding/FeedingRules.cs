// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Shepherd.Feeding
{
    /// <summary>Where a meal comes from: a Shepherd trough, or (off by default, design Q2) a player container.</summary>
    public enum FeederKind { Trough, Container }

    /// <summary>Design 4.2 <c>Container Selection</c>: every eligible container except denied ones, or only marked ones.</summary>
    public enum ContainerSelectionMode { AllExceptDenied, MarkedOnly }

    /// <summary>What the animal owner knows about one animal at its food search moment.</summary>
    public readonly struct AnimalFacts
    {
        public AnimalFacts(bool tamed, bool beingTamed, bool tameable, bool hungry, bool dead, bool disallowed)
        {
            Tamed = tamed; BeingTamed = beingTamed; Tameable = tameable; Hungry = hungry; Dead = dead; Disallowed = disallowed;
        }

        public bool Tamed { get; }
        /// <summary>Untamed, but its taming has started (it ate once): GrindstoneSkills <c>Herd.IsBeingTamed</c>.</summary>
        public bool BeingTamed { get; }
        public bool Tameable { get; }
        public bool Hungry { get; }
        public bool Dead { get; }
        /// <summary>The creature prefab is on <c>Disallow Animals</c>.</summary>
        public bool Disallowed { get; }
    }

    /// <summary>Comma lists of prefab names (<c>Disallow Feed</c>, <c>Disallow Animals</c>), AutoFeedRedux semantics.</summary>
    public static class NameList
    {
        /// <summary>Trimmed, case-insensitive, empty entries skipped; null or blank text is an empty list.</summary>
        public static IReadOnlyCollection<string> Parse(string? text)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in (text ?? "").Split(','))
            {
                var name = part.Trim();
                if (name.Length > 0) names.Add(name);
            }
            return names;
        }
    }

    /// <summary>Design 1.1 A1, A2, A7, A9: which animals look for trough food, and which food they take.</summary>
    public static class FeedingRules
    {
        /// <summary>
        /// A hungry, living, tameable animal that is not disallowed eats when it is tamed or its taming has started (A1);
        /// a wild one that has not started taming eats only with <c>Feed Wild Animals</c> (A7).
        /// </summary>
        public static bool Eligible(AnimalFacts animal, bool feedWild)
        {
            if (!animal.Tameable || !animal.Hungry || animal.Dead || animal.Disallowed) return false;
            return animal.Tamed || animal.BeingTamed || feedWild;
        }

        public static bool EligibleYoung(AnimalFacts animal, bool feedWild, bool growing, bool nativeEater) =>
            growing && nativeEater && !animal.Tameable && animal.Hungry && !animal.Dead && !animal.Disallowed && (animal.Tamed || feedWild);

        /// <summary>
        /// The first of the creature's foods (its own <c>m_consumeItems</c> order, prefab names) that the source holds and
        /// that is not on <c>Disallow Feed</c>; null when none.
        /// </summary>
        public static string? FirstFood(IEnumerable<string> consumeItems, Func<string, bool> holds, IReadOnlyCollection<string> disallowed)
        {
            if (consumeItems == null) throw new ArgumentNullException(nameof(consumeItems));
            if (holds == null) throw new ArgumentNullException(nameof(holds));
            foreach (var item in consumeItems)
            {
                if (string.IsNullOrEmpty(item) || IsListed(disallowed, item)) continue;
                if (holds(item)) return item;
            }
            return null;
        }

        /// <summary>The name is on a list made by <see cref="NameList.Parse"/> (case-insensitive).</summary>
        public static bool IsListed(IReadOnlyCollection<string>? names, string? name)
        {
            if (names == null || names.Count == 0 || string.IsNullOrEmpty(name)) return false;
            if (names is ICollection<string> set) return set.Contains(name!);
            foreach (var entry in names) if (string.Equals(entry, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    /// <summary>What the animal owner knows about one loaded container (design 4.2, 8.1).</summary>
    public readonly struct ContainerFacts
    {
        public ContainerFacts(bool isTrough, bool isPrivate, bool playerBuilt, bool? burrowFeedSource, bool shepherdMarked, bool shepherdDenied)
        {
            IsTrough = isTrough; IsPrivate = isPrivate; PlayerBuilt = playerBuilt; BurrowFeedSource = burrowFeedSource;
            ShepherdMarked = shepherdMarked; ShepherdDenied = shepherdDenied;
        }

        public bool IsTrough { get; }
        public bool IsPrivate { get; }
        public bool PlayerBuilt { get; }
        /// <summary>Burrow's <c>meadworks.containers.feed_source</c> answer; null when Burrow does not provide it.</summary>
        public bool? BurrowFeedSource { get; }
        /// <summary>Shepherd's own ZDO mark <see cref="ContainerEligibility.MarkKey"/> (used without Burrow).</summary>
        public bool ShepherdMarked { get; }
        /// <summary>Shepherd's own ZDO deny mark <see cref="ContainerEligibility.DenyKey"/>.</summary>
        public bool ShepherdDenied { get; }
    }

    /// <summary>
    /// Design 8.1: a container feeds only when container feeding is on (checked by the caller), it is a player-built,
    /// non-private, non-trough container, and the selection allows it. With <see cref="ContainerSelectionMode.MarkedOnly"/>
    /// Burrow's "Feed source" tag decides when Burrow provides it; otherwise Shepherd's own mark.
    /// </summary>
    public static class ContainerEligibility
    {
        public const string MarkKey = "shepherd.feed";
        public const string DenyKey = "shepherd.feed_deny";

        public static bool IsFeedSource(ContainerFacts facts, ContainerSelectionMode selection)
        {
            if (facts.IsTrough || facts.IsPrivate || !facts.PlayerBuilt) return false;
            if (selection == ContainerSelectionMode.MarkedOnly)
                return facts.BurrowFeedSource ?? facts.ShepherdMarked;
            return !facts.ShepherdDenied;
        }
    }

    /// <summary>A solid part of the segment from an animal to its food source, in meters.</summary>
    public readonly struct FeedSolidSpan
    {
        public FeedSolidSpan(double start, double end) { Start = start; End = end; }
        public double Start { get; }
        public double End { get; }
    }

    /// <summary>Distances and timings of the walk to a food source (design 4.2, A4 to A6).</summary>
    public static class FeedMath
    {
        /// <summary>Maximum difference between animal and source footing while eating in approach mode.</summary>
        public const double EatRiseMetres = 1.0;

        public static double SolidThickness(IEnumerable<FeedSolidSpan> solids)
        {
            var spans = new List<FeedSolidSpan>();
            foreach (var span in solids)
            {
                if (double.IsNaN(span.Start) || double.IsNaN(span.End) || double.IsInfinity(span.Start)
                    || double.IsInfinity(span.End) || span.Start < 0 || span.End < span.Start)
                    return double.PositiveInfinity;
                if (span.End > span.Start) spans.Add(span);
            }
            spans.Sort((a, b) => a.Start.CompareTo(b.Start));
            double total = 0, end = 0;
            foreach (var span in spans)
            {
                if (span.End <= end) continue;
                total += span.End - Math.Max(end, span.Start);
                end = span.End;
            }
            return total;
        }

        public static bool WallWithinReach(double thickness, double configuredMetres)
        {
            double limit = double.IsNaN(configuredMetres) ? 0 : Math.Max(0, Math.Min(2, configuredMetres));
            return thickness == 0 || (thickness > 0 && limit > 0 && thickness <= limit + 0.0001);
        }
        /// <summary>Meters from a source's center to where an animal of this radius stands to eat.</summary>
        public static double StandOff(double sourceRadius, double animalRadius) => sourceRadius + animalRadius;

        /// <summary>An animal this far (XZ) from the source center is within <c>Move Proximity</c> of its side.</summary>
        public static bool InReach(double distance, double sourceRadius, double animalRadius, double proximity) =>
            InReach(distance, sourceRadius, animalRadius, proximity, 0);

        public static bool InReach(double distance, double sourceRadius, double animalRadius, double proximity, double rise) =>
            System.Math.Abs(rise) <= EatRiseMetres && distance <= StandOff(sourceRadius, animalRadius) + proximity;

        /// <summary>
        /// With <c>Require Move To Feed</c> the animal eats only within reach; without it, anywhere within the search range
        /// (AutoFeedRedux).
        /// </summary>
        public static bool MayEatFrom(double distance, double sourceRadius, double animalRadius, double proximity, bool requireMove, double range) =>
            MayEatFrom(distance, sourceRadius, animalRadius, proximity, requireMove, range, 0);

        public static bool MayEatFrom(double distance, double sourceRadius, double animalRadius, double proximity, bool requireMove, double range, double rise) =>
            requireMove ? InReach(distance, sourceRadius, animalRadius, proximity, rise) : distance <= range;

        /// <summary>The walk started at <paramref name="since"/> lasted longer than <c>Give Up Seconds</c>.</summary>
        public static bool GaveUp(double since, double now, double giveUpSeconds) => now - since > giveUpSeconds;

        /// <summary>Sides of a source an animal may stand on to eat, evenly around it.</summary>
        public const int RingBearings = 8;

        /// <summary>Seconds between checks that a walking animal still has a full path to its eat spot.</summary>
        public const double SpotRecheckSeconds = 2.0;

        /// <summary>
        /// Meters (XZ) from an animal to its own-side eat spot within which it walks straight there when no side of the
        /// source has a full path: an animal in a navmesh pocket beside a tile seam gets only partial paths, and the game's
        /// path walk stands still on them.
        /// </summary>
        public const double DirectWalkMetres = 6.0;

        /// <summary>Meters of height between the animal and its eat spot beyond which it never walks straight (a ledge or raised floor).</summary>
        public const double DirectWalkRise = 1.0;

        /// <summary>The animal is close enough to its eat spot, and level enough with it, to walk straight there without a full path.</summary>
        public static bool MayWalkDirect(double toSpot, double rise) => toSpot >= 0 && toSpot <= DirectWalkMetres && System.Math.Abs(rise) <= DirectWalkRise;

        /// <summary>
        /// The ring sides to try, nearest first: the animal's own side (0), then one step either way, and so on to the far
        /// side. A side whose path stops short (a navmesh tile seam beside the source) gives way to the next.
        /// </summary>
        public static IEnumerable<int> RingOrder(int bearings)
        {
            if (bearings <= 0) yield break;
            yield return 0;
            for (int step = 1; step <= bearings / 2; step++)
            {
                yield return step;
                if (bearings - step != step) yield return bearings - step;
            }
        }
    }
}
