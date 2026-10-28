// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Thing's authority runtime (design 3.1, A2 to A4, A8, A9, B1 to B4). Runs only where ZNet is the server
// (single player, host, dedicated server). The game's own ZNet methods change bannedlist.txt; Thing's ledger and audit log
// live in BepInEx/config/Meadworks/thing/. Moderation keys on the connection's platform ID (ISocket.GetHostName).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Communication;
using ExtraPotions.Thing.Config;
using ExtraPotions.Thing.Localization;
using ExtraPotions.Thing.Moderation;

namespace ExtraPotions.Thing.Runtime
{
    public static class ThingPaths
    {
        public static string DataFolder => Path.Combine(Paths.ConfigPath, "Meadworks", "thing");
        public static string LedgerFile => Path.Combine(DataFolder, "ledger.txt");
        public static string AnnouncementsFile => Path.Combine(DataFolder, ThingSettings.AnnouncementsFile);
        public static string PermissionsFile => Path.Combine(DataFolder, "permissions.txt");
    }

    /// <summary>An online player on the server: platform ID and name, and the peer.</summary>
    public readonly struct OnlinePlayer
    {
        public OnlinePlayer(Actor actor, ZNetPeer peer)
        {
            Actor = actor;
            Peer = peer;
        }

        public Actor Actor { get; }
        public ZNetPeer Peer { get; }
    }

    internal enum MuteWriteResult { Refused, Saved, LiveOnly }
    internal readonly struct ModerationWriteResult
    {
        internal ModerationWriteResult(LedgerChange change, bool saved) { Change = change; Saved = saved; }
        internal LedgerChange Change { get; }
        internal bool Saved { get; }
    }

    public static class ThingServer
    {
        const float KickDelaySeconds = 1.5f;
        const double AuditPruneSeconds = 3600;
        static readonly Actor System = new Actor("thing", "Thing");

        static MeadworksLog? log;
        static ZNet? net;
        static ModerationLedger ledger = new ModerationLedger();
        static IReadOnlyList<Announcement> announcements = Array.Empty<Announcement>();
        static readonly ShutdownCountdown countdown = new ShutdownCountdown();
        static readonly HashSet<long> greeted = new HashSet<long>();
        static readonly List<(long Uid, double At)> pendingKicks = new List<(long, double)>();
        static readonly PlayerFreezeSet frozen = new PlayerFreezeSet();
        static readonly Dictionary<string, ZNetPeer> freezeConnections = new Dictionary<string, ZNetPeer>(StringComparer.Ordinal);
        static readonly Dictionary<long, (PlayerFreezeUpdate Update, Actor Admin, Actor Target, double At)> pendingFreezes
            = new Dictionary<long, (PlayerFreezeUpdate, Actor, Actor, double)>();
        static readonly Stopwatch clock = new Stopwatch();
        static double lastSecond = -1;
        static double lastAnnouncement;
        static double nextPrune;

        /// <summary>Thing's authority runtime is loaded for the current world.</summary>
        public static bool Active => net != null;
        internal static bool CurrentAuthority => net != null && ReferenceEquals(net, ZNet.instance) && net.IsServer();

        public static ModerationLedger Ledger => ledger;

        public static void Initialize(MeadworksLog logger) { log = logger; ThingMessages.Initialize(logger); }

        public static void Tick()
        {
            var current = ZNet.instance;
            if (current == null || !current.IsServer() || !ThingModules.ProductActive(out _))
            {
                if (net != null) Unload();
                return;
            }
            if (!ReferenceEquals(current, net)) Load(current);
            ThingMessages.Tick();
            var now = clock.Elapsed.TotalSeconds;
            RunPendingKicks(now);
            if (now - lastSecond < 1) return;
            lastSecond = now;
            try
            {
                LiftExpired();
                SyncFreezes(now);
                Announce(now);
                Countdown(now);
                Greet();
                if (now >= nextPrune) { PruneAudit(); nextPrune = now + AuditPruneSeconds; }
            }
            catch (Exception error)
            {
                log?.Warning("Thing server tick: " + error.GetType().Name + ": " + error.Message);
            }
        }

