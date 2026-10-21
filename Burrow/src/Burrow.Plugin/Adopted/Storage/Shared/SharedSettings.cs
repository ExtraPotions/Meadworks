using BepInEx.Configuration;
using ExtraPotions.Burrow.Embedded.SyncedConfig;
using UnityEngine;

namespace Burrow.Shared
{
    /// <summary>
    /// Section "9. Shared". The timeout and the touch duration change what every player experiences of a shared
    /// chest and are synced; whether touches are shown and in which colour is per player. Values are read at use
    /// time.
    /// </summary>
    public static class SharedSettings
    {
        public const string Section = "9. Shared";
        public const string DefaultColour = "#ffb347";

        public static ConfigEntry<float> TransferRadius { get; private set; }

        public static ConfigEntry<float> RequestTimeout { get; private set; }
        public static ConfigEntry<float> TouchSeconds { get; private set; }
        public static ConfigEntry<bool> ShowTouches { get; private set; }
        public static ConfigEntry<string> TouchColour { get; private set; }

        public static void Bind(SyncedConfiguration synced)
        {
            TransferRadius = synced.Bind(Section, "Transfer Radius", 30f,
                "Maximum authenticated player distance in meters for a new shared chest transfer.",
                acceptableValues: new AcceptableValueRange<float>(0f, 50f));
            RequestTimeout = synced.Bind(Section, "Request Timeout", 2f,
                "Seconds to wait for an answer from the player whose game currently controls a shared chest, before the center message says 'The chest did not answer'. The transfer is not canceled: the items stay held, and the transfer finishes when the answer arrives later.",
                acceptableValues: new AcceptableValueRange<float>(0.5f, 30f));
            TouchSeconds = synced.Bind(Section, "Touch Seconds", 5f,
                "Seconds a slot stays marked after another player pressed the mouse on it, unless the player lets go earlier.",
                acceptableValues: new AcceptableValueRange<float>(0.5f, 60f));
            ShowTouches = synced.Bind(Section, "Show Touches", true,
                "Slots another player is moving are tinted and the tooltip says who.", synced: false);
            TouchColour = synced.Bind(Section, "Touch Color", DefaultColour,
                "Color (hex, #rrggbb) of a slot another player is moving.", synced: false);
        }

        /// <summary>The touch colour, or the default when the setting does not parse.</summary>
        public static Color Colour()
        {
            string value = (TouchColour.Value ?? "").Trim();
            if (ColorUtility.TryParseHtmlString(value, out Color colour))
                return colour;
            ColorUtility.TryParseHtmlString(DefaultColour, out colour);
            return colour;
        }
    }
}
