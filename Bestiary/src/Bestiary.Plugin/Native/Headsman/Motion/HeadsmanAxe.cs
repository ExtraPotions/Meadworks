// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The bone greataxe's shape in its own frame (the bundle's ecp_headsman_axe and ecp_greataxe; AssetWorkshop
    /// HeadsmanAxe): the butt at the origin, the haft up +Y through an S-curve to the head at 1.5 m, the blade out along
    /// -X. The pieces of the boss's axe are laid in this frame, bottom up (the haft's 22 vertebrae, the blade's five
    /// shards, the hooked back in two).
    /// </summary>
    public static class HeadsmanAxe
    {
        public const int Pieces = 29;

        /// <summary>The middle of the blade's cutting edge, what bites the ground in the slam and the scrape.</summary>
        public static readonly Vector3 Edge = new Vector3(-0.54f, 1.2f, 0f);

        /// <summary>The haft's centre at a height, as the model is built.</summary>
        public static Vector3 Spine(float height)
        {
            float t = Mathf.Clamp01((height - 0.065f) / 1.102f);
            return new Vector3(-0.040f * Mathf.Sin(t * 2f * Mathf.PI), height, -0.012f * Mathf.Sin(t * Mathf.PI));
        }

        /// <summary>The haft's direction at a height, butt to head.</summary>
        public static Vector3 Along(float height) => (Spine(height + 0.002f) - Spine(height - 0.002f)).normalized;
    }
}
