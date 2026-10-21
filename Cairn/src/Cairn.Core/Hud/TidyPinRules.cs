// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room: the presentation rules of Tidy Pins, from the user's requests (2026-10-07 and 2026-10-19).
// TidyPins is a behavior reference only; no TidyPins code, DLL or implementation was inspected.
namespace ExtraPotions.Cairn.Hud
{
    /// <summary>How Tidy Pins draws one large- or small-map pin. Unity-free; the plugin applies the results.</summary>
    public static class TidyPinRules
    {
        public const float IconScale = 0.75f;
        public const float CheckedAlpha = 0.35f;

        /// <summary>Live pins that keep the game's size and opacity: other players, pings and shouts.</summary>
        public static bool KeepsGameLook(string pinType) =>
            pinType == "Player" || pinType == "Ping" || pinType == "Shout";

        /// <summary>The factor on the pin's existing scale: smaller unless kept, times <paramref name="hoverScale"/> under the cursor.</summary>
        public static float Scale(string pinType, bool hovered, float hoverScale)
        {
            var scale = KeepsGameLook(pinType) ? 1f : IconScale;
            if (hovered && !float.IsNaN(hoverScale) && hoverScale > 1f) scale *= hoverScale;
            return scale;
        }

        /// <summary>The factor on the pin's existing opacity: crossed-out pins fade unless kept.</summary>
        public static float Alpha(string pinType, bool isChecked) => isChecked && !KeepsGameLook(pinType) ? CheckedAlpha : 1f;

        /// <summary>Names show only on the large map, for the hovered pin, or every pin while the show-all key is held.</summary>
        public static bool ShowsName(bool largeMap, bool hovered, bool showAll, string name) =>
            largeMap && (hovered || showAll) && !string.IsNullOrEmpty(name);
    }
}
