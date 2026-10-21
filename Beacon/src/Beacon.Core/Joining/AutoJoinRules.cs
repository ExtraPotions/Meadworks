// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: auto-join and reconnect (design F7, F8, section 6).
using System;
using System.Collections.Generic;
using ExtraPotions.Beacon.Servers;

namespace ExtraPotions.Beacon.Joining
{
    public enum AutoJoinSkip { None, Disabled, LaunchConnect, AlreadyRan, NoServer, UnknownServer }

    public readonly struct AutoJoinDecision
    {
        public AutoJoinDecision(AutoJoinSkip skip, ServerEntry? target)
        {
            Skip = skip;
            Target = target;
        }

        public AutoJoinSkip Skip { get; }
        public ServerEntry? Target { get; }
    }

    public static class AutoJoinRules
    {
        /// <summary>
        /// Whether to start the countdown once the main menu has a character (F7): off by default, skipped when the game was
        /// launched with its own <c>+connect</c>, once per launch, toward the named server or else the default one.
        /// </summary>
        public static AutoJoinDecision Decide(bool enabled, string? serverName, ServerList list, bool launchedWithConnect, bool alreadyRan)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (!enabled) return new AutoJoinDecision(AutoJoinSkip.Disabled, null);
            if (launchedWithConnect) return new AutoJoinDecision(AutoJoinSkip.LaunchConnect, null);
            if (alreadyRan) return new AutoJoinDecision(AutoJoinSkip.AlreadyRan, null);
            var name = (serverName ?? "").Trim();
            if (name.Length == 0)
            {
                var d = list.Default;
                return d == null ? new AutoJoinDecision(AutoJoinSkip.NoServer, null) : new AutoJoinDecision(AutoJoinSkip.None, d);
            }
            var named = list.FindByName(name);
            return named == null ? new AutoJoinDecision(AutoJoinSkip.UnknownServer, null) : new AutoJoinDecision(AutoJoinSkip.None, named);
        }

        /// <summary>The game's own join options on its command line, each followed by its value (BC04b).</summary>
        public static bool LaunchJoins(IReadOnlyList<string> args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            for (var i = 0; i < args.Count - 1; i++)
            {
                switch (args[i])
                {
                    case "+connect":
                    case "+connect_lobby":
                    case "-joinserverwithcharacter":
                    case "-joincode":
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The character for an auto-join (setting Auto Join Character): empty keeps the selected one (the first when the
        /// selection is out of range); otherwise the first whose name, then file name, matches ignoring case; -1 when none.
        /// </summary>
        public static int PickCharacter(IReadOnlyList<string> names, IReadOnlyList<string> files, string? wanted, int selected)
        {
            if (names == null) throw new ArgumentNullException(nameof(names));
            if (files == null) throw new ArgumentNullException(nameof(files));
            if (names.Count == 0) return -1;
            var w = (wanted ?? "").Trim();
            if (w.Length == 0) return selected >= 0 && selected < names.Count ? selected : 0;
            for (var i = 0; i < names.Count; i++)
                if (string.Equals((names[i] ?? "").Trim(), w, StringComparison.OrdinalIgnoreCase)) return i;
            for (var i = 0; i < files.Count && i < names.Count; i++)
                if (string.Equals((files[i] ?? "").Trim(), w, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }

    public enum CountdownState { Counting, Join, Cancelled }

    /// <summary>The cancellable countdown before an auto-join (0 to 30 seconds; Escape or pad B cancels).</summary>
    public sealed class AutoJoinCountdown
    {
        public const int MaxSeconds = 30;

        float remaining;
        CountdownState state = CountdownState.Counting;

        public AutoJoinCountdown(int seconds)
        {
            remaining = Math.Max(0, Math.Min(MaxSeconds, seconds));
        }

        public int SecondsShown => (int)Math.Ceiling(Math.Max(0f, remaining));

        public CountdownState State => state;

        public CountdownState Tick(float deltaSeconds)
        {
            if (state != CountdownState.Counting) return state;
            remaining -= Math.Max(0f, deltaSeconds);
            if (remaining <= 0f) state = CountdownState.Join;
            return state;
        }

        public void Cancel()
        {
            if (state == CountdownState.Counting) state = CountdownState.Cancelled;
        }
    }

    /// <summary>
    /// What ended a connection. <see cref="ConnectFailed"/> is a connection that never completed; <see cref="Refused"/> is a
    /// server or platform refusal other than a full server or another version; <see cref="Unknown"/> is a status Beacon does
    /// not know. Only <see cref="Unexpected"/> may reconnect.
    /// </summary>
    public enum DisconnectKind { Unexpected, Kicked, Banned, PlayerQuit, WrongPassword, VersionMismatch, ServerFull, ConnectFailed, Refused, Unknown }

    public enum ReconnectDecision { Reconnect, Off, Locked, NotAfterThis, OutOfAttempts }

    public static class ReconnectRules
    {
        public const int MinAttempts = 1;
        public const int MaxAttempts = 10;

        /// <summary>
        /// Reconnect only after an unexpected disconnect, never after a kick, ban, own quit or refusal; the server may lock the
        /// setting off (section 6); at most the configured number of attempts (1 to 10).
        /// </summary>
        public static ReconnectDecision Decide(bool enabled, bool lockedByServer, DisconnectKind kind, int attemptsSoFar, int maxAttempts)
        {
            if (!enabled) return ReconnectDecision.Off;
            if (lockedByServer) return ReconnectDecision.Locked;
            if (kind != DisconnectKind.Unexpected) return ReconnectDecision.NotAfterThis;
            var limit = Math.Max(MinAttempts, Math.Min(MaxAttempts, maxAttempts));
            return attemptsSoFar >= limit ? ReconnectDecision.OutOfAttempts : ReconnectDecision.Reconnect;
        }

        /// <summary>Wait before attempt <paramref name="attemptIndex"/> (from 0): 5, 10, 20, 40, then 60 seconds.</summary>
        public static int DelaySeconds(int attemptIndex) =>
            attemptIndex <= 0 ? 5 : attemptIndex >= 4 ? 60 : 5 << attemptIndex;
    }
}
