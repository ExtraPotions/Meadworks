// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExtraPotions.Shepherd.Breeding
{
    public enum BreedingVerb : byte { Help, Breed, Stars }
    public readonly struct BreedingCommandLine
    {
        public BreedingVerb Verb { get; }
        public int Level { get; }
        BreedingCommandLine(BreedingVerb verb, int level = 0) { Verb = verb; Level = level; }
        public static BreedingCommandLine Parse(IReadOnlyList<string> args, int maxLevel)
        {
            if (args.Count == 1 && args[0].Equals("breed", StringComparison.OrdinalIgnoreCase)) return new BreedingCommandLine(BreedingVerb.Breed);
            if (args.Count == 2 && args[0].Equals("stars", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out int stars) && stars < maxLevel)
                return new BreedingCommandLine(BreedingVerb.Stars, stars + 1);
            return new BreedingCommandLine(BreedingVerb.Help);
        }
    }
}
