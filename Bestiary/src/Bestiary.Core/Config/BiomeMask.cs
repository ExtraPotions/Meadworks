// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Bestiary.Config
{
    /// <summary>
    /// The game's <c>Heightmap.Biome</c> flags with the same values (read from <c>assembly_valheim.dll</c> 1.0.17), so the
    /// plugin casts between them and ECP's config text ("BlackForest, Meadows") imports unchanged.
    /// </summary>
    [Flags]
    public enum BiomeMask
    {
        None = 0,
        Meadows = 1,
        Swamp = 2,
        Mountain = 4,
        BlackForest = 8,
        Plains = 16,
        AshLands = 32,
        DeepNorth = 64,
        Ocean = 256,
        Mistlands = 512,
        Land = 639,
        All = 895,
    }

    public static class BiomeMasks
    {
        private static readonly BiomeMask[] Singles =
        {
            BiomeMask.Meadows, BiomeMask.Swamp, BiomeMask.Mountain, BiomeMask.BlackForest, BiomeMask.Plains,
            BiomeMask.AshLands, BiomeMask.DeepNorth, BiomeMask.Ocean, BiomeMask.Mistlands,
        };

        /// <summary>Comma list of names (case-insensitive, trimmed); null when a name is unknown. Empty text is None.</summary>
        public static BiomeMask? Parse(string? text)
        {
            var result = BiomeMask.None;
            foreach (var raw in (text ?? "").Split(','))
            {
                var name = raw.Trim();
                if (name.Length == 0) continue;
                if (!Enum.TryParse(name, true, out BiomeMask value) || !Enum.IsDefined(typeof(BiomeMask), value)) return null;
                result |= value;
            }
            return result;
        }

        /// <summary>A named value as its name; otherwise its single biomes in ascending value order.</summary>
        public static string Format(BiomeMask mask)
        {
            if (Enum.IsDefined(typeof(BiomeMask), mask)) return mask.ToString();
            return string.Join(", ", Singles.Where(b => (mask & b) == b).Select(b => b.ToString()));
        }

        public static bool Contains(BiomeMask mask, BiomeMask biome) => biome != BiomeMask.None && (mask & biome) == biome;

        public static IReadOnlyList<BiomeMask> Each(BiomeMask mask) => Singles.Where(b => (mask & b) == b).ToArray();
    }
}