        public static IReadOnlyList<OnlinePlayer> Online()
        {
            if (net == null) return Array.Empty<OnlinePlayer>();
            return net.GetPeers().Where(p => p != null && p.IsReady() && p.m_socket != null)
                .Select(p => new OnlinePlayer(new Actor(p.m_socket.GetHostName() ?? "", p.m_playerName ?? ""), p))
                .Where(p => p.Actor.Id.Length > 0).ToList();
        }

        public static void Kick(OnlinePlayer target, Actor admin, string reason)
        {
            var text = Reasons.Normalize(reason);
            ThingNotices.Send(target.Peer.m_uid, ThingTextKeys.Kicked, text.Length == 0 ? ThingText.Text(ThingTextKeys.NoReason) : text);
            pendingKicks.Add((target.Peer.m_uid, clock.Elapsed.TotalSeconds + KickDelaySeconds));
            Audit(admin, ThingAction.Kick, target.Actor, AuditOutcome.Done, text);
        }

        /// <summary>Bans by platform ID (online or not); <paramref name="duration"/> null is permanent.</summary>
        public static LedgerChange Ban(Actor target, OnlinePlayer? online, Actor admin, string reason, TimeSpan? duration)
            => BanWithPersistence(target, online, admin, reason, duration).Change;

        internal static ModerationWriteResult BanWithPersistence(Actor target, OnlinePlayer? online, Actor admin, string reason, TimeSpan? duration)
        {
            var text = Reasons.Normalize(reason);
            var change = ledger.Ban(target, admin, text, DateTime.UtcNow, duration);
            net!.Ban(target.Id);
            if (online.HasValue)
            {
                var shown = text.Length == 0 ? ThingText.Text(ThingTextKeys.NoReason) : text;
                if (duration.HasValue) ThingNotices.Send(online.Value.Peer.m_uid, ThingTextKeys.BannedFor, Durations.Describe(duration.Value), shown);
                else ThingNotices.Send(online.Value.Peer.m_uid, ThingTextKeys.Banned, shown);
                pendingKicks.Add((online.Value.Peer.m_uid, clock.Elapsed.TotalSeconds + KickDelaySeconds));
            }
            var saved = SaveLedger();
            Audit(admin, duration.HasValue ? ThingAction.TempBan : ThingAction.Ban, target, saved ? AuditOutcome.Done : AuditOutcome.Failed,
                (duration.HasValue ? Durations.Describe(duration.Value) + ": " : "") + text
                + (saved ? "" : "; native ban changed; ledger persistence uncertain"));
            return new ModerationWriteResult(change, saved);
        }

        public static LedgerChange Unban(string id, Actor admin)
            => UnbanWithPersistence(id, admin).Change;

        internal static ModerationWriteResult UnbanWithPersistence(string id, Actor admin)
        {
            var change = ledger.Unban(id);
            net!.Unban(id);
            var saved = SaveLedger();
            Audit(admin, ThingAction.Unban, new Actor(id, ""), saved ? AuditOutcome.Done : AuditOutcome.Failed,
                (change == LedgerChange.Removed ? "" : "not in Thing's ledger")
                + (saved ? "" : "; native unban changed; ledger persistence uncertain"));
            return new ModerationWriteResult(change, saved);
        }

        public static void Warn(OnlinePlayer target, Actor admin, string reason)
            => WarnWithPersistence(target, admin, reason);

        internal static bool WarnWithPersistence(OnlinePlayer target, Actor admin, string reason)
        {
            var text = Reasons.Normalize(reason);
            ledger.Warn(target.Actor, admin, text, DateTime.UtcNow);
            ThingNotices.Send(target.Peer.m_uid, ThingTextKeys.Warned, text);
            var saved = SaveLedger();
            Audit(admin, ThingAction.Warn, target.Actor, saved ? AuditOutcome.Done : AuditOutcome.Failed,
                text + (saved ? "" : "; warning delivered; ledger persistence uncertain"));
            return saved;
        }

        public static bool Say(Actor admin, string text, bool topLeft = false)
        {
            var sent = CurrentAuthority && MeadworksServerNotices.TryBroadcast(Plugin.Guid, text,
                topLeft ? ServerNoticePlacement.TopLeft : ServerNoticePlacement.Center);
            Audit(admin, ThingAction.Broadcast, default, sent ? AuditOutcome.Done : AuditOutcome.Failed,
                (topLeft ? "top-left: " : "center: ") + text);
            return sent;
        }

