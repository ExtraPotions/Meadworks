// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Shepherd.Breeding;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>Unity's random for the portable yield decisions.</summary>
    public sealed class YieldDice : IBreedingRandom
    {
        public static readonly YieldDice Instance = new YieldDice();
        private YieldDice() { }
        public double Value() => UnityEngine.Random.value;
        public int Range(int minInclusive, int maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive);
    }
}
