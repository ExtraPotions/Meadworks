// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Cairn's side of the shared exploration contracts (design A8, Keel design 8.2). Cairn reads Keel's
// sea factor as meadworks.explore.radius 1.0 and provides its computed radius as meadworks.explore.current 1.0 (Crucible
// main cda9696). Cairn.Core has no Crucible reference, so the contract bounds are restated here; the plugin reads and
// registers through Crucible.Contracts.
using System;

namespace ExtraPotions.Cairn.Radius
{
    /// <summary>Where the sea factor comes from: Keel when it provides the contract, else GrindstoneSkills' Sailing API.</summary>
    public static class SeaFactorSource
    {
        /// <summary>The largest factor <c>meadworks.explore.radius</c> 1.0 accepts (Crucible ExploreRadiusContract.MaximumFactor).</summary>
        public const float MaximumFactor = 16f;

        /// <summary>
        /// The sea factor. <paramref name="keelProvides"/>: a <c>meadworks.explore.radius</c> provider is registered and
        /// <paramref name="keelFactor"/> is its reading (neutral 1 when it failed). Keel's factor counts when it is valid and
        /// above neutral. Otherwise (no Keel, Keel ashore, or Keel's Sailing stepped aside because GrindstoneSkills runs its
        /// own) GrindstoneSkills' factor counts while the local player is aboard; else 1.
        /// </summary>
        public static float Pick(bool keelProvides, float keelFactor, bool grindstoneAvailable, bool aboard, float grindstoneFactor)
        {
            if (keelProvides && Valid(keelFactor) && keelFactor > 1f)
                return keelFactor;
            if (!grindstoneAvailable || !aboard) return 1f;
            return float.IsNaN(grindstoneFactor) || float.IsInfinity(grindstoneFactor) || grindstoneFactor < 0f ? 1f : grindstoneFactor;
        }

        private static bool Valid(float factor) => !float.IsNaN(factor) && !float.IsInfinity(factor) && factor > 0f && factor <= MaximumFactor;
    }

    /// <summary>The value Cairn provides as <c>meadworks.explore.current</c> 1.0.</summary>
    public static class ExploreCurrentValue
    {
        /// <summary>The largest radius the contract accepts, in meters (Crucible ExploreCurrentContract.MaximumRadius).</summary>
        public const float MaximumRadius = 10000f;

        /// <summary>
        /// The radius Cairn last computed for the local player, clamped to the contract's bound; -1 (invalid, so readers
        /// fall back to their own value) before any computation or for a broken value.
        /// </summary>
        public static float Of(bool computed, float radius)
        {
            if (!computed || float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f) return -1f;
            return Math.Min(MaximumRadius, radius);
        }
    }
}
