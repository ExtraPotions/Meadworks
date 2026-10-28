// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the ship panel (design 10, 4.5, K09) and the boat camera distance (C13; TheGreatestShips README
// behavior: 8 to 40 m, 16 the game's limit; no code read).
using System;
using System.Collections.Generic;
using System.Globalization;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Localization;

namespace ExtraPotions.Keel.Display
{
    /// <summary>One panel row: its label key, the value shown right of it, and the key of the value's unit (empty for none).</summary>
    public readonly struct PanelLine
    {
        public PanelLine(string key, string number, string unitKey)
        {
            Key = key;
            Number = number;
            UnitKey = unitKey;
        }

        public string Key { get; }
        public string Number { get; }

        /// <summary>The speed unit or meters; empty on the multiplier row.</summary>
        public string UnitKey { get; }
    }

    /// <summary>One ability's meter on the panel: shown or not, its text key and argument, and how full it is (0 to 1).</summary>
    public readonly struct AbilityMeter
    {
        public AbilityMeter(bool visible, string key, string argument, float fill)
        {
            Visible = visible;
            Key = key;
            Argument = argument;
            Fill = fill;
        }

        public bool Visible { get; }
        public string Key { get; }
        public string Argument { get; }
        public float Fill { get; }
    }

    public static class PanelModel
    {
        /// <summary>Pixels between the panel and the screen's right edge, and below the minimap.</summary>
        public const int Margin = 10;
        public const int Gap = 8;

        public const float MinimumBoatCamera = 8f;
        public const float MaximumBoatCamera = 40f;

        private const double MetresPerNauticalMile = 1852.0;

        /// <summary>The ship's speed in the chosen unit (forward or backward, shown without sign).</summary>
        public static double Speed(float metresPerSecond, SpeedUnit unit)
        {
            if (float.IsNaN(metresPerSecond) || float.IsInfinity(metresPerSecond)) return 0;
            var mps = Math.Abs((double)metresPerSecond);
            switch (unit)
            {
                case SpeedUnit.Knots: return mps * 3600.0 / MetresPerNauticalMile;
                case SpeedUnit.KilometersPerHour: return mps * 3.6;
                default: return mps;
            }
        }

        /// <summary>
        /// The top speed against the vanilla ship of this hull: the game's forward drag grows with the square of the
        /// speed, so speed follows the square root of the sail-force-to-drag ratio; the helmsman's Sailing factor multiplies in.
        /// Without known positive vanilla and effective values only the helm factor is shown.
        /// </summary>
        public static double SpeedFactor(float sailForce, float vanillaSailForce, float forwardDrag, float vanillaForwardDrag, float helmFactor)
        {
            var helm = float.IsNaN(helmFactor) || float.IsInfinity(helmFactor) || helmFactor <= 0f ? 1.0 : helmFactor;
            if (float.IsNaN(sailForce) || float.IsInfinity(sailForce) || sailForce < 0f
                || float.IsNaN(vanillaSailForce) || float.IsInfinity(vanillaSailForce) || vanillaSailForce <= 0f
                || float.IsNaN(forwardDrag) || float.IsInfinity(forwardDrag) || forwardDrag <= 0f
                || float.IsNaN(vanillaForwardDrag) || float.IsInfinity(vanillaForwardDrag) || vanillaForwardDrag <= 0f)
                return helm;
            return Math.Sqrt((sailForce / (double)forwardDrag) / (vanillaSailForce / (double)vanillaForwardDrag)) * helm;
        }

        /// <summary>The Solo ship panel's rows (ShipConfig layout): speed (one decimal), speed multiplier ("x" and two decimals),
        /// explore radius (whole meters).</summary>
        public static IEnumerable<PanelLine> Lines(float metresPerSecond, SpeedUnit unit, double speedFactor, float radius, CultureInfo culture)
        {
            var c = culture ?? CultureInfo.InvariantCulture;
            yield return new PanelLine(KeelTextKeys.PanelSpeed, Speed(metresPerSecond, unit).ToString("0.0", c), KeelTextKeys.Unit(unit));
            yield return new PanelLine(KeelTextKeys.PanelSpeedFactor, "x" + (double.IsNaN(speedFactor) ? 1.0 : speedFactor).ToString("0.00", c), "");
            yield return new PanelLine(KeelTextKeys.PanelRadius, (float.IsNaN(radius) ? 0f : Math.Max(0f, radius)).ToString("0", c), KeelTextKeys.UnitMetres);
        }

        /// <summary>
        /// An ability's meter (user decision 2026-10-08: one kit meter per ability): hidden when the ability does not
        /// exist (Sailing off, unlock level above 100); empty with "needs Sailing N" below its level; filling while it
        /// recharges, with the whole seconds left; full and "ready" otherwise.
        /// </summary>
        public static AbilityMeter Ability(bool unlockable, float level, float unlockLevel, float cooldownLeft, float cooldownLength)
        {
            if (!unlockable) return new AbilityMeter(false, "", "", 0f);
            if (level < unlockLevel)
                return new AbilityMeter(true, KeelTextKeys.AbilityNeedsLevel, Math.Ceiling(unlockLevel).ToString("0", CultureInfo.InvariantCulture), 0f);
            var left = float.IsNaN(cooldownLeft) ? 0f : Math.Max(0f, cooldownLeft);
            if (left <= 0f || !(cooldownLength > 0f)) return new AbilityMeter(true, KeelTextKeys.AbilityReady, "", 1f);
            var fill = Math.Min(1f, Math.Max(0f, 1f - left / cooldownLength));
            return new AbilityMeter(true, KeelTextKeys.AbilityCooldown, Math.Ceiling(left).ToString("0", CultureInfo.InvariantCulture), fill);
        }

        /// <summary>The panel shows aboard a ship on a graphical client while Keel acts, unless a menu, the inventory or the map is open.</summary>
        public static bool Visible(bool setting, bool allowsUi, bool productActive, bool aboard, bool blocked) =>
            setting && allowsUi && productActive && aboard && !blocked;

        /// <summary>As <see cref="Visible(bool, bool, bool, bool, bool)"/>, but asks whether a menu, the inventory or the map is open only
        /// when the panel could otherwise show: the game's own UI probes are not safe at the main menu (Game.IsPaused reads ZNet).</summary>
        public static bool Visible(bool setting, bool allowsUi, bool productActive, bool aboard, Func<bool> blocked) =>
            setting && allowsUi && productActive && aboard && !blocked();

        /// <summary>The panel's top-left corner with zero offsets: right-aligned under the minimap (y grows downwards).</summary>
        public static (int X, int Y) Home(int parentWidth, int panelWidth, int minimapBottom) =>
            (parentWidth - panelWidth - Margin, Math.Max(0, minimapBottom) + Gap);


        /// <summary>The boat camera's farthest distance: the setting (8 to 40 m) while Keel acts, else the game's own.</summary>
        public static float BoatCamera(bool active, float setting, float original)
        {
            if (!active || float.IsNaN(setting) || float.IsInfinity(setting)) return original;
            return Math.Min(MaximumBoatCamera, Math.Max(MinimumBoatCamera, setting));
        }

    }
}
