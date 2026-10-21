// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (BC03b): the join path through the game's own flow (design F2 to F4, section 4.1), behavior only from
// AutoConnector 1.0.0's README; no AutoConnector code read. Valheim's join flow was read with ilspycmd for reading only.
using System;
using ExtraPotions.Beacon.Config;
using ExtraPotions.Beacon.Integration;
using ExtraPotions.Beacon.Joining;
using ExtraPotions.Beacon.Localization;
using ExtraPotions.Beacon.Secrets;
using ExtraPotions.Beacon.Servers;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Beacon.Runtime
{
    /// <summary>
    /// Follows every join the game starts (its server list, an invite, a launch option or Beacon itself) and answers the
    /// game's password request with the stored password. The password goes in through the game's own launch-password path
    /// (<c>FejdStartup.ServerPassword</c>) for the length of the handshake only, then that path is cleared. A password the
    /// player types is held only until the server accepts or rejects it, and is tracked by the redactor while held. A
    /// password never reaches a log, the console or a file other than the secret store. Main thread only.
    /// </summary>
    public static partial class BeaconJoin
    {
        static readonly JoinCoordinator coordinator = new JoinCoordinator();
        static string? sentStored;
        static string? typed;
        static bool injected;
        static bool suppressCapture;
        static ServerAddress? rejoinForPassword;

        internal static JoinCoordinator Coordinator => coordinator;

        /// <summary>The join module runs: Beacon is ready and AutoConnector is not installed.</summary>
        public static bool Active => BeaconModules.Active(BeaconModule.Join);

        /// <summary>A join Beacon started or followed is waiting for the server.</summary>
        public static bool Joining => coordinator.AwaitingConnection;

        /// <summary>Called as the game starts a join with its join data.</summary>
        internal static void OnJoinServer(ServerJoinData data)
        {
            if (!Active || Plugin.Settings == null) return;
            var target = TargetOf(data);
            if (target != coordinator.Target) ClearRead();
            var settings = Plugin.Settings;
            coordinator.OnJoinStarted(target, BeaconServers.List, e => BeaconSecrets.Store is IAsyncSecretStore || BeaconSecrets.Store.Contains(e.SecretRef),
                new JoinOptions(settings.Get(BeaconSettings.General.SkipPasswordPrompt), settings.Get(BeaconSettings.General.OfferToSavePasswords)));
            JoinStarted?.Invoke(target);
        }

        /// <summary>Raised with the server of every join the game starts (null when its join data has no Beacon form).</summary>
        internal static event Action<ServerAddress?>? JoinStarted;

        /// <summary>Raised when the game is back at the main menu after a Beacon-seen join.</summary>
        internal static event Action<MenuOutcome>? ReturnedToMenu;

        /// <summary>Raised once the server accepted the join.</summary>
        internal static event Action<ServerAddress>? Connected;

        /// <summary>Before the game handles the server's handshake.</summary>
        internal static void BeforeHandshake(bool needPassword)
        {
            injected = false;
            suppressCapture = false;
            if (!needPassword || !Active) return;
            if (FejdStartup.ServerPassword != null)
            {
                // The game's own launch password wins; it is never captured as typed.
                suppressCapture = true;
                return;
            }
            if (coordinator.OnPasswordRequested() != JoinAction.SendStored) return;
            var entry = coordinator.Entry;
            var store = BeaconSecrets.Store;
            var secret = resumedSecret;
            var found = resumedSecret != null || (!(store is IAsyncSecretStore) && entry != null && store.TryRead(entry.SecretRef, out secret));
            if (entry == null || !found || string.IsNullOrEmpty(secret))
            {
                coordinator.OnStoredUnreadable();
                Report(store.LastFailure == SecretFailure.StoredElsewhere && entry != null
                    ? BeaconText.Format(BeaconTextKeys.SecretStoredElsewhere, entry.Name)
                    : BeaconText.Text(BeaconTextKeys.SecretStoreFailed));
                return;
            }
            BeaconSecrets.Redactor.Track(secret);
            sentStored = secret;
            suppressCapture = true;
            injected = true;
            FejdStartup.ServerPassword = secret;
        }

        /// <summary>After the handshake, also when it threw: the launch-password path is cleared again.</summary>
        internal static void AfterHandshake()
        {
            if (injected) FejdStartup.ServerPassword = null;
            injected = false;
            suppressCapture = false;
        }

        /// <summary>The game's password dialog submitted <paramref name="password"/>.</summary>
        internal static void BeforePasswordEntered(string password)
        {
            if (suppressCapture || !Active || string.IsNullOrEmpty(password) || coordinator.Target == null) return;
            ForgetTyped();
            typed = password;
            BeaconSecrets.Redactor.Track(password);
            coordinator.OnPlayerTyped();
        }

        /// <summary>The game shows the main menu's connection result (start of the menu scene, or a join that never left it).</summary>
        internal static void OnMenu(GameStatus status)
        {
            ResetAsyncSecrets();
            AfterHandshake();
            ForgetSecrets();
            var outcome = coordinator.OnReturnedToMenu(status);
            if (outcome.Target == null) return;
            if (outcome.Step == MenuStep.RejoinForPassword) rejoinForPassword = outcome.Target;
            ReturnedToMenu?.Invoke(outcome);
        }

        public static void Tick(MeadworksLog log)
        {
            if (!Active) { ResumeWithoutSecret(); ResetAsyncSecrets(); return; }
            TickHandshake();
            if (coordinator.AwaitingConnection && ZNet.instance != null && !ZNet.instance.IsServer()
                && ZNet.GetConnectionStatus() == ZNet.ConnectionStatus.Connected)
                Accept(log);
            if (rejoinForPassword != null && BeaconMenu.Ready)
            {
                var target = rejoinForPassword;
                rejoinForPassword = null;
                BeaconMenu.Join(target, null);
            }
        }

        static void Accept(MeadworksLog log)
        {
            var target = coordinator.Target!;
            var entry = coordinator.Entry;
            var action = coordinator.OnConnected();
            if (action == JoinAction.StoreTyped && entry != null && typed != null)
            {
                if (BeaconSecrets.Store is IAsyncSecretStore) SaveTypedAsync(entry.SecretRef, typed, entry.Name);
                else if (BeaconSecrets.Store.Store(entry.SecretRef, typed)) log.Info(BeaconSecrets.Redactor.Clean(BeaconText.Format(BeaconTextKeys.PasswordSaved, entry.Name)));
                else Report(BeaconText.Text(BeaconTextKeys.SecretStoreFailed));
            }
            var offered = action == JoinAction.OfferSave ? typed : null;
            ForgetSecrets();
            if (!string.IsNullOrEmpty(offered)) BeaconSaveOffer.Offer(target, offered!);
            Connected?.Invoke(target);
        }

        /// <summary>Beacon's form of the game's join data; null for a kind Beacon does not save.</summary>
        public static ServerAddress? TargetOf(ServerJoinData data)
        {
            switch (data.m_type)
            {
                case ServerJoinDataType.Dedicated: return JoinTargets.FromHost(data.Dedicated.m_host, data.Dedicated.m_port);
                case ServerJoinDataType.SteamUser: return JoinTargets.FromSteam(data.SteamUser.m_joinUserID.m_SteamID);
                case ServerJoinDataType.PlayFabUser: return JoinTargets.FromPlayFab(data.PlayFabUser.m_remotePlayerId);
                default: return null;
            }
        }

        /// <summary>The game's join data for a Beacon address.</summary>
        public static bool TryJoinData(ServerAddress address, out ServerJoinData data)
        {
            data = ServerJoinData.None;
            switch (address.Kind)
            {
                case AddressKind.Host:
                    data = new ServerJoinData(new ServerJoinDataDedicated(address.Host, (ushort)address.Port));
                    return data.IsValid;
                case AddressKind.Steam:
                    if (!JoinTargets.TryGetSteamId(address, out var id)) return false;
                    data = new ServerJoinData(new ServerJoinDataSteamUser(id));
                    return data.IsValid;
                case AddressKind.PlayFab:
                    data = new ServerJoinData(new ServerJoinDataPlayFabUser(address.Host));
                    return data.IsValid;
                default:
                    return false;
            }
        }

        static void Report(string text) => Plugin.Log?.Warning(BeaconSecrets.Redactor.Clean(text));

        static void ForgetTyped()
        {
            BeaconSecrets.Redactor.Untrack(typed);
            typed = null;
        }

        static void ForgetSecrets()
        {
            ForgetTyped();
            BeaconSecrets.Redactor.Untrack(sentStored);
            sentStored = null;
        }

        public static void Reset()
        {
            ResetAsyncSecrets();
            AfterHandshake();
            ForgetSecrets();
            coordinator.Reset();
            rejoinForPassword = null;
            JoinStarted = null;
            ReturnedToMenu = null;
            Connected = null;
        }
    }
}
