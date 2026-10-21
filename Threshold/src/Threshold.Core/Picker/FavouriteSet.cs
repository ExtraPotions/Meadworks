// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: personal favorites (approved PotalMap idea: personal, client-local, survive restarts). Storage
// follows Wayfare's PlayerFavourites rule (GPL-3.0-or-later): a comma set of position keys in the character's custom
// data, so favorites travel with the character; the key helper is the adapted PortalKey.
using System;
using System.Collections.Generic;
using ExtraPotions.Threshold.Portals;

namespace ExtraPotions.Threshold.Picker
{
    public sealed class FavouriteSet
    {
        /// <summary><c>Player.m_customData</c> key holding the set.</summary>
        public const string CharacterDataKey = ThresholdCore.CharacterDataPrefix + "favorites";

        private readonly List<string> order = new List<string>();
        private readonly HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

        public FavouriteSet(IEnumerable<string> keys)
        {
            foreach (var key in keys ?? Array.Empty<string>())
                if (PortalKey.IsKey(key) && this.keys.Add(key)) order.Add(key);
        }

        public static FavouriteSet Parse(string? raw) => new FavouriteSet(PortalKey.ParseSet(raw));

        public int Count => order.Count;

        /// <summary>Changes with every toggle, so a list built from the set knows when to refill.</summary>
        public int Version { get; private set; }

        public bool Contains(string key) => key != null && keys.Contains(key);

        /// <summary>Adds or removes the portal by position key; returns whether it is a favorite afterwards.</summary>
        public bool Toggle(string key)
        {
            if (!PortalKey.IsKey(key)) throw new ArgumentException("Expected a portal position key.", nameof(key));
            Version++;
            if (keys.Remove(key))
            {
                order.Remove(key);
                return false;
            }
            keys.Add(key);
            order.Add(key);
            return true;
        }

        public string Format() => PortalKey.FormatSet(order);
    }
}
