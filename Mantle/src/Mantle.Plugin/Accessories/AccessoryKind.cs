// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Mantle.Core;
using Mantle.Crafting;

namespace Mantle.Accessories
{
    /// <summary>
    /// One of Mantle's rings or belts as a crafted item (<see cref="CraftedKind"/>), made from a built-in registry entry. Its stats
    /// and recipe are the registry's now (the YAML over the built-in values, <see cref="AccessoriesFile"/>), so the recipe follows
    /// a file change; the item itself (its prefab, name token and description token) is built once when ZNetScene wakes
    /// (<see cref="AccessoryPrefab"/>). Never worn on the body: it only lies in its slot.
    /// </summary>
    public sealed class AccessoryKind : CraftedKind
    {
        public const string IdPrefix = "Mantle_";

        public AccessoryKind(AccessoryEntry defaults)
            : base(defaults.Id, defaults.Slot == AccessorySlot.Ring ? "ring" : "belt", defaults.Id.Substring(IdPrefix.Length).ToLowerInvariant(),
                defaults.Name, defaults.Look, defaults.Weight)
        {
            Defaults = defaults;
        }

        public AccessoryEntry Defaults { get; }

        /// <summary>The entry in use: the registry's, else the built-in one.</summary>
        public AccessoryEntry Entry => AccessoryRegistry.Current.Find(Id) ?? Defaults;

        public override CraftStats Recipe => Stats(Entry.Recipe ?? Defaults.Recipe);

        public override CraftStats DefaultRecipe => Stats(Defaults.Recipe);

        /// <summary>Whether the settings let it be crafted now: its entry is on, Mantle is on and the slot kind exists.</summary>
        public override bool Craftable => Entry.Enabled && InventorySettings.Enabled.Value && (Defaults.Slot == AccessorySlot.Ring
            ? InventorySettings.RingSlots.Value > 0 : InventorySettings.BeltSlot.Value);

        private static CraftStats Stats(AccessoryRecipe recipe) => new CraftStats(recipe.Station, recipe.Level, recipe.Cost);
    }

    /// <summary>The kinds of the built-in entries that Mantle makes.</summary>
    public static class AccessoryKinds
    {
        private static readonly List<AccessoryKind> all = Build();

        public static IReadOnlyList<AccessoryKind> All => all;

        private static List<AccessoryKind> Build()
        {
            List<AccessoryKind> kinds = new List<AccessoryKind>();
            foreach (AccessoryEntry entry in AccessoryCatalog.All)
            {
                if (entry.BuiltIn)
                    kinds.Add(new AccessoryKind(entry));
            }
            return kinds;
        }

        public static AccessoryKind ById(string id)
        {
            foreach (AccessoryKind kind in all)
            {
                if (string.Equals(kind.Id, id, StringComparison.OrdinalIgnoreCase))
                    return kind;
            }
            return null;
        }
    }
}
