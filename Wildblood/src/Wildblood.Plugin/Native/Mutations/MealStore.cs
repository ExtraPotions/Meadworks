// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// What a Devouring creature has eaten: the prefab hash of each creature it devoured, oldest first, packed four bytes
    /// each (little-endian, the same on every platform) into one byte array on its own ZDO under
    /// <see cref="TraitKeys.DevouredMeals"/>. The array's length is the meal count, so the hot checks (targeting, the
    /// bite) read it without unpacking anything. A ZDO is replicated, so every machine reads the same list - the limit is
    /// checked against it wherever a bite lands, and every client draws the eaten creatures on the nameplate from it -
    /// and it survives a reload and a hand-over. Only the devourer's owner writes it, as it banks a meal
    /// (<see cref="Runtime.CreatureRpc"/>).
    /// </summary>
    public static class MealStore
    {
        private static readonly int DevouredMealsHash = TraitKeys.DevouredMeals.GetStableHashCode();

        private const int Width = 4;

        /// <summary>How many creatures it has devoured; 0 for a creature that never has, or one with no ZDO.</summary>
        public static int Count(ZDO? zdo) => Bytes(zdo).Length / Width;

        /// <summary>The prefab hash of every creature it has devoured, oldest first.</summary>
        public static List<int> Load(ZDO? zdo)
        {
            byte[] bytes = Bytes(zdo);
            List<int> meals = new List<int>(bytes.Length / Width);
            for (int at = 0; at + Width <= bytes.Length; at += Width)
            {
                meals.Add(bytes[at] | bytes[at + 1] << 8 | bytes[at + 2] << 16 | bytes[at + 3] << 24);
            }
            return meals;
        }

        /// <summary>Owner-only: one more meal, at the end. A new array, never the stored one changed in place.</summary>
        public static void Add(ZDO zdo, int prefabHash)
        {
            byte[] old = Bytes(zdo);
            int kept = old.Length - old.Length % Width;
            byte[] grown = new byte[kept + Width];
            System.Buffer.BlockCopy(old, 0, grown, 0, kept);
            grown[kept] = (byte)prefabHash;
            grown[kept + 1] = (byte)(prefabHash >> 8);
            grown[kept + 2] = (byte)(prefabHash >> 16);
            grown[kept + 3] = (byte)(prefabHash >> 24);
            zdo.Set(TraitKeys.DevouredMeals, grown);
        }

        private static byte[] Bytes(ZDO? zdo) =>
            zdo != null && zdo.GetByteArray(DevouredMealsHash, out byte[] bytes) && bytes != null
                ? bytes : System.Array.Empty<byte>();
    }
}
