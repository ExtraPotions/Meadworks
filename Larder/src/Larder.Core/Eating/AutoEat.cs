// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/AutoEat.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the UpdateFood prefix and postfix became pure rules (switches, the
// expiring food, the item to eat); the item search gained Mantle's Food slots first (design 8.1).
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;

namespace ExtraPotions.Larder.Eating
{
    /// <summary>An inventory item that could be eaten automatically.</summary>
    public readonly struct AutoEatCandidate
    {
        public AutoEatCandidate(string sharedName, bool inFoodSlot, int order)
        {
            SharedName = sharedName;
            InFoodSlot = inFoodSlot;
            Order = order;
        }

        /// <summary>The item's shared name (<c>m_shared.m_name</c>), which identifies "the same food".</summary>
        public string SharedName { get; }

        /// <summary>The item sits in one of Mantle's Food slots (published grid range); false without Mantle.</summary>
        public bool InFoodSlot { get; }

        /// <summary>The item's place in the game's inventory order.</summary>
        public int Order { get; }
    }

    /// <summary>
    /// Auto eat (design B5): when a food runs out, another of the same food is eaten from the inventory with sound and
    /// animation. Needs the server's Allow Auto Eat and the player's own Auto Eat, local player only.
    /// </summary>
    public static class AutoEat
    {
        /// <summary>
        /// The food is used as from the inventory (the game's inventory path), never on the station the player is
        /// looking at.
        /// </summary>
        public const bool AsFromInventory = true;

        /// <summary>The time left at or below which a food can run out on the game's next one-second food tick.</summary>
        public const float ExpiringAt = 1f;

        public static bool Active(bool allowAutoEat, bool ownAutoEat, bool isLocalPlayer) => allowAutoEat && ownAutoEat && isLocalPlayer;

        /// <summary>The first food at or below one second left, or -1.</summary>
        public static int Expiring(IReadOnlyList<float> timesLeft)
        {
            for (int i = 0; i < timesLeft.Count; i++)
            {
                if (timesLeft[i] <= ExpiringAt)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// The item to eat for an expired food: the same shared name, Mantle's Food slots first, then the rest of the
        /// inventory, each in inventory order; -1 when none is left.
        /// </summary>
        public static int Select(string sharedName, IReadOnlyList<AutoEatCandidate> items)
        {
            if (string.IsNullOrEmpty(sharedName))
                return -1;
            int best = -1;
            for (int i = 0; i < items.Count; i++)
            {
                AutoEatCandidate item = items[i];
                if (item.SharedName != sharedName)
                    continue;
                if (best < 0 || Before(item, items[best]))
                    best = i;
            }
            return best;
        }

        private static bool Before(AutoEatCandidate a, AutoEatCandidate b)
        {
            if (a.InFoodSlot != b.InFoodSlot)
                return a.InFoodSlot;
            return a.Order < b.Order;
        }
    }
}
