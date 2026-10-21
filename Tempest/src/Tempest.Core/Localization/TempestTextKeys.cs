// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the localization keys Tempest uses (tokens meadworks_tempest_<key>, design section 11).
using System;
using System.Collections.Generic;
using ExtraPotions.Tempest.Events;

namespace ExtraPotions.Tempest.Localization
{
    public readonly struct TextKey
    {
        public TextKey(string key, int arguments)
        {
            Key = key;
            Arguments = arguments;
        }

        public string Key { get; }

        /// <summary>Number of <c>{0}</c>-style placeholders, numbered from 0.</summary>
        public int Arguments { get; }
    }

    /// <summary>Every key of assets/localization/*.json; the tests hold the catalog to this table.</summary>
    public static class TempestTextKeys
    {
        public const string OffNoCrucible = "off_no_crucible";
        public const string OffNoTempest = "off_no_tempest";
        public const string ImportDone = "import_done";
        public const string ImportPreview = "import_preview";
        public const string ImportUnavailable = "import_unavailable";
        public const string ImportHostOnly = "import_host_only";
        public const string WarningStarted = "warning_started";
        public const string EventStarted = "event_started";
        public const string EventEnded = "event_ended";
        public const string PhaseWarning = "phase_warning";
        public const string PhaseActive = "phase_active";
        public const string PhaseClearing = "phase_clearing";
        public const string BadgeEtaWarning = "badge_eta_warning";
        public const string BadgeEtaActive = "badge_eta_active";
        public const string BadgeEtaClearing = "badge_eta_clearing";
        public const string CommandStatusIdle = "cmd_status_idle";
        public const string CommandStatusEvent = "cmd_status_event";
        public const string CommandStarted = "cmd_started";
        public const string CommandStopped = "cmd_stopped";
        public const string CommandBusy = "cmd_busy";
        public const string CommandNothing = "cmd_nothing";
        public const string CommandNoBiome = "cmd_no_biome";
        public const string CommandCheatOff = "cmd_cheat_off";
        public const string CommandNotAdmin = "cmd_not_admin";
        public const string CommandMischiefDenied = "cmd_mischief_denied";
        public const string CommandRemoteFailed = "cmd_remote_failed";
        public const string CommandSent = "cmd_sent";
        public const string CommandHeadless = "cmd_headless";
        public const string CommandInactive = "cmd_inactive";
        public const string CommandHelp = "cmd_help";
        public const string CommandDryUnsupported = "cmd_dry_unsupported";
        public const string OverrideSet = "override_set";
        public const string OverrideReset = "override_reset";
        public const string OverrideUnknown = "override_unknown";
        public const string OverrideCleared = "override_cleared";

        public const string KindLightningStorm = "kind_lightning_storm";

        /// <summary>The key of an event kind's display name: <c>kind_</c> and the kind's token (<c>kind_blizzard</c>, <c>kind_dense_fog</c>).</summary>
        public static string Kind(EventKind kind) => "kind_" + EventKinds.Token(kind);

        /// <summary>The key of an event's display name: the kind's, or <c>kind_lightning_storm</c> for a lightning storm.</summary>
        public static string Kind(EventKind kind, bool dry) => dry && kind == EventKind.Thunderstorm ? KindLightningStorm : Kind(kind);

        public static string Kind(TempestEvent e) => Kind(e.Kind, e.Dry);

        /// <summary>The key of a phase name for status lines.</summary>
        public static string Phase(EventPhase phase)
        {
            switch (phase)
            {
                case EventPhase.Warning: return PhaseWarning;
                case EventPhase.Active: return PhaseActive;
                case EventPhase.Clearing: return PhaseClearing;
                default: throw new ArgumentException("Idle has no phase text.", nameof(phase));
            }
        }

        /// <summary>The key of the badge's countdown line for a phase ("Arrives in {0}", "Time remaining {0}", "Clears in {0}").</summary>
        public static string BadgeEta(EventPhase phase)
        {
            switch (phase)
            {
                case EventPhase.Warning: return BadgeEtaWarning;
                case EventPhase.Active: return BadgeEtaActive;
                case EventPhase.Clearing: return BadgeEtaClearing;
                default: throw new ArgumentException("Idle has no badge countdown.", nameof(phase));
            }
        }
        public static IReadOnlyList<TextKey> All { get; } = Array.AsReadOnly(new[]
        {
            new TextKey("step_aside_events", 1),
            new TextKey("step_aside_filter", 1),
            new TextKey("step_aside_override", 1),
            new TextKey("off_no_crucible", 0),
            new TextKey("off_no_tempest", 0),
            new TextKey("import_done", 2),
            new TextKey("import_preview", 4),
            new TextKey("import_unavailable", 0),
            new TextKey("import_host_only", 0),
            new TextKey("cheat_weather_commands", 0),
            new TextKey("cheat_personal_override", 0),
            new TextKey("kind_blizzard", 0),
            new TextKey("kind_thunderstorm", 0),
            new TextKey("kind_lightning_storm", 0),
            new TextKey("kind_dense_fog", 0),
            new TextKey("kind_ash_storm", 0),
            new TextKey("kind_meteor_shower", 0),
            new TextKey("warning_started", 2),
            new TextKey("event_started", 1),
            new TextKey("event_ended", 1),
            new TextKey("phase_warning", 0),
            new TextKey("phase_active", 0),
            new TextKey("phase_clearing", 0),
            new TextKey("badge_eta_warning", 1),
            new TextKey("badge_eta_active", 1),
            new TextKey("badge_eta_clearing", 1),
            new TextKey("cmd_status_idle", 0),
            new TextKey("cmd_status_event", 3),
            new TextKey("cmd_started", 1),
            new TextKey("cmd_stopped", 1),
            new TextKey("cmd_busy", 0),
            new TextKey("cmd_nothing", 0),
            new TextKey("cmd_no_biome", 1),
            new TextKey("cmd_cheat_off", 0),
            new TextKey("cmd_not_admin", 0),
            new TextKey("cmd_mischief_denied", 0),
            new TextKey("cmd_remote_failed", 1),
            new TextKey("cmd_sent", 0),
            new TextKey("cmd_headless", 0),
            new TextKey("cmd_inactive", 0),
            new TextKey("cmd_help", 0),
            new TextKey("cmd_dry_unsupported", 0),
            new TextKey("override_set", 1),
            new TextKey("override_reset", 0),
            new TextKey("override_unknown", 1),
            new TextKey("override_cleared", 0),
        });

        /// <summary>The whole second <see cref="Countdown"/> displays. It changes when that text changes.</summary>
        public static long WholeSeconds(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) seconds = 0;
            return (long)Math.Ceiling(seconds - 1e-9);
        }

        /// <summary>Formats seconds as <c>m:ss</c> (or <c>h:mm:ss</c>) for countdowns; negative and non-finite values give <c>0:00</c>.</summary>
        public static string Countdown(double seconds)
        {
            var total = WholeSeconds(seconds);
            var h = total / 3600;
            var m = total / 60 % 60;
            var s = total % 60;
            return h > 0 ? h + ":" + m.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ":" + s.ToString("00", System.Globalization.CultureInfo.InvariantCulture) : m + ":" + s.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
