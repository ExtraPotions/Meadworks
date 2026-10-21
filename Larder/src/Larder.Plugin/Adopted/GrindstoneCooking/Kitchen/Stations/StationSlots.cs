// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Kitchen/Stations/StationSlots.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// GrindstoneSkills' key for one slot of a cooking station or oven, beside the game's own ("slot" + i holds the item
    /// name and the cooked time, "slotstatus" + i the status): the Cooking level of the cook who put the food on. The
    /// game keeps a slot only in the station's ZDO and changes it on the ZDO owner, so the key lives in the same ZDO, is
    /// written where the game writes its slots, and reaches every peer with it. A slot filled by a plain vanilla add
    /// reads as level 0. Every way the game empties a slot goes through CookingStation.SetSlot with an empty name
    /// (taking a dish off, a missing recipe, the station breaking); the postfix there resets the key. It is reset by
    /// writing the default: ZDO.Remove* does not raise the data revision, so a removal would never reach other peers.
    /// Each key's hash ("grindstone_level" + slot, as ZDO.Get/Set with a name would hash it) is worked out once per slot
    /// rather than built and hashed on every read.
    /// </summary>
    public static class StationSlots
    {
        /// <summary>Slots whose key hashes are worked out ahead; a station with more hashes the rest on each use.</summary>
        private const int HashedSlots = 16;

        private static readonly int[] LevelKeys = Hashes(Keys.SlotLevel);

        /// <summary>The cook's Cooking level when the food went on, 0 when unknown.</summary>
        public static float Level(ZDO zdo, int slot) => Keys.ReadFloat(zdo, Keys.SlotLevel + slot);

        /// <summary>Stores the cook's level for a freshly filled slot.</summary>
        public static void Write(ZDO zdo, int slot, float level) => zdo.Set(Key(LevelKeys, Keys.SlotLevel, slot), level);

        public static void Reset(ZDO zdo, int slot) => Write(zdo, slot, 0f);

        private static int[] Hashes(string key)
        {
            int[] hashes = new int[HashedSlots];
            for (int slot = 0; slot < HashedSlots; slot++)
                hashes[slot] = (key + slot).GetStableHashCode();
            return hashes;
        }

        /// <summary>The hash of <paramref name="key"/> followed by the slot number, from <paramref name="hashes"/> when it has it.</summary>
        private static int Key(int[] hashes, string key, int slot) =>
            slot >= 0 && slot < hashes.Length ? hashes[slot] : (key + slot).GetStableHashCode();

        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.SetSlot))]
        private static class ClearSlot
        {
            [HarmonyPostfix]
            private static void Postfix(CookingStation __instance, int slot, string itemName)
            {
                ZNetView nview = __instance.m_nview;
                if (string.IsNullOrEmpty(itemName) && Kitchen.IsKitchen(__instance) && nview != null && nview.IsValid())
                    Reset(nview.GetZDO(), slot);
            }
        }
    }
}
