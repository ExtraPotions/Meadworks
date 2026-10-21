// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The pale, see-through look of a thing coming into being (the new axe in the Executioner's hands, the bones of the
    /// skeleton it raises): whitish, greenish, bluish, half clear. A flat colour on a transparent shader the game has
    /// loaded; where none is found, a copy of the thing's own material tinted the same (opaque, but still pale).
    /// </summary>
    public static class HeadsmanGhost
    {
        public static readonly Color Colour = new Color(0.5f, 0.92f, 0.85f, 0.55f);
        private static readonly string[] Shaders = { "Sprites/Default", "Legacy Shaders/Particles/Alpha Blended", "Particles/Standard Unlit", "UI/Default" };
        private static readonly Dictionary<Material, Material> tinted = new Dictionary<Material, Material>();
        private static Material? clear;

        /// <summary>The ghost material; `fallback` is tinted instead when the game has no transparent shader loaded.</summary>
        public static Material For(Material fallback)
        {
            if (clear != null)
            {
                return clear;
            }
            foreach (string name in Shaders)
            {
                Shader? shader = BundleEffects.FindShader(name);
                if (shader != null)
                {
                    clear = new Material(shader) { name = "ecp_headsman_ghost", color = Colour, renderQueue = 3100 };
                    return clear;
                }
            }
            if (!tinted.TryGetValue(fallback, out Material pale))
            {
                pale = tinted[fallback] = new Material(fallback) { name = "ecp_headsman_ghost_opaque", color = Colour };
            }
            return pale;
        }
    }
}
