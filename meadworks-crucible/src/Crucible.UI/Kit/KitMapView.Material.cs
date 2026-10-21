// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public sealed partial class KitMapView
    {
        /// <summary>
        /// Draws the map image with this material instead of the plain UI one. The game's map texture holds encoded data the
        /// game's own large-map material turns into terrain, so a product passes a copy of that material (the view never changes
        /// the game's). Null restores the default material.
        /// </summary>
        public void SetMapMaterial(Material material)
        {
            if (disposed)
                return;
            map.material = material;
        }
    }
}
