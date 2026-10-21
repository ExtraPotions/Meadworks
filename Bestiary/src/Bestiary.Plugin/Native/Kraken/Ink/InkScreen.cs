// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// Ink on the local player's screen; what the ink status effect calls. It keeps the splat textures (the kraken's
    /// bundle's, or ones drawn in code when there are none) and the one overlay in the current HUD, built the first
    /// time it is needed and again after the HUD was replaced (each world load brings a new one). With no local player
    /// or no HUD it does nothing. Only ever draws on this machine.
    /// </summary>
    public static class InkScreen
    {
        private static Texture2D[] textures = Array.Empty<Texture2D>();
        private static Texture2D[]? drawn;
        private static InkOverlay? overlay;

        /// <summary>The splat textures to deal from; an empty set means the ones drawn in code.</summary>
        public static void Use(Texture2D[] splats) =>
            textures = splats == null ? Array.Empty<Texture2D>() : Array.FindAll(splats, t => t != null);

        /// <summary>
        /// Covers the local player's screen now for <paramref name="hold"/> seconds, then fades it out over
        /// <paramref name="fade"/>; starts over if already inked.
        /// </summary>
        public static void Splat(float hold, float fade)
        {
            InkOverlay? shown = Overlay();
            if (shown != null)
            {
                shown.Show(Textures(), Mathf.Max(0f, hold), Mathf.Max(0f, fade));
            }
        }

        /// <summary>Takes the ink off the screen at once.</summary>
        public static void Clear()
        {
            if (overlay != null)
            {
                overlay.Hide();
            }
        }

        /// <summary>The overlay went with its HUD: drop the reference so the next splat builds one in the new HUD.</summary>
        internal static void Forget(InkOverlay gone)
        {
            if (ReferenceEquals(overlay, gone))
            {
                overlay = null;
            }
        }

        private static InkOverlay? Overlay()
        {
            if (overlay != null)
            {
                return overlay;
            }
            Hud hud = Hud.instance;
            if (hud == null || hud.m_damageScreen == null || Player.m_localPlayer == null)
            {
                return null;
            }
            overlay = InkOverlay.Create(hud.m_damageScreen.transform);
            return overlay;
        }

        private static Texture2D[] Textures()
        {
            Texture2D[] live = Array.FindAll(textures, t => t != null);
            if (live.Length > 0)
            {
                return live;
            }
            if (drawn == null || Array.Exists(drawn, t => t == null))
            {
                drawn = InkSplats.Build();
            }
            return drawn;
        }
    }
}