        internal static MuteWriteResult Mute(OnlinePlayer target, Actor admin, string reason, TimeSpan? duration)
        {
            if (!CurrentAuthority || !Online().Any(p => ReferenceEquals(p.Peer, target.Peer) && p.Actor.Id == target.Actor.Id)) return MuteWriteResult.Refused;
            ledger.Mute(target.Actor, admin, reason, DateTime.UtcNow, duration);
            var saved = SaveLedger();
            if (duration.HasValue) TellMute(target.Peer.m_uid, ThingTextKeys.MutedFor, Durations.Describe(duration.Value));
            else TellMute(target.Peer.m_uid, ThingTextKeys.MutedUntilLifted);
            Audit(admin, ThingAction.Mute, target.Actor, saved ? AuditOutcome.Done : AuditOutcome.Failed,
                "server-enforced; " + (duration.HasValue ? Durations.Describe(duration.Value) : "until lifted") + "; " + Reasons.Normalize(reason)
                + (saved ? "" : "; live ledger changed; persistence uncertain"));
            return saved ? MuteWriteResult.Saved : MuteWriteResult.LiveOnly;
        }

        /// <summary>Freezes or unfreezes one connected peer for this connection only. Nothing is written to the ledger.</summary>
        internal static bool SetFrozen(OnlinePlayer target, Actor admin, bool on)
        {
            if (!CurrentAuthority || target.Peer == null || target.Peer.m_uid == 0) return false;
            if (!Online().Any(player => ReferenceEquals(player.Peer, target.Peer) && player.Actor.Id == target.Actor.Id)) return false;
            RetainFreezeConnections();
            if (!frozen.TrySet(target.Actor.Id, target.Peer.m_uid, on, out var update)) return false;
            pendingFreezes.Add(update.Revision, (update, admin, target.Actor, clock.Elapsed.TotalSeconds));
            try { ThingPlayerFreeze.Push(update); }
            catch (Exception error) { log?.Warning("Thing freeze notice failed: " + error.GetType().Name); }
            return true;
        }

        internal static void AcknowledgeFreeze(long peer, long revision, bool on)
        {
            if (!CurrentAuthority || !pendingFreezes.TryGetValue(revision, out var pending)) return;
            if (!Online().Any(player => player.Peer.m_uid == peer && player.Actor.Id == pending.Target.Id)) return;
            RetainFreezeConnections();
            if (!frozen.Acknowledge(peer, revision, on)) return;
            pendingFreezes.Remove(revision);
            Audit(pending.Admin, on ? ThingAction.FreezePlayer : ThingAction.UnfreezePlayer, pending.Target, AuditOutcome.Done,
                "client acknowledged; clears on disconnect; local input enforcement");
        }

        static void SyncFreezes(double now)
        {
            RetainFreezeConnections();
            foreach (var pair in pendingFreezes.ToArray())
            {
                var pending = pair.Value;
                if (frozen.IsCurrent(pending.Update) && now - pending.At < 10) continue;
                pendingFreezes.Remove(pair.Key);
                Audit(pending.Admin, pending.Update.Frozen ? ThingAction.FreezePlayer : ThingAction.UnfreezePlayer, pending.Target,
                    AuditOutcome.Failed, "not acknowledged: disconnected, superseded or timed out; current state continues to retry");
            }
            foreach (var update in frozen.Due(now))
                try { ThingPlayerFreeze.Push(update); }
                catch (Exception error) { log?.Warning("Thing freeze retry failed: " + error.GetType().Name); }
        }

        static void RetainFreezeConnections()
        {
            var online = Online();
            foreach (var player in online)
                if (freezeConnections.TryGetValue(player.Actor.Id, out var previous) && !ReferenceEquals(previous, player.Peer))
                    frozen.Disconnect(player.Actor.Id);
            freezeConnections.Clear();
            foreach (var player in online) freezeConnections[player.Actor.Id] = player.Peer;
            frozen.Retain(online.Select(player => new KeyValuePair<string, long>(player.Actor.Id, player.Peer.m_uid)));
        }

