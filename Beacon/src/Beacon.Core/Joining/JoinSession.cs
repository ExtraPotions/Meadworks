// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (BC03b): one join through the game's own flow, from its join button to the main menu again. Sending the
// stored password and re-asking after a rejection follow AutoConnector 1.0.0's README (behavior only, no code read).
using System;
using System.Collections.Generic;
using System.Globalization;
using ExtraPotions.Beacon.Servers;

namespace ExtraPotions.Beacon.Joining
{
    /// <summary>
    /// The game's connection states by name. The plugin maps the game's value through its name, so a renumbered game enum
    /// still maps and a new name becomes <see cref="Unknown"/>.
    /// </summary>
    public enum GameStatus
    {
        None, Connecting, Connected, ErrorVersion, ErrorDisconnected, ErrorConnectFailed, ErrorPassword, ErrorAlreadyConnected,
        ErrorBanned, ErrorFull, ErrorPlatformExcluded, ErrorCrossplayPrivilege, ErrorKicked, Unknown,
    }

    public static class ConnectionOutcomes
    {
        public static GameStatus Parse(string? name)
        {
            var text = (name ?? "").Trim();
            if (text.Length == 0 || !char.IsLetter(text[0])) return GameStatus.Unknown;
            return Enum.TryParse(text, false, out GameStatus status) ? status : GameStatus.Unknown;
        }

        /// <summary>
        /// What ended a connection, read when the game is back at the main menu. A status that is still Connected there means
        /// the player logged out; a lost connection counts as unexpected only after the game had connected.
        /// </summary>
        public static DisconnectKind Classify(GameStatus status, bool wasConnected) => status switch
        {
            GameStatus.Connected => DisconnectKind.PlayerQuit,
            GameStatus.ErrorPassword => DisconnectKind.WrongPassword,
            GameStatus.ErrorKicked => DisconnectKind.Kicked,
            GameStatus.ErrorBanned => DisconnectKind.Banned,
            GameStatus.ErrorVersion => DisconnectKind.VersionMismatch,
            GameStatus.ErrorFull => DisconnectKind.ServerFull,
            GameStatus.ErrorAlreadyConnected => DisconnectKind.Refused,
            GameStatus.ErrorPlatformExcluded => DisconnectKind.Refused,
            GameStatus.ErrorCrossplayPrivilege => DisconnectKind.Refused,
            GameStatus.ErrorDisconnected => wasConnected ? DisconnectKind.Unexpected : DisconnectKind.ConnectFailed,
            GameStatus.ErrorConnectFailed => wasConnected ? DisconnectKind.Unexpected : DisconnectKind.ConnectFailed,
            _ => DisconnectKind.Unknown,
        };
    }

    /// <summary>The game's join data as Beacon addresses (design F5, F6).</summary>
    public static class JoinTargets
    {
        /// <summary>A dedicated server's host and port; null for an empty host, a host that already carries a port, or a port outside 1..65535.</summary>
        public static ServerAddress? FromHost(string? host, int port)
        {
            var h = (host ?? "").Trim();
            if (h.Length == 0 || port < 1 || port > 65535) return null;
            var colons = 0;
            foreach (var c in h) if (c == ':') colons++;
            if (colons == 1) return null;
            var p = port.ToString(CultureInfo.InvariantCulture);
            var text = colons > 1 && h[0] != '[' ? "[" + h + "]:" + p : h + ":" + p;
            return ServerAddress.TryParse(text, out var address) ? address : null;
        }

        public static ServerAddress? FromSteam(ulong id) =>
            id != 0 && ServerAddress.TryParse("steam:" + id.ToString(CultureInfo.InvariantCulture), out var address) ? address : null;

        public static ServerAddress? FromPlayFab(string? id) =>
            ServerAddress.TryParse("playfab:" + (id ?? "").Trim(), out var address) ? address : null;

        public static bool TryGetSteamId(ServerAddress address, out ulong id)
        {
            id = 0;
            return address != null && address.Kind == AddressKind.Steam
                && ulong.TryParse(address.Host, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id != 0;
        }
    }

