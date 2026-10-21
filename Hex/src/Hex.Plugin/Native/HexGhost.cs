// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: server-confirmed Ghost Mode (docs/ghost-authority.md) over existing Crucible paths only: the
// meadworks.hex.ghost operation (request), C13 MeadworksMischief.DecideForPeer (decision) and the meadworks.hex.ghost.set
// peer command (published set). Every Hex game honors the confirmed set for every player through Player.InGhostMode.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using ExtraPotions.Hex.Ghost;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexGhost
{
    internal const string OperationId = "meadworks.hex.ghost", SetCommandId = "meadworks.hex.ghost.set";
    const string HostPeer = "host";
    const double SweepSeconds = 0.25;
    static readonly GhostRegistry registry = new();
    static readonly GhostPublisher publisher = new();
    static readonly GhostView view = new();
    static readonly GhostRequester requester = new();
    static readonly GhostVanillaGrace vanillaGrace = new();
    static readonly Dictionary<string, (long Revision, Task<OperationResult<bool>> Task)> pushes = new(StringComparer.Ordinal);
    static readonly List<string> peerIds = new(), settled = new();
    static IDisposable? operation, command;
    static Task<OperationResult<GhostAnswer>>? request;
    static MeadworksLog? log;
    static long serverGeneration = -1, clientGeneration = -1, pushCounter;
    static double nextSweep;

    sealed class RequestCodec : IOperationCodec<(GhostVerb Verb, GhostKey Key), GhostAnswer>
    {
        public byte[] EncodeRequest((GhostVerb Verb, GhostKey Key) request) => GhostWire.EncodeRequest(request.Verb, request.Key);
        public (GhostVerb Verb, GhostKey Key) DecodeRequest(byte[] bytes) => GhostWire.DecodeRequest(bytes);
        public byte[] EncodeReply(GhostAnswer reply) => GhostWire.EncodeAnswer(reply);
        public GhostAnswer DecodeReply(byte[] bytes) => GhostWire.DecodeAnswer(bytes);
    }
    sealed class SetCodec : IOperationCodec<GhostSet, bool>
    {
        public byte[] EncodeRequest(GhostSet set) => GhostWire.EncodeSet(set.Revision, set.Keys.ToArray());
        public GhostSet DecodeRequest(byte[] bytes) => GhostWire.DecodeSet(bytes);
        public byte[] EncodeReply(bool reply) => GhostWire.EncodeAck();
        public bool DecodeReply(byte[] bytes) => GhostWire.DecodeAck(bytes);
    }

    internal static void Initialize(ProductRegistration registration, ManualLogSource logger)
    {
        log = MeadworksLog.For(logger, HexCore.ProductName);
        operation = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(OperationId, new System.Version(1, 0)), new RequestCodec(), Decide);
        command = CruciblePlugin.Services.PeerCommands.Register(registration, new OperationDescriptor(SetCommandId, new System.Version(1, 0)), new SetCodec(), Receive);
    }

    internal static void Shutdown()
    {
        operation?.Dispose(); operation = null;
        command?.Dispose(); command = null;
        ResetServer(); ResetClient();
        log = null;
    }

    /// <summary>The Player.InGhostMode addition: the confirmed state for any player, not only the local one.</summary>
    internal static bool Honours(Player player)
    {
        if (player == null) return false;
        var net = ZNet.instance;
        var server = net != null && net.IsServer();
        if (player == Player.m_localPlayer)
        {
            // The authority evaluates its own player live by the same rule it records; a client also needs the server's confirmation.
            if (!HexMovement.On(player, "ghost_mode")) return false;
            return server ? !VanillaBlocked() : view.Contains(Key(player));
        }
        if (server ? registry.Count == 0 : view.Count == 0) return false;
        var key = Key(player);
        return server ? registry.Contains(key) : view.Contains(key);
    }

    /// <summary>Called every frame while Hex is active: server sweep and publish, or client request pacing.</summary>
    internal static void Tick()
    {
        if (operation == null) return;
        var now = Time.realtimeSinceStartupAsDouble;
        var net = ZNet.instance;
        var session = CruciblePlugin.Session;
        if (net != null && net.IsServer()) { ResetClient(); ServerTick(now, net, session); }
        else { ResetServer(); ClientTick(now, net, session); }
    }

    static void ServerTick(double now, ZNet net, SessionSnapshot session)
    {
        if (!session.AuthorityReady || !session.IsAuthority) { ResetServer(); return; }
        if (session.Generation != serverGeneration) { ResetServer(); serverGeneration = session.Generation; }
        var local = Player.m_localPlayer;
        var localKey = Key(local);
        var blocked = VanillaBlocked();
        if (!blocked && !localKey.IsNone && HexMovement.On(local, "ghost_mode")) { if (registry.CanAdmit(HostPeer)) registry.Grant(HostPeer, localKey); }
        else registry.Revoke(HostPeer);
        if (now >= nextSweep)
        {
            nextSweep = now + SweepSeconds;
            var refused = new HashSet<string>(StringComparer.Ordinal);
            foreach (var peer in registry.RetainWhere((id, key) => id == HostPeer || Valid(net, id, key, blocked, refused)))
            {
                log?.Debug("Ghost Mode confirmation revoked for peer " + peer + ".");
                // Policy or vanilla-setting revocations tell the player; disconnects and character changes need no notice.
                if (refused.Contains(peer) && long.TryParse(peer, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid)) Notify(uid, GhostNotices.RevokedKey);
            }
        }
        Publish(now, net);
    }

    static bool Valid(ZNet net, string id, GhostKey key, bool blocked, HashSet<string> refused)
    {
        var peer = FindPeer(net, id);
        if (peer == null || peer.m_rpc == null || !peer.m_rpc.IsConnected() || Key(peer.m_characterID) != key) return false;
        if (!blocked && MeadworksMischief.DecideForPeer(peer.m_rpc, MischiefCategory.Personal).Allowed) return true;
        refused.Add(id);
        return false;
    }

    static bool VanillaBlocked()
    {
        var policy = Plugin.Settings?.GhostWithVanilla.Value ?? GhostVanillaRule.Default;
        var known = TryReadRefusalPeerPresence(out var any);
        return vanillaGrace.Blocks(policy, known, any, Time.realtimeSinceStartupAsDouble);
    }

    // Hex is client-optional: a Meadworks player without Hex runs creatures that never receive the ghost set, like a vanilla player.
    static readonly System.Version PeerProtocol = new(1, 0);
    static bool TryReadRefusalPeerPresence(out bool any)
    {
        var net = ZNet.instance;
        Func<long, int>? kindProvider;
        try { if (!CruciblePlugin.Services.Capabilities.TryResolve(PeerKindContract.Id, PeerKindContract.ContractVersion, out kindProvider)) kindProvider = null; }
        catch { kindProvider = null; }
        if (net != null && net.IsServer() && kindProvider != null)
        {
            var peers = CruciblePlugin.Services.Sessions.Peers;
            foreach (var peer in net.GetPeers())
            {
                if (peer == null || !peer.IsReady()) continue;
                var kind = PeerKindContract.Read(kindProvider, peer.m_uid);
                var pending = kind == PeerKindContract.Pending;
                var crucible = kind == PeerKindContract.Crucible;
                var hasHex = crucible && peers.Resolve(peer.m_uid.ToString(CultureInfo.InvariantCulture), Plugin.Guid, PeerProtocol);
                if (GhostVanillaRule.BlocksForPeer(pending, crucible, hasHex)) { any = true; return true; }
            }
            any = false;
            return true;
        }

        Func<int>? provider;
        try { if (!CruciblePlugin.Services.Capabilities.TryResolve(VanillaPeersContract.Id, VanillaPeersContract.ContractVersion, out provider)) provider = null; }
        catch { provider = null; }
        var known = VanillaPeersContract.TryReadAny(provider, out any);
        if (net != null && net.IsServer())
        {
            var peers = CruciblePlugin.Services.Sessions.Peers;
            any |= net.GetPeers().Any(peer => peer != null && peer.IsReady() && !peers.Resolve(peer.m_uid.ToString(CultureInfo.InvariantCulture), Plugin.Guid, PeerProtocol));
        }
        return known;
    }

    // Pending handshakes are excluded; Unknown (including a disabled/missing peer-class contract) remains conservative.

    // Server text in the server's own language, through Crucible's 0.3.8 server notice API (literal text, no client key).
    static void Notify(long peer, string key)
    {
        if (!MeadworksServerNotices.TrySend(Plugin.Guid, peer, HexText.Text(key), ServerNoticePlacement.Center))
            log?.Debug("Ghost Mode notice to peer " + peer + " was not routed.");
    }

    static void Publish(double now, ZNet net)
    {
        // Runs every frame: the lists are reused, no LINQ.
        var ids = peerIds; ids.Clear();
        foreach (var p in net.GetPeers()) if (p != null && p.IsReady()) ids.Add(p.m_uid.ToString(CultureInfo.InvariantCulture));
        publisher.Retain(ids);
        if (pushes.Count > 0)
        {
            settled.Clear();
            foreach (var key in pushes.Keys) if (!ids.Contains(key)) settled.Add(key);
            foreach (var gone in settled) pushes.Remove(gone);
            settled.Clear();
            foreach (var pair in pushes) if (pair.Value.Task.IsCompleted) settled.Add(pair.Key);
            foreach (var key in settled)
            {
                var push = pushes[key];
                pushes.Remove(key);
                var task = push.Task;
                if (task.Status == TaskStatus.RanToCompletion && task.Result.State == OperationState.Succeeded && task.Result.Reply) publisher.Delivered(key, push.Revision);
                else publisher.Failed(key, now);
            }
        }
        GhostSet? snapshot = null;
        foreach (var id in ids)
        {
            if (pushes.ContainsKey(id) || !publisher.Due(id, registry.Revision, registry.Count == 0, now)) continue;
            snapshot ??= new GhostSet(registry.Revision, registry.Snapshot());
            publisher.Sent(id, snapshot.Revision, now);
            var correlation = "ghost-" + snapshot.Revision.ToString(CultureInfo.InvariantCulture) + "-" + (++pushCounter).ToString(CultureInfo.InvariantCulture);
            pushes[id] = (snapshot.Revision, CruciblePlugin.PeerCommandClient.SendAsync<GhostSet, bool>(id, SetCommandId, correlation, snapshot, CancellationToken.None));
        }
    }

    // Server: one remote peer's request. Identity comes from the authenticated native connection, never from the payload.
    static OperationExecution<GhostAnswer> Decide(AuthorityContext context, (GhostVerb Verb, GhostKey Key) request)
    {
        if (context.IsLocal) return OperationExecution<GhostAnswer>.Rejected("GhostLocalAuthority");
        var net = ZNet.instance;
        var session = CruciblePlugin.Session;
        var active = Plugin.Ready && Plugin.Active && net != null && net.IsServer() && session.AuthorityReady && session.IsAuthority
            && context.Session.Generation == session.Generation && serverGeneration == session.Generation;
        var peer = active ? FindPeer(net!, context.SenderId) : null;
        var id = peer?.m_uid.ToString(CultureInfo.InvariantCulture);
        var matches = peer != null && Key(peer.m_characterID) == request.Key;
        var allowed = peer != null && request.Verb == GhostVerb.On && peer.m_rpc != null
            && MeadworksMischief.DecideForPeer(peer.m_rpc, MischiefCategory.Personal).Allowed;
        var answer = GhostDecision.Decide(request.Verb, active, matches, allowed, id != null && registry.CanAdmit(id), active && VanillaBlocked());
        if (id != null) { if (answer.Keeps) registry.Grant(id, request.Key); else registry.Revoke(id); }
        context.MarkDecisionMade();
        var notice = GhostNotices.For(answer);
        if (notice != null && peer != null) Notify(peer.m_uid, notice);
        log?.Debug("Ghost Mode request from peer " + (id ?? context.SenderId) + ": " + request.Verb + " -> " + answer + ".");
        return OperationExecution<GhostAnswer>.Success(answer);
    }

    // Client: the published set, only from the current verified server connection and session.
    static OperationExecution<bool> Receive(PeerCommandContext context, GhostSet set)
    {
        var net = ZNet.instance;
        if (context.IsLocal || !context.ServerConnectionVerified || net == null || net.IsServer()) return OperationExecution<bool>.Rejected("GhostSetUnverified");
        var generation = CruciblePlugin.Session.Generation;
        if (context.Session.Generation != generation) return OperationExecution<bool>.Rejected("GhostSetStale");
        if (view.Generation != generation) { view.Reset(generation); requester.Reset(); request = null; clientGeneration = generation; }
        context.MarkDecisionMade();
        view.Accept(generation, set.Revision, set.Keys.ToArray());
        return OperationExecution<bool>.Success(true);
    }

    static void ClientTick(double now, ZNet? net, SessionSnapshot session)
    {
        if (net == null || session.Role != SessionRole.RemoteClient || !session.AuthorityReady || !Connected(net)) { ResetClient(); return; }
        if (clientGeneration != session.Generation) { view.Reset(session.Generation); requester.Reset(); request = null; clientGeneration = session.Generation; }
        if (request != null)
        {
            if (!request.IsCompleted) return;
            var done = request; request = null;
            var answered = done.Status == TaskStatus.RanToCompletion && done.Result.State == OperationState.Succeeded;
            requester.Completed(now, answered);
            if (answered) log?.Debug("Ghost Mode answer: " + done.Result.Reply + ".");
        }
        var local = Player.m_localPlayer;
        var key = Key(local);
        var wish = local != null && HexMovement.On(local, "ghost_mode");
        var stamp = MeadworksMischief.Policy + "|" + (session.IsAdmin ? "admin" : "player");
        var verb = requester.Next(now, new GhostRequestFacts(session.Generation, key, wish, view.Contains(key), stamp));
        if (verb.HasValue)
            request = CruciblePlugin.RequestClient.InvokeAsync<(GhostVerb Verb, GhostKey Key), GhostAnswer>(OperationId, (verb.Value, key), CancellationToken.None);
    }

    static ZNetPeer? FindPeer(ZNet net, string id) => long.TryParse(id, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid)
        ? net.GetPeers().FirstOrDefault(p => p != null && p.IsReady() && p.m_uid == uid) : null;

    static bool Connected(ZNet net)
    {
        var server = net.GetServerPeer();
        return server != null && server.IsReady() && server.m_rpc != null && server.m_rpc.IsConnected();
    }

    static GhostKey Key(Player? player)
    {
        var nview = player != null ? player.m_nview : null;
        return nview != null && nview.IsValid() ? Key(nview.GetZDO().m_uid) : default;
    }

    static GhostKey Key(ZDOID id) => id == ZDOID.None ? default : new GhostKey(id.UserID, id.ID);

    static void ResetServer()
    {
        vanillaGrace.Reset();
        if (serverGeneration == -1 && registry.Count == 0 && pushes.Count == 0) return;
        registry.Clear(); publisher.Clear(); pushes.Clear();
        serverGeneration = -1; nextSweep = 0;
    }

    static void ResetClient()
    {
        if (clientGeneration == -1 && view.Count == 0 && request == null) return;
        view.Reset(-1); requester.Reset(); request = null;
        clientGeneration = -1;
    }
}
