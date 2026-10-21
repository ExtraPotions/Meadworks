// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: pin color by access mode (parity #72). PotalMap's documented colors (behaviour reference: public
// magenta, private blue); Admin, which PotalMap does not have, is orange. Portals that let every item through are gray
// while Highlight Stone Portals is on. The list group headers use the same colors.
using System;
using ExtraPotions.Threshold.Access;
using ExtraPotions.Threshold.Picker;

namespace ExtraPotions.Threshold.Map
{
    /// <summary>An RGBA color, 0 to 1 per channel (Unity-free).</summary>
    public readonly struct PinColour : IEquatable<PinColour>
    {
        public PinColour(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }

        public static PinColour White { get; } = new PinColour(1f, 1f, 1f, 1f);

        public PinColour WithAlpha(float alpha) => new PinColour(R, G, B, alpha);

        public bool Equals(PinColour other) => R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && A.Equals(other.A);
        public override bool Equals(object? obj) => obj is PinColour other && Equals(other);
        public override int GetHashCode() => (R, G, B, A).GetHashCode();
        public override string ToString() => "(" + R + ", " + G + ", " + B + ", " + A + ")";
    }

    public static class MapPinColours
    {
        public static PinColour Public { get; } = new PinColour(0.95f, 0.45f, 0.95f);
        public static PinColour Private { get; } = new PinColour(0.45f, 0.65f, 1f);
        public static PinColour Admin { get; } = new PinColour(1f, 0.55f, 0.2f);

        /// <summary>Portals that let every item through (Highlight Stone Portals).</summary>
        public static PinColour Stone { get; } = new PinColour(0.72f, 0.72f, 0.72f);

        /// <summary>The picker's Return To Hub pin (unchanged from 0.2.0).</summary>
        public static PinColour ReturnHub { get; } = new PinColour(1f, 0.85f, 0.4f);

        /// <summary>The portal walked into is drawn faded in the picker.</summary>
        public const float HereAlpha = 0.6f;

        public static PinColour Of(AccessMode mode)
        {
            switch (mode)
            {
                case AccessMode.Private: return Private;
                case AccessMode.Admin: return Admin;
                default: return Public;
            }
        }

        /// <summary>A destination's color in the list and on the maps: gray for a stone portal, else its mode's color.</summary>
        public static PinColour Tint(MapPinSpec pin)
        {
            if (pin == null) throw new ArgumentNullException(nameof(pin));
            return pin.IsStone ? Stone : Of(pin.Mode);
        }

        /// <summary>The color of a list group header: the mode groups take their mode's color, the Stone group gray;
        /// Favorites and Hubs mix modes and keep the plain text (null).</summary>
        public static PinColour? Header(PickerCategory category)
        {
            switch (category)
            {
                case PickerCategory.Public: return Public;
                case PickerCategory.Private: return Private;
                case PickerCategory.Admin: return Admin;
                case PickerCategory.Stone: return Stone;
                default: return null;
            }
        }

        /// <summary>A pin's tint: the player arrow plain, the Return To Hub pin gold, the portal walked into faded in its
        /// color, every other pin its color (<see cref="Tint"/>).</summary>
        public static PinColour ForPin(MapPinSpec pin, bool returnHub)
        {
            if (pin == null) throw new ArgumentNullException(nameof(pin));
            if (pin.Kind == MapPinKind.Player) return PinColour.White;
            if (returnHub) return ReturnHub;
            var colour = Tint(pin);
            return pin.Kind == MapPinKind.Here ? colour.WithAlpha(HereAlpha) : colour;
        }
    }
}
