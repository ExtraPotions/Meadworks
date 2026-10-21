// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Experience/XpScope.cs.
// ValheimMods - Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// See THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
// Changed by expDARE, 2026: Larder namespace/keys, effective module gates and shared Skills/UI seams;
// native reference interaction, item, food, experience-scope and presentation algorithms retained.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// The kitchen interaction whose Cooking experience is being raised right now. The game raises the skill from
    /// inside CookingStation.OnInteract and InventoryGui.DoCrafting, both on the cook's own client where skills live;
    /// a prefix on each opens the scope with the dish it is about, a finalizer closes it, and <see cref="XpScaling"/>
    /// scales Cooking raised while it is open. Only the patch that opened the scope closes it.
    /// </summary>
    public static class XpScope
    {
        private static string discoverable;

        public static bool Active { get; private set; }

        /// <summary>The dish whose food value sets the tier; null counts as tier 1.</summary>
        public static ItemDrop.ItemData Dish { get; private set; }

        /// <summary>How many dishes the raise is for: 1 at a station, the multi-craft amount at a crafting station.</summary>
        public static int Units { get; private set; } = 1;

        /// <summary>
        /// Opens the scope. <paramref name="discoverablePrefab"/> is the dish's prefab name when this interaction makes
        /// it (and so may earn the discovery bonus), null otherwise. Returns false when a scope is already open.
        /// </summary>
        public static bool Begin(ItemDrop.ItemData dish, string discoverablePrefab, int units)
        {
            if (Active)
                return false;
            Active = true;
            Dish = dish;
            Units = Mathf.Max(1, units);
            discoverable = discoverablePrefab;
            return true;
        }

        /// <summary>Closes the scope when <paramref name="opened"/> says the caller's Begin opened it.</summary>
        public static void End(bool opened)
        {
            if (!opened)
                return;
            Active = false;
            Dish = null;
            Units = 1;
            discoverable = null;
        }

        /// <summary>The dish that may earn the discovery bonus, handed out once per scope; null after the first call.</summary>
        public static string TakeDiscoverable()
        {
            string prefab = discoverable;
            discoverable = null;
            return prefab;
        }

        /// <summary>Hides an open scope for experience that is already scaled. Pass the result to <see cref="Resume"/>.</summary>
        public static bool Pause()
        {
            bool wasActive = Active;
            Active = false;
            return wasActive;
        }

        public static void Resume(bool wasActive)
        {
            if (wasActive)
                Active = true;
        }
    }
}
