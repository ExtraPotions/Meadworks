// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public enum KitFontRole { Body, Emphasis, Title }
    // New roles go at the end so stored or compiled values keep their meaning.
    public enum KitColorRole { Text, Muted, Accent, Good, Bad, Warning, OnPrimary, Shade, Heading }
    public enum KitSurfaceRole { Window, Frame, Inset, Card, Button, ButtonHover, ButtonPressed, ButtonDisabled, Field, Cell, Row, Tab }

    /// <summary>
    /// The engine-free half of <see cref="KitAppearance"/>: each color role's palette entry and each surface's source
    /// pixels per UI unit in the Timber and Brown looks. Colors are the same in both looks; only surfaces change.
    /// </summary>
    public static class KitAppearanceRules
    {
        /// <summary>Source pixels per UI unit of the forged Timber window pieces and buttons, and of Brown's 200-PPU panel.</summary>
        public const float ForgedPixelsPerUnit = 2f;

        /// <summary>The unit a canvas or sprite uses when it does not say.</summary>
        public const float DefaultReferencePixelsPerUnit = 100f;

        public static KitColor Color(KitColorRole role)
        {
            switch (role)
            {
                case KitColorRole.Text: return KitPalette.SheetLabel;
                case KitColorRole.Muted: return KitPalette.TabDimText;
                case KitColorRole.Accent: return KitPalette.SheetValue;
                case KitColorRole.Good: return new KitColor(.55f, .85f, .45f, 1f);
                case KitColorRole.Bad: return new KitColor(1f, .45f, .38f, 1f);
                case KitColorRole.Warning: return KitPalette.TabLitText;
                case KitColorRole.OnPrimary: return KitPalette.TabLitText;
                case KitColorRole.Shade: return KitPopupShade.Colour;
                case KitColorRole.Heading: return KitPalette.SheetHeading;
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        /// <summary>Source pixels per UI unit a surface is drawn at in the Timber (<paramref name="timber"/>) or Brown look.</summary>
        public static float PixelsPerUnit(KitSurfaceRole role, bool timber)
        {
            switch (role)
            {
                case KitSurfaceRole.Field:
                case KitSurfaceRole.Cell:
                case KitSurfaceRole.Row: return 1f;
                case KitSurfaceRole.Window:
                case KitSurfaceRole.Card:
                case KitSurfaceRole.Frame: return ForgedPixelsPerUnit;
                case KitSurfaceRole.Inset:
                case KitSurfaceRole.Button:
                case KitSurfaceRole.ButtonHover:
                case KitSurfaceRole.ButtonPressed:
                case KitSurfaceRole.ButtonDisabled:
                case KitSurfaceRole.Tab: return timber ? ForgedPixelsPerUnit : 1f;
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        /// <summary>
        /// An Image's pixels-per-unit multiplier that draws <paramref name="pixelsPerUnit"/> source pixels per UI unit,
        /// whatever the canvas reference and the sprite's own scale (unknown or non-positive ones count as 100).
        /// </summary>
        public static float Multiplier(float pixelsPerUnit, float canvasReference, float spritePixelsPerUnit)
        {
            float reference = canvasReference > 0f ? canvasReference : DefaultReferencePixelsPerUnit;
            float sprite = spritePixelsPerUnit > 0f ? spritePixelsPerUnit : DefaultReferencePixelsPerUnit;
            return Math.Max(0.01f, pixelsPerUnit * reference / sprite);
        }
    }
}
