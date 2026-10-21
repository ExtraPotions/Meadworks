// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// Creates the glow light (display.md section 5): a point light without shadows on a child of the dropped item, so it
    /// dies with the item when it is picked up. Not the game's <c>LightLod</c>: its limit is shared with every torch and
    /// fire, and our cap counts only our lights. Viewing client only; never created on a headless server.
    /// </summary>
    internal static class GlowLights
    {
        public const string ChildName = "ecf_glow";

        /// <summary>A dedicated server renders nothing (the game's own test; <c>ZNet.IsDedicated()</c> is always false).</summary>
        public static bool Headless => SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;

        public static Light Create(Transform parent)
        {
            GameObject child = new GameObject(ChildName);
            child.transform.SetParent(parent, worldPositionStays: false);
            Light light = child.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
            light.enabled = false;
            return light;
        }
    }
}
