// Threshold - ExtraPotions Meadworks portals.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/Core/ListHash.cs.
// Changed by expDARE, 2026: namespace, public visibility, the Unity Vector3 overload replaced by three floats and
// MapPoint so the type stays in the Unity-free Threshold.Core. Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Threshold.Portals
{
    /// <summary>A list's contents folded into one 64-bit number, in order, so the server can tell whether the list a client
    /// already holds is still current without sending the list again. A change to any value, a new entry or a missing
    /// one gives another number (in practice: two lists colliding would take billions of years of changes). Never 0, which
    /// stands for "no list held".</summary>
    public struct ListHash
    {
        private ulong value;

        /// <summary>A new hash of a list with <paramref name="count"/> entries.</summary>
        public static ListHash Start(int count)
        {
            ListHash hash = new ListHash { value = 0x9E3779B97F4A7C15UL };
            hash.Add(count);
            return hash;
        }

        public long Value => value != 0UL ? (long)value : 1L;

        public void Add(long part) => value = Mix(value ^ (ulong)part);

        public void Add(int part) => Add((long)part);

        public void Add(uint part) => Add((long)part);

        /// <summary>Exactly: a float widens to a double without loss.</summary>
        public void Add(float part) => Add(BitConverter.DoubleToInt64Bits(part));

        public void Add(float x, float y, float z)
        {
            Add(x);
            Add(y);
            Add(z);
        }

        public void Add(MapPoint part) => Add(part.X, part.Y, part.Z);

        /// <summary>The finaliser of SplitMix64: every bit of the input moves about half the bits of the output.</summary>
        private static ulong Mix(ulong x)
        {
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }
}
