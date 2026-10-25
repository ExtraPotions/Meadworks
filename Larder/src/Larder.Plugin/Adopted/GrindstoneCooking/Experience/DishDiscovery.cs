// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Experience/DishDiscovery.cs.
// ValheimMods - Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// See THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
// Changed by expDARE, 2026: Larder namespace/keys, effective module gates and shared Skills/UI seams;
// native reference interaction, item, food, experience-scope and presentation algorithms retained.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// The dishes a character has made, for the discovery bonus: prefab names, comma-separated, in the player's own
    /// Player.m_customData under <see cref="Keys.MadeDishes"/>. Player.Save writes m_customData into the character
    /// file and Player.Load reads it back, so the list survives relogs and follows the character between worlds.
    /// </summary>
    public static class DishDiscovery
    {
        private const char Separator = ',';
        private sealed class DiscoveryCache
        {
            internal string Raw;
            internal readonly HashSet<string> Dishes = new HashSet<string>(StringComparer.Ordinal);
        }
        private static readonly ConditionalWeakTable<Player, DiscoveryCache> caches = new ConditionalWeakTable<Player, DiscoveryCache>();

        private static DiscoveryCache CacheFor(Player player, string made)
        {
            DiscoveryCache cache = caches.GetValue(player, _ => new DiscoveryCache());
            string raw = made ?? "";
            if (cache.Raw != raw)
            {
                cache.Raw = raw;
                cache.Dishes.Clear();
                foreach (string dish in raw.Split(Separator))
                    if (!string.IsNullOrEmpty(dish)) cache.Dishes.Add(dish);
            }
            return cache;
        }

        /// <summary>Whether the character has made this dish before.</summary>
        public static bool HasMade(Player player, string dishPrefab)
        {
            if (player == null || string.IsNullOrEmpty(dishPrefab))
                return false;
            Read(player, out string made);
            return CacheFor(player, made).Dishes.Contains(dishPrefab);
        }

        /// <summary>Records the dish as made. True when this was the character's first time.</summary>
        public static bool TryRecord(Player player, string dishPrefab)
        {
            if (player == null || string.IsNullOrEmpty(dishPrefab) || dishPrefab.IndexOf(Separator) >= 0 || HasMade(player, dishPrefab))
                return false;
            Read(player, out string made);
            DiscoveryCache cache = CacheFor(player, made);
            var updated = string.IsNullOrEmpty(made) ? dishPrefab : made + Separator + dishPrefab;
            player.m_customData[Keys.MadeDishes] = updated;
            cache.Raw = updated;
            cache.Dishes.Add(dishPrefab);
            return true;
        }
        private static bool Read(Player player, out string made) =>
            player.m_customData.TryGetValue(Keys.MadeDishes, out made)
            || player.m_customData.TryGetValue("grindstone_made", out made);
    }
}
