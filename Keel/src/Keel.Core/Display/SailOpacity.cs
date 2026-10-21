// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Sail Opacity, Sail Opacity Both Sides and Sail Opacity On Deck (design C13 and 4.5; user decision
// 2026-10-08). TheGreatestShips README behavior only (1 draws the sail as the game does, lower is see-through on the
// side seen from the tiller, 0 hides it; Both Sides; On Deck); no code read.
using System;

namespace ExtraPotions.Keel.Display
{
    public enum SailLook { Normal, Faded, Hidden }

    public static class SailOpacity
    {
        /// <summary>Opacity at or below this hides the sail.</summary>
        public const float HiddenBelow = 0.01f;

        /// <summary>
        /// The opacity the local player's ship's sail is drawn with: the setting while steering, or anywhere aboard with
        /// On Deck; 1 otherwise. Without Both Sides only a viewer astern of the sail (the tiller side) sees it fade.
        /// </summary>
        public static float Alpha(float opacity, bool steering, bool aboard, bool onDeck, bool bothSides, bool viewerAstern)
        {
            var value = float.IsNaN(opacity) || float.IsInfinity(opacity) ? 1f : Math.Min(1f, Math.Max(0f, opacity));
            if (!(steering || (aboard && onDeck))) return 1f;
            if (!bothSides && !viewerAstern) return 1f;
            return value;
        }

        public static SailLook Look(float alpha)
        {
            if (float.IsNaN(alpha) || alpha >= 1f) return SailLook.Normal;
            return alpha <= HiddenBelow ? SailLook.Hidden : SailLook.Faded;
        }

        /// <summary>The camera's offset from the sail along the ship's heading; negative is astern (the tiller side).</summary>
        public static bool Astern(float cameraForwardOffset) => !(cameraForwardOffset > 0f);
    }
}
