// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from OpenKeep 3.0.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, OpenKeep/OpenKeep/src/Recipes/RecipeFavourites.cs
// and OpenKeep/OpenKeep/src/Core/CharacterData.cs (set and flag formats).
// OpenKeep - a Valheim mod. Copyright (C) 2026 Gerald Glasgow. This program is free software: you can redistribute
// it and/or modify it under the terms of the GNU General Public License as published by the Free Software
// Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Kindle; keys prefixed Kindle. (OpenKeep keys kept for the converter); the
// favourites are a Unity-free model over the stored strings, so reading and writing the character's custom data,
// centre messages, the clear popup and the panel rebuild stay in the plugin; the set keeps its written order.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Kindle.Recipes
{
    /// <summary>Outcome of the favourites only button.</summary>
    public enum OnlyToggle
    {
        On,
        Off,
        /// <summary>Turning it on was refused: there is no favourite to show (the plugin shows a hint).</summary>
        RefusedNoFavourites,
    }

    /// <summary>
    /// The character's favourite recipes, saved with it in its custom data: <c>Kindle.favoriteRecipes</c> (recipe keys,
    /// comma separated) and the favourites only view, <c>Kindle.favoriteRecipesOnly</c> (<c>1</c> when on). Removing the
    /// last favourite turns favourites only off. Every change here is written back by the plugin, which then rebuilds the
    /// open crafting panel so stars, order and the favourites only list follow at once.
    /// </summary>
    public sealed class FavouriteRecipes
    {
        public const string SetKey = KindleCore.CharacterDataPrefix + "favoriteRecipes";
        public const string OnlyKey = KindleCore.CharacterDataPrefix + "favoriteRecipesOnly";
        public const string OpenKeepSetKey = "OpenKeep.favouriteRecipes";
        public const string OpenKeepOnlyKey = "OpenKeep.favouriteRecipesOnly";

        private readonly List<string> order = new List<string>();
        private readonly HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

        private FavouriteRecipes() { }

        /// <summary>From the stored values (either may be null when the key is absent).</summary>
        public static FavouriteRecipes Parse(string? set, string? onlyFlag)
        {
            FavouriteRecipes result = new FavouriteRecipes();
            if (!string.IsNullOrEmpty(set))
            {
                foreach (string part in set!.Split(','))
                    result.Add(Clean(part));
            }
            result.OnlyFlag = onlyFlag == "1" || string.Equals(onlyFlag, "true", StringComparison.OrdinalIgnoreCase);
            return result;
        }

        public IReadOnlyList<string> Keys => order;

        public int Count => order.Count;

        /// <summary>The stored favourites only flag (see <see cref="OnlyFavourites"/> for the effective value).</summary>
        public bool OnlyFlag { get; private set; }

        public bool Contains(string? key) => key != null && keys.Contains(key);

        /// <summary>The recipe shows as a favourite: the Favourites setting is on and the key is in the set.</summary>
        public bool Is(string? key, bool enabled) => enabled && Contains(key);

        /// <summary>The list shows favourites only: the Favourites setting is on and the flag is set.</summary>
        public bool OnlyFavourites(bool enabled) => enabled && OnlyFlag;

        /// <summary>Makes the recipe a favourite or not; true when it is one now. A blank key changes nothing.</summary>
        public bool Toggle(string? key)
        {
            string clean = Clean(key);
            if (clean.Length == 0)
                return false;
            if (keys.Remove(clean))
            {
                order.Remove(clean);
                if (order.Count == 0)
                    OnlyFlag = false;
                return false;
            }
            Add(clean);
            return true;
        }

        /// <summary>Favourites only on or off; turning it on is refused while there is no favourite to show.</summary>
        public OnlyToggle ToggleOnly()
        {
            if (OnlyFlag)
            {
                OnlyFlag = false;
                return OnlyToggle.Off;
            }
            if (order.Count == 0)
                return OnlyToggle.RefusedNoFavourites;
            OnlyFlag = true;
            return OnlyToggle.On;
        }

        /// <summary>Clears every favourite and favourites only; returns how many favourites there were.</summary>
        public int Clear()
        {
            int count = order.Count;
            order.Clear();
            keys.Clear();
            OnlyFlag = false;
            return count;
        }

        /// <summary>The value for <see cref="SetKey"/>, or null to remove the key.</summary>
        public string? SerializeSet() => order.Count > 0 ? string.Join(",", order) : null;

        /// <summary>The value for <see cref="OnlyKey"/>, or null to remove the key.</summary>
        public string? SerializeOnly() => OnlyFlag ? "1" : null;

        private void Add(string key)
        {
            if (key.Length > 0 && keys.Add(key))
                order.Add(key);
        }

        private static string Clean(string? key) => (key ?? "").Replace(",", "").Replace("|", "").Trim();
    }
}
