// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// The dark sunk inset behind a kit window's reading area, for windows with plain labels.
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;

namespace ExtraPotions.Wildblood.Integration;

internal static class WildbloodInsets
{
    /// <summary>An inset at (x, -y) of the given size from the window's top left. Call right after the window is made so it draws behind the labels.</summary>
    internal static void Add(RectTransform panel, string name, Vector2 at, Vector2 size)
    {
        var area = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        area.gameObject.layer = panel.gameObject.layer;
        area.SetParent(panel, false);
        area.anchorMin = area.anchorMax = area.pivot = new Vector2(0, 1);
        area.anchoredPosition = at;
        area.sizeDelta = size;
        KitForged.Inset(area);
    }
}
