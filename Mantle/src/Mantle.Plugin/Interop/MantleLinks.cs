// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// User decision 2026-10-12 22:41: other MeadWorks products read what Mantle publishes through these Crucible capabilities
// (Crucible.Contracts ProductLinks), never through Mantle's config file, its custom data keys or its object names.
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using Mantle.Core;
using Mantle.Ring;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Mantle.Interop
{
    /// <summary>
    /// Mantle's side of the product links: who lays out the inventory (<c>meadworks.owns.inventory</c>, its
    /// <c>1. Inventory / Enabled</c>), the local player's main grid (<c>meadworks.inventory.layout</c>, the numbers it
    /// writes to <c>Mantle.mainGrid</c>), the key ring (<c>meadworks.inventory.keyring</c>) and the HUD column square
    /// (<c>meadworks.hud.column_square</c>, Crucible.UI's KitColumnSquare copies Mantle's dressed armor square).
    /// </summary>
    internal static class MantleLinks
    {
        const string Prefix = "PlateColumn_plate_", ArmorSuffix = "_mantle_armor", FrameName = "Mantle_stat_icon_frame";
        static readonly Tuple<bool, int, int, int> NotLaidOut = Tuple.Create(false, 0, 0, 0);
        static Tuple<bool, int, int, int> layout = NotLaidOut;
        static Tuple<int, IReadOnlyCollection<string>> ring;

        public static void Register(ProductRegistration registration)
        {
            registration.AddCapability<Func<bool>>(FeatureOwnerContract.Id(FeatureOwnerContract.Inventory), FeatureOwnerContract.ContractVersion, LaysOut);
            registration.AddCapability<Func<Tuple<bool, int, int, int>>>(InventoryLayoutContract.Id, InventoryLayoutContract.ContractVersion, Layout);
            registration.AddCapability<Func<Tuple<int, IReadOnlyCollection<string>>>>(KeyRingContract.Id, KeyRingContract.ContractVersion, KeyRingReading);
            registration.AddCapability<Func<object, Tuple<object, object, object, object>>>(HudColumnSquareContract.Id, HudColumnSquareContract.ContractVersion, ColumnSquare);
        }

        static bool LaysOut() => InventorySettings.Enabled.Value;

        /// <summary>
        /// Not laying out while the master switch is off; null (no grid yet) while it is on and the local player's layout
        /// is not applied, as a missing <c>Mantle.mainGrid</c> read before; otherwise the grid that key holds.
        /// </summary>
        static Tuple<bool, int, int, int> Layout()
        {
            if (!InventorySettings.Enabled.Value) return NotLaidOut;
            if (!InventoryState.Active || !InventoryState.IsLocal(Player.m_localPlayer)) return null;
            var current = InventoryState.Layout;
            if (!layout.Item1 || layout.Item2 != current.Width || layout.Item3 != current.MainRows || layout.Item4 != current.BlockedCells)
                layout = Tuple.Create(true, current.Width, current.MainRows, current.BlockedCells);
            return layout;
        }

        static Tuple<int, IReadOnlyCollection<string>> KeyRingReading()
        {
            IReadOnlyList<string> items = KeyRing.Prefabs;
            int stack = KeyRingSettings.KeyStack.Value;
            if (ring == null || ring.Item1 != stack || !ReferenceEquals(ring.Item2, items))
                ring = Tuple.Create(stack, (IReadOnlyCollection<string>)items);
            return ring;
        }

        /// <summary>
        /// A copy of Mantle's dressed armor square in <paramref name="value"/> (the HUD column), inactive, as (square, icon,
        /// value text, background); null until the armor square is there and dressed, so no undressed look is copied.
        /// </summary>
        static Tuple<object, object, object, object> ColumnSquare(object value)
        {
            if (!(value is RectTransform column) || column == null) return null;
            Transform armor = null;
            foreach (Transform child in column)
                if (child.name.StartsWith(Prefix, StringComparison.Ordinal) && child.name.EndsWith(ArmorSuffix, StringComparison.Ordinal) && Dressed(child)) armor = child;
            if (armor == null) return null;
            var go = Object.Instantiate(armor.gameObject, column, false);
            go.SetActive(false);
            var label = go.GetComponentInChildren<TMP_Text>(true);
            Image background = null, picture = null;
            foreach (Transform child in go.transform)
            {
                var image = child.GetComponent<Image>();
                if (image == null || (label != null && label.transform.IsChildOf(child))) continue;
                if (Dressed(child)) background = image; else if (picture == null) picture = image;
            }
            if (label == null || background == null || picture == null) { Object.Destroy(go); return null; }
            return Tuple.Create<object, object, object, object>(go.transform, picture, label, background);
        }

        static bool Dressed(Transform box)
        {
            foreach (var part in box.GetComponentsInChildren<Transform>(true))
                if (part.name == FrameName) return true;
            return false;
        }
    }
}