        internal static MuteWriteResult Unmute(Actor target, OnlinePlayer? online, Actor admin)
        {
            if (!CurrentAuthority) return MuteWriteResult.Refused;
            ledger.Unmute(target.Id);
            var saved = SaveLedger();
            if (online.HasValue) TellMute(online.Value.Peer.m_uid, ThingTextKeys.Unmuted);
            Audit(admin, ThingAction.Unmute, target, saved ? AuditOutcome.Done : AuditOutcome.Failed,
                saved ? "server-enforced" : "live ledger changed; persistence uncertain");
            return saved ? MuteWriteResult.Saved : MuteWriteResult.LiveOnly;
        }

        static void TellMute(long peer, string key, params object[] values)
        {
            try { ThingNotices.Send(peer, key, values); }
            catch (Exception error) { log?.Warning("Thing mute notice failed: " + error.GetType().Name); }
        }

        public static void StartShutdown(Actor admin, int minutes)
        {
            var warnings = ThingSettings.Communication.ShutdownWarnings.Normalize(Plugin.Settings?.Get(ThingSettings.Communication.ShutdownWarnings) ?? "")
                .Split(',').Where(p => p.Length > 0).Select(int.Parse);
            var start = countdown.Start(minutes, warnings, clock.Elapsed.TotalSeconds);
            ThingNotices.Broadcast(ThingTextKeys.ShutdownWarning, start.MinutesLeft);
            Audit(admin, ThingAction.Shutdown, default, AuditOutcome.Done, minutes + " min");
        }

        public static bool CancelShutdown(Actor admin)
        {
            if (!countdown.Cancel()) return false;
            ThingNotices.Broadcast(ThingTextKeys.ShutdownCancelled);
            Audit(admin, ThingAction.CancelShutdown, default, AuditOutcome.Done, "");
            return true;
        }

        public static void SaveWorld(Actor admin)
        {
            net!.Save(false, true, false);
            Audit(admin, ThingAction.SaveWorld, default, AuditOutcome.Done, "");
        }

        public static void Audit(Actor admin, ThingAction action, Actor target, AuditOutcome outcome, string detail)
        {
            try
            {
                Directory.CreateDirectory(ThingPaths.DataFolder);
                var now = DateTime.UtcNow;
                var entry = new AuditEntry(now, admin.Id, admin.Name, action, target.Id ?? "", target.Name ?? "", outcome, detail);
                File.AppendAllText(Path.Combine(ThingPaths.DataFolder, AuditLog.FileName(now)), entry.ToLine() + "\n");
            }
            catch (Exception error)
            {
                log?.Warning("Thing audit write failed: " + error.GetType().Name + ": " + error.Message);
            }
        }

        internal static void ReloadAnnouncements(IReadOnlyList<Announcement> entries)
        {
            announcements = entries.ToArray();
            lastAnnouncement = clock.Elapsed.TotalSeconds;
        }

        public static void Unload()
        {
            ThingMessages.Reset();
            ThingChatMute.Reset();
            net = null;
            ledger = new ModerationLedger();
            announcements = Array.Empty<Announcement>();
            countdown.Cancel();
            greeted.Clear();
            pendingKicks.Clear();
            frozen.ClearAll();
            freezeConnections.Clear();
            pendingFreezes.Clear();
            clock.Reset();
            ThingAccess.Clear();
        }

        static void Load(ZNet current)
        {
            Unload();
            net = current;
            clock.Restart();
            lastSecond = -1;
            lastAnnouncement = 0;
            nextPrune = 0;
            try
            {
                Directory.CreateDirectory(ThingPaths.DataFolder);
                if (File.Exists(ThingPaths.LedgerFile))
                {
                    ledger = ModerationLedger.Load(File.ReadAllText(ThingPaths.LedgerFile), out var skipped);
                    if (skipped > 0) log?.Warning("Thing ledger: " + skipped + " unreadable lines skipped.");
                }
                if (!File.Exists(ThingPaths.AnnouncementsFile)) File.WriteAllText(ThingPaths.AnnouncementsFile, Announcements.Format(Array.Empty<Announcement>()));
                var parsed = Announcements.Parse(File.ReadAllText(ThingPaths.AnnouncementsFile));
                announcements = parsed.Entries;
                foreach (var problem in parsed.Problems) log?.Warning(ThingSettings.AnnouncementsFile + " " + problem);
                ThingAccess.Bind(current.GetWorldUID());
                log?.Verbose("Thing server data loaded: " + ledger.Bans.Count + " bans, " + ledger.Mutes.Count + " mutes, " + announcements.Count + " announcements.");
            }
            catch (Exception error)
            {
                log?.Warning("Thing server data could not be loaded: " + error.GetType().Name + ": " + error.Message);
            }
        }