    public readonly struct JoinOptions
    {
        public JoinOptions(bool skipPrompt, bool offerToSave)
        {
            SkipPrompt = skipPrompt;
            OfferToSave = offerToSave;
        }

        /// <summary>Setting Skip Password Prompt.</summary>
        public bool SkipPrompt { get; }

        /// <summary>Setting Offer To Save Passwords.</summary>
        public bool OfferToSave { get; }
    }

    public enum MenuStep
    {
        None,
        /// <summary>The stored password was rejected: join the same server again so the game asks the player (F4).</summary>
        RejoinForPassword,
    }

    public readonly struct MenuOutcome
    {
        public MenuOutcome(DisconnectKind kind, ServerAddress? target, MenuStep step, bool wasConnected)
        {
            Kind = kind;
            Target = target;
            Step = step;
            WasConnected = wasConnected;
        }

        public DisconnectKind Kind { get; }

        /// <summary>The server of the join that ended; null when the menu did not follow a Beacon-seen join.</summary>
        public ServerAddress? Target { get; }

        public MenuStep Step { get; }

        public bool WasConnected { get; }
    }

    /// <summary>
    /// Follows one join: the game's join button (any server, saved or not), its password request, the accepted connection and
    /// the return to the main menu. It never holds a password; the plugin acts on the returned <see cref="JoinAction"/>.
    /// </summary>
    public sealed class JoinCoordinator
    {
        readonly HashSet<ServerAddress> offered = new HashSet<ServerAddress>();
        JoinAttempt? attempt;
        ServerAddress? rejoinForPassword;
        bool requested;

        public ServerAddress? Target { get; private set; }

        public ServerEntry? Entry => attempt?.Entry;

        public bool Connected { get; private set; }

        public bool AwaitingConnection => Target != null && attempt != null && !Connected;

        public void OnJoinStarted(ServerAddress? target, ServerList list, Func<ServerEntry, bool> hasStored, JoinOptions options)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (hasStored == null) throw new ArgumentNullException(nameof(hasStored));
            // The game calls its join again after a login popup: the same unused attempt stays.
            if (target != null && target == Target && attempt != null && !requested && !Connected) return;
            Target = target;
            Connected = false;
            requested = false;
            if (target == null)
            {
                attempt = null;
                rejoinForPassword = null;
                return;
            }
            var entry = list.Find(target);
            var next = new JoinAttempt(entry, entry != null && hasStored(entry), options.SkipPrompt, options.OfferToSave, offered.Contains(target));
            if (rejoinForPassword == target) next = next.AfterRejection();
            rejoinForPassword = null;
            attempt = next;
        }

        public JoinAction OnPasswordRequested()
        {
            if (attempt == null || Connected) return JoinAction.None;
            requested = true;
            return attempt.OnPasswordRequested();
        }

        /// <summary>The stored password could not be read: the game's prompt shows instead, and a typed password replaces it once accepted.</summary>
        public void OnStoredUnreadable()
        {
            if (attempt != null) attempt = attempt.AfterRejection();
        }

        public void OnPlayerTyped() => attempt?.OnPlayerTyped();

        public JoinAction OnConnected()
        {
            if (attempt == null || Connected) return JoinAction.None;
            Connected = true;
            var action = attempt.OnAccepted();
            if (action == JoinAction.OfferSave) offered.Add(Target!);
            return action;
        }

        /// <summary>The game is back at the main menu: what ended the join, and whether to join again for the password prompt.</summary>
        public MenuOutcome OnReturnedToMenu(GameStatus status)
        {
            var target = Target;
            var wasConnected = Connected;
            var ended = attempt;
            Target = null;
            attempt = null;
            Connected = false;
            requested = false;
            if (target == null) return new MenuOutcome(DisconnectKind.Unknown, null, MenuStep.None, false);
            var kind = ConnectionOutcomes.Classify(status, wasConnected);
            if (kind == DisconnectKind.WrongPassword && ended != null && ended.OnRejected() == JoinAction.ReconnectAndAsk)
            {
                rejoinForPassword = target;
                return new MenuOutcome(kind, target, MenuStep.RejoinForPassword, wasConnected);
            }
            return new MenuOutcome(kind, target, MenuStep.None, wasConnected);
        }

        public void Reset()
        {
            offered.Clear();
            attempt = null;
            rejoinForPassword = null;
            Target = null;
            Connected = false;
            requested = false;
        }
    }
}
