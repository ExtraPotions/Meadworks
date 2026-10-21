// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (BC04b): auto-join with a cancellable countdown and reconnect with limits (design F7, F8, section 6).
using System;
using BepInEx.Configuration;
using ExtraPotions.Beacon.Config;
using ExtraPotions.Beacon.Joining;
using ExtraPotions.Beacon.Localization;
using ExtraPotions.Beacon.Servers;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Beacon.Runtime
{
    /// <summary>
    /// Auto-join: once per launch, when the main menu is idle with a character, toward the named or default saved server,
    /// after a countdown that Escape, pad B or <c>beacon cancel</c> stops; skipped when the game was launched with its own
    /// join option. Reconnect: after an unexpected disconnect only, never after a kick, ban, own logout or refusal, at most
    /// Reconnect Attempts times with growing delays, each through the game's own join. An authenticated server may deny
    /// automatic reconnect without changing the player's preference; that decision survives disconnect restoration.
    /// The countdown and reconnect wait use the existing kit center panel. Main thread only.
    /// </summary>
    public static class BeaconAutoJoin
    {
        static readonly ReconnectSession reconnect = new ReconnectSession();
        static AutoJoinCountdown? countdown;
        static ServerEntry? countdownServer;
        static int countdownCharacter = -1;
        static bool autoJoinDecided;
        static bool subscribed;
        static int reconnectAttempt;
        static int reconnectLimit;

        static double Now => Time.realtimeSinceStartup;

        public static void Initialize()
        {
            if (subscribed) return;
            BeaconJoin.JoinStarted += OnJoinStarted;
            BeaconJoin.ReturnedToMenu += OnMenu;
            BeaconJoin.Connected += OnConnected;
            subscribed = true;
        }

        /// <summary>Stops a running countdown or reconnect cycle and returns the text said; null when there was none.</summary>
        public static string? Cancel(MeadworksLog? log)
        {
            autoJoinDecided = true;
            string? text = null;
            if (countdown != null)
            {
                countdown.Cancel();
                countdown = null;
                countdownServer = null;
                text = BeaconText.Text(BeaconTextKeys.AutoJoinCancelled);
            }
            else if (reconnect.Cancel()) text = BeaconText.Text(BeaconTextKeys.ReconnectCancelled);
            if (text != null) log?.Info(text);
            BeaconJoinCard.Remove();
            return text;
        }

        /// <summary>A countdown or reconnect wait is showing; keyboard Escape reaches it through BeaconMenuBack.</summary>
        public static bool Waiting => countdown != null || reconnect.Pending;

        public static void Tick(MeadworksLog log)
        {
            if (!BeaconJoin.Active || Plugin.Settings == null) return;
            Plugin.Settings.RefreshReconnectPolicy();
            if ((countdown != null || reconnect.Pending) && CancelPressed()) Cancel(log);
            if (BeaconImportSecrets.Busy) return;
            if (reconnect.Pending && BeaconMenu.Ready && reconnect.Tick(Now))
            {
                var target = reconnect.Target;
                if (target == null || !BeaconMenu.Join(target, null)) reconnect.Cancel();
            }
            if (!autoJoinDecided && BeaconMenu.Ready && !BeaconJoin.Joining && !reconnect.Pending) StartCountdown(log);
            if (countdown != null) TickCountdown();
            if (countdown != null && countdownServer != null) BeaconJoinCard.Show(JoinCard.Countdown(countdownServer.Name, countdown.SecondsShown));
            else if (reconnect.Pending) BeaconJoinCard.Show(JoinCard.Reconnect(reconnectAttempt, reconnectLimit, reconnect.SecondsLeft(Now)));
            else BeaconJoinCard.Remove();
        }

        static void StartCountdown(MeadworksLog log)
        {
            autoJoinDecided = true;
            var settings = Plugin.Settings!;
            var server = settings.Get(BeaconSettings.AutoJoin.Server);
            var decision = AutoJoinRules.Decide(settings.Get(BeaconSettings.AutoJoin.Enabled), server, BeaconServers.List,
                AutoJoinRules.LaunchJoins(Environment.GetCommandLineArgs()), alreadyRan: false);
            if (decision.Skip == AutoJoinSkip.UnknownServer)
            {
                log.Info(BeaconText.Format(BeaconTextKeys.AutoJoinUnknownServer, server.Trim()));
                return;
            }
            if (decision.Skip != AutoJoinSkip.None || decision.Target == null)
            {
                if (decision.Skip != AutoJoinSkip.Disabled) log.Verbose("Beacon auto-join skipped: " + decision.Skip + ".");
                return;
            }
            var wanted = settings.Get(BeaconSettings.AutoJoin.Character);
            var (names, files, selected) = BeaconMenu.Characters();
            var character = AutoJoinRules.PickCharacter(names, files, wanted, selected);
            if (character < 0)
            {
                log.Info(BeaconText.Format(BeaconTextKeys.AutoJoinUnknownCharacter, wanted.Trim()));
                return;
            }
            var seconds = settings.Get(BeaconSettings.AutoJoin.CountdownSeconds);
            countdown = new AutoJoinCountdown(seconds);
            countdownServer = decision.Target;
            countdownCharacter = character;
            log.Info(BeaconText.Format(BeaconTextKeys.AutoJoinCountdown, decision.Target.Name, countdown.SecondsShown));
        }

        static void TickCountdown()
        {
            if (countdown!.Tick(Time.unscaledDeltaTime) != CountdownState.Join) return;
            var server = countdownServer;
            var character = countdownCharacter;
            countdown = null;
            countdownServer = null;
            if (server != null) BeaconMenu.Join(server.Address, character);
        }

        static bool CancelPressed()
        {
            if (MeadworksInput.Typing) return false;
            if (!MeadworksInput.PadDown(PadButton.B)) return false;
            MeadworksInput.PadConsume(PadButton.B); return true;
        }

        static void OnJoinStarted(ServerAddress? target)
        {
            Plugin.Settings?.BeginReconnectConnection(target);
            // Any join, the player's or Beacon's, settles auto-join for this launch.
            autoJoinDecided = true;
            countdown = null;
            countdownServer = null;
            BeaconJoinCard.Remove();
            reconnect.OnJoinStarted(target);
        }

        static void OnConnected(ServerAddress target) => reconnect.OnConnected(Now);

        static void OnMenu(MenuOutcome outcome)
        {
            if (outcome.Target == null || Plugin.Settings == null) return;
            if (outcome.Step == MenuStep.RejoinForPassword)
            {
                reconnect.Cancel();
                return;
            }
            var settings = Plugin.Settings;
            settings.RefreshReconnectPolicy();
            var plan = reconnect.OnDisconnected(outcome.Target, outcome.Kind, outcome.WasConnected,
                settings.Get(BeaconSettings.Reconnect.Enabled), settings.ReconnectPolicy.LockedFor(outcome.Target), settings.Get(BeaconSettings.Reconnect.Attempts), Now);
            var log = Plugin.Log;
            switch (plan.Step)
            {
                case ReconnectStep.Scheduled:
                    reconnectAttempt = plan.Attempt;
                    reconnectLimit = plan.Limit;
                    log?.Info(BeaconText.Format(BeaconTextKeys.Reconnecting, plan.Attempt, plan.Limit));
                    break;
                case ReconnectStep.Stopped when plan.Decision == ReconnectDecision.OutOfAttempts:
                    log?.Info(BeaconText.Format(BeaconTextKeys.ReconnectStopped, plan.Limit));
                    break;
                case ReconnectStep.Locked:
                    log?.Info(BeaconText.Text(BeaconTextKeys.ReconnectLocked));
                    break;
            }
        }

        public static void Reset()
        {
            if (subscribed)
            {
                BeaconJoin.JoinStarted -= OnJoinStarted;
                BeaconJoin.ReturnedToMenu -= OnMenu;
                BeaconJoin.Connected -= OnConnected;
                subscribed = false;
            }
            reconnect.Cancel();
            BeaconJoinCard.Remove();
            countdown = null;
            countdownServer = null;
            countdownCharacter = -1;
            autoJoinDecided = false;
        }
    }
}
