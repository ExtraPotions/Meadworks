// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>
    /// The dice breeding decisions use, so Shepherd.Core stays Unity-free and tests can script every roll. The plugin
    /// passes an adapter over <c>UnityEngine.Random</c>.
    /// </summary>
    public interface IBreedingRandom
    {
        /// <summary>A value in [0, 1).</summary>
        double Value();

        /// <summary>An integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
        int Range(int minInclusive, int maxExclusive);
    }

    /// <summary>Deterministic dice over <see cref="System.Random"/> (tests, fixed-seed checks).</summary>
    public sealed class SeededRandom : IBreedingRandom
    {
        private readonly Random random;

        public SeededRandom(int seed) { random = new Random(seed); }

        public double Value() => random.NextDouble();

        public int Range(int minInclusive, int maxExclusive) => random.Next(minInclusive, maxExclusive);
    }
}
