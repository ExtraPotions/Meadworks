// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>Shared appearance for product-owned UI, using the installed MeadWorks art and native font fallbacks.</summary>
    public static class KitAppearance
    {
        public static bool Active => CrucibleUiPlugin.Active && KitLook.On;

        public static event Action Changed
        {
            add => KitLook.Changed += value;
            remove => KitLook.Changed -= value;
        }

        public static TMP_FontAsset Font(KitFontRole role) => role == KitFontRole.Title ? KitCanvas.Heading : KitCanvas.Font;

        /// <summary>The same in Timber and Brown; with styling off a caller may still use it for its own text.</summary>
        public static Color Color(KitColorRole role) => KitUnity.Colour(KitAppearanceRules.Color(role));

        /// <summary>Null with styling disabled: the caller keeps or restores its native presentation.</summary>
        public static Sprite Sprite(KitSurfaceRole role)
        {
            if (!Active) return null;
            if (!KitLook.Timber)
            {
                switch (role)
                {
                    case KitSurfaceRole.Window:
                    case KitSurfaceRole.Card: return KitArt.BrownPanel;
                    // Brown's panel already contains its border; no separate overlay is needed.
                    case KitSurfaceRole.Frame: return null;
                    case KitSurfaceRole.Inset:
                    case KitSurfaceRole.Field:
                    case KitSurfaceRole.Cell:
                    case KitSurfaceRole.Row: return KitArt.Cell;
                    case KitSurfaceRole.Button:
                    case KitSurfaceRole.ButtonDisabled:
                    case KitSurfaceRole.Tab: return KitArt.BrownButton;
                    case KitSurfaceRole.ButtonHover: return KitArt.ButtonHover;
                    case KitSurfaceRole.ButtonPressed: return KitArt.ButtonPressed;
                    default: throw new ArgumentOutOfRangeException(nameof(role));
                }
            }
            switch (role)
            {
                case KitSurfaceRole.Window: return KitForgedArt.WindowWood;
                case KitSurfaceRole.Frame: return KitForgedArt.WindowFrame;
                case KitSurfaceRole.Inset: return KitForgedArt.WindowInset;
                case KitSurfaceRole.Card: return KitForgedArt.SidePlate;
                case KitSurfaceRole.Button:
                case KitSurfaceRole.Tab: return KitForgedArt.Button;
                case KitSurfaceRole.ButtonHover: return KitForgedArt.ButtonHover;
                case KitSurfaceRole.ButtonPressed: return KitForgedArt.ButtonPressed;
                case KitSurfaceRole.ButtonDisabled: return KitForgedArt.ButtonDisabled;
                case KitSurfaceRole.Field:
                case KitSurfaceRole.Cell:
                case KitSurfaceRole.Row: return KitArt.Cell;
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        /// <summary>Source pixels per UI unit for the current look; <see cref="Fit(Image, KitSurfaceRole)"/> applies it.</summary>
        public static float PixelsPerUnit(KitSurfaceRole role) => KitAppearanceRules.PixelsPerUnit(role, KitLook.Timber);

        /// <summary>
        /// Sets <paramref name="image"/>'s pixels-per-unit multiplier so its current sprite draws at the surface's scale
        /// in the current look, whatever the canvas reference. Call again from <see cref="Changed"/> after a look change.
        /// </summary>
        public static void Fit(Image image, KitSurfaceRole role) => Fit(image, PixelsPerUnit(role));

        /// <summary>Sets the multiplier for <paramref name="pixelsPerUnit"/> source pixels per UI unit (a caller's own larger value for small controls).</summary>
        public static void Fit(Image image, float pixelsPerUnit)
        {
            if (image == null) return;
            Canvas canvas = image.canvas != null ? image.canvas : image.GetComponentInParent<Canvas>();
            float multiplier = KitAppearanceRules.Multiplier(pixelsPerUnit,
                canvas != null ? canvas.referencePixelsPerUnit : 0f, image.sprite != null ? image.sprite.pixelsPerUnit : 0f);
            // The setter dirties the image's vertices even for the same value; periodic restyling calls this often.
            if (image.pixelsPerUnitMultiplier != multiplier) image.pixelsPerUnitMultiplier = multiplier;
        }
    }
}