        static bool SaveLedger()
        {
            try
            {
                Directory.CreateDirectory(ThingPaths.DataFolder);
                var temp = ThingPaths.LedgerFile + ".tmp";
                var text = ledger.Save();
                File.WriteAllText(temp, text);
                if (File.Exists(ThingPaths.LedgerFile)) File.Replace(temp, ThingPaths.LedgerFile, null);
                else File.Move(temp, ThingPaths.LedgerFile);
                return File.ReadAllText(ThingPaths.LedgerFile) == text;
            }
            catch (Exception error)
            {
                log?.Warning("Thing ledger write failed: " + error.GetType().Name + ": " + error.Message);
                return false;
            }
        }

        static void RunPendingKicks(double now)
        {
            if (pendingKicks.Count == 0 || net == null) return;
            for (var i = pendingKicks.Count - 1; i >= 0; i--)
            {
                if (pendingKicks[i].At > now) continue;
                var peer = net.GetPeer(pendingKicks[i].Uid);
                pendingKicks.RemoveAt(i);
                if (peer != null) net.InternalKick(peer);
            }
        }

        static void LiftExpired()
        {
            var lifted = ledger.TakeExpired(DateTime.UtcNow);
            foreach (var ban in lifted)
            {
                net!.Unban(ban.Target.Id);
                Audit(System, ThingAction.Unban, ban.Target, AuditOutcome.Done, "temporary ban ended");
                log?.Info(ThingText.Format(ThingTextKeys.TempBanLifted, ban.Target.Name.Length > 0 ? ban.Target.Name : ban.Target.Id));
            }
            var unmuted = ledger.TakeExpiredMutes(DateTime.UtcNow);
            foreach (var mute in unmuted)
            {
                var target = Online().FirstOrDefault(p => p.Actor.Id == mute.Target.Id);
                if (target.Peer != null) TellMute(target.Peer.m_uid, ThingTextKeys.Unmuted);
                Audit(System, ThingAction.Unmute, mute.Target, AuditOutcome.Done, "temporary server-enforced mute ended");
            }
            if (lifted.Count > 0 || unmuted.Count > 0) SaveLedger();
        }

        static void Announce(double now)
        {
            if (announcements.Count == 0) { lastAnnouncement = now; return; }
            foreach (var a in Announcements.Due(announcements, lastAnnouncement, now)) ThingNotices.Broadcast("", a.Text);
            lastAnnouncement = now;
        }

        static void Countdown(double now)
        {
            foreach (var e in countdown.Tick(now))
            {
                if (e.Kind == CountdownEventKind.Warn) ThingNotices.Broadcast(ThingTextKeys.ShutdownWarning, e.MinutesLeft);
                else if (e.Kind == CountdownEventKind.Save)
                {
                    ThingNotices.Broadcast(ThingTextKeys.ShutdownSaving);
                    net!.Save(false, true, false);
                    Audit(System, ThingAction.SaveWorld, default, AuditOutcome.Done, "shutdown countdown reached zero");
                }
            }
        }

        static void Greet()
        {
            var motd = MessageOfTheDay.ForJoin(Plugin.Settings?.Get(ThingSettings.Communication.MessageOfTheDay));
            var online = new HashSet<long>();
            foreach (var p in net!.GetPeers())
            {
                if (p == null || !p.IsReady() || p.m_characterID == ZDOID.None) continue;
                online.Add(p.m_uid);
                if (greeted.Add(p.m_uid) && motd != null) ThingNotices.Send(p.m_uid, "", motd);
            }
            greeted.IntersectWith(online);
        }

        static void PruneAudit()
        {
            var days = Plugin.Settings?.Get(ThingSettings.General.AuditLogDays) ?? 30;
            if (!Directory.Exists(ThingPaths.DataFolder)) return;
            foreach (var path in Directory.GetFiles(ThingPaths.DataFolder, "audit-*.log"))
                if (AuditLog.Expired(Path.GetFileName(path), DateTime.UtcNow, days))
                    try { File.Delete(path); } catch (Exception error) { log?.Warning("Thing audit prune: " + error.GetType().Name); }
        }
    }
}
