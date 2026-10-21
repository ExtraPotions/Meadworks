// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Meadworks.Contracts;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// Both ends of the <c>meadworks.hud.occupied</c> capability (Crucible.Contracts' <c>HudOccupiedContract</c>): a product
    /// publishes the rectangle its HUD panel covers with <see cref="Measure"/>; another product keeps clear of every other
    /// product's rectangle with <see cref="Lowest"/>, instead of finding their panels by object name.
    /// </summary>
    public static class KitHudOccupancy
    {
        private static readonly Vector3[] corners = new Vector3[4];
        private static readonly Tuple<bool, float, float, float, float> Hidden = Tuple.Create(false, 0f, 0f, 0f, 0f);

        /// <summary>
        /// The reading for <paramref name="panel"/> under <paramref name="root"/>: shown while it is active, as left, top,
        /// width, height in the root's units from its top-left corner, y down.
        /// </summary>
        public static Tuple<bool, float, float, float, float> Measure(RectTransform root, RectTransform panel)
        {
            if (root == null || panel == null || !panel.gameObject.activeInHierarchy)
                return Hidden;
            panel.GetWorldCorners(corners);
            Vector3 a = root.InverseTransformPoint(corners[0]), b = root.InverseTransformPoint(corners[2]);
            float left = Mathf.Min(a.x, b.x) - root.rect.xMin, right = Mathf.Max(a.x, b.x) - root.rect.xMin;
            float top = root.rect.yMax - Mathf.Max(a.y, b.y), bottom = root.rect.yMax - Mathf.Min(a.y, b.y);
            return right > left && bottom > top ? Tuple.Create(true, left, top, right - left, bottom - top) : Hidden;
        }

        /// <summary>
        /// The lowest bottom edge (root units from the top, y down) of every other product's shown HUD rectangle that
        /// overlaps the span [<paramref name="left"/>, <paramref name="right"/>] horizontally; -1 when none does.
        /// Resolves the providers afresh, so call it when laying out, not every frame.
        /// </summary>
        public static float Lowest(CapabilityRegistry registry, string ownProductId, float left, float right)
        {
            float lowest = -1f;
            if (registry == null)
                return lowest;
            foreach (var provider in registry.ResolveEach<Func<Tuple<bool, float, float, float, float>>>(HudOccupiedContract.Id, HudOccupiedContract.ContractVersion))
            {
                if (provider.Key == ownProductId)
                    continue;
                if (HudOccupiedContract.TryRead(provider.Value, out bool shown, out float x, out float top, out float width, out float height)
                    && shown && x < right && x + width > left)
                    lowest = Mathf.Max(lowest, top + height);
            }
            return lowest;
        }
    }
}
