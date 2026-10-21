// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: HUD badge state and warning cue (design 4.3, 8, W8).
using System;
using ExtraPotions.Tempest.Events;
using ExtraPotions.Tempest.Localization;

namespace ExtraPotions.Tempest.Integration
{
    /// <summary>What the badge shows: the event kind, its phase, seconds left and the fraction of the phase left.</summary>
    public readonly struct BadgeState
    {
        public BadgeState(bool visible, EventKind kind, EventPhase phase, double secondsLeft, double fraction, bool dry = false)
        {
            Dry = dry;
            Visible = visible;
            Kind = kind;
            Phase = phase;
            SecondsLeft = secondsLeft;
            Fraction = fraction;
        }

        public static BadgeState Hidden => default;

        public bool Visible { get; }
        public EventKind Kind { get; }

        /// <summary>A dry thunderstorm (its name says so).</summary>
        public bool Dry { get; }

        public EventPhase Phase { get; }
        public double SecondsLeft { get; }
        public double Fraction { get; }
    }

    /// <summary>The kit HUD badge (design 8): visible while an event's warning, storm or clearing reaches the local player.</summary>
    public static class Badge
    {
        /// <param name="otherHudShows">Cairn's weather HUD shows the warning (Tempest then hides its badge).</param>
        public static BadgeState For(TempestEvent? e, double now, WeatherBiome localBiome, bool gameForced, bool badgeOn, bool otherHudShows)
        {
            if (e == null || !badgeOn || otherHudShows || gameForced || !e.Affects(localBiome)) return BadgeState.Hidden;
            var state = Timeline.At(e, now);
            if (state.Phase == EventPhase.Idle) return BadgeState.Hidden;
            var length = state.Phase == EventPhase.Warning ? e.WarningSeconds : state.Phase == EventPhase.Active ? e.ActiveSeconds : e.ClearingSeconds;
            var left = Math.Max(0, state.SecondsLeft);
            var fraction = length > 0 ? Math.Max(0, Math.Min(1, left / length)) : 0;
            return new BadgeState(true, e.Kind, state.Phase, left, fraction, e.Dry);
        }

        /// <summary>Badge first line: the event's name.</summary>
        public static string Title(BadgeState state, Func<string, string> text)
        {
            if (!state.Visible) return "";
            if (text == null) throw new ArgumentNullException(nameof(text));
            return text(TempestTextKeys.Kind(state.Kind, state.Dry));
        }

        /// <summary>Badge second line: the phase's countdown ("Arrives in", "Time remaining", "Clears in").</summary>
        public static string Detail(BadgeState state, Func<string, string, string> format)
        {
            if (!state.Visible) return "";
            if (format == null) throw new ArgumentNullException(nameof(format));
            return format(TempestTextKeys.BadgeEta(state.Phase), TempestTextKeys.Countdown(state.SecondsLeft));
        }
        /// <summary>Badge line from catalog keys: kind, phase and countdown.</summary>
        public static string Text(BadgeState state, Func<string, string> text)
        {
            if (!state.Visible) return "";
            if (text == null) throw new ArgumentNullException(nameof(text));
            return text(TempestTextKeys.Kind(state.Kind, state.Dry)) + " " + text(TempestTextKeys.Phase(state.Phase)) + " " + TempestTextKeys.Countdown(state.SecondsLeft);
        }
    }

    /// <summary>Center messages for weather phases.</summary>
    public static class CenterNotice
    {
        /// <summary>Shown when Warning Message is on and Cairn's weather display is not already showing the event (one message, not two).</summary>
        public static bool Show(bool warningMessageOn, bool otherHudShows) => warningMessageOn && !otherHudShows;
    }

    /// <summary>The warning sound plays once per event, when its warning first reaches the local player outside a forced environment.</summary>
    public sealed class WarningCue
    {
        int played;

        public bool Observe(TempestEvent? e, double now, WeatherBiome localBiome, bool gameForced)
        {
            if (e == null || e.Serial == played || gameForced || !e.Affects(localBiome)) return false;
            if (Timeline.At(e, now).Phase != EventPhase.Warning) return false;
            played = e.Serial;
            return true;
        }
    }

    /// <summary>
    /// The game sound the warning plays: the frost start for a blizzard, the weather thunder for a storm. These are effect
    /// prefabs referenced by the game's own status effects and thunder environments, not necessarily ZNetScene prefabs.
    /// </summary>
    public static class WarningSound
    {
        public const string Storm = "sfx_thunder";
        public const string Blizzard = "sfx_Frost_Start";

        public static string For(EventKind kind) => kind == EventKind.Blizzard ? Blizzard : Storm;

        /// <summary>True when a game prefab name is the wanted sound (exact, ordinal).</summary>
        public static bool Matches(string? prefabName, string wanted) => prefabName != null && string.Equals(prefabName, wanted, StringComparison.Ordinal);
    }
}
