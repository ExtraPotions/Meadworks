// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using Audit = System.Action<string, string, string, int, int, int, bool, bool>;
namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexSpawnAuthority
{
    internal const string ReceiptField = "expDARE.Hex.SpawnReceipt", ActorField = "expDARE.Hex.SpawnActor", StackField = "expDARE.Hex.SpawnStack";
    static IDisposable? operation;
    static ManualLogSource? log;
    static readonly Dictionary<RequestIdentity, Work> works = new();
    static int budgetFrame = -1, used;
    sealed class Codec : IOperationCodec<HexSpawnRequest, HexSpawnReply>
    {
        public byte[] EncodeRequest(HexSpawnRequest value) => HexSpawnData.Encode(value);
        public HexSpawnRequest DecodeRequest(byte[] bytes) => HexSpawnData.Decode(bytes);
        public byte[] EncodeReply(HexSpawnReply value) => HexSpawnData.Encode(value);
        public HexSpawnReply DecodeReply(byte[] bytes) => HexSpawnData.DecodeReply(bytes);
    }
    sealed class Work
    {
        internal AuthorityContext Context = null!;
        internal HexSpawnRequest Request = null!;
        internal HexSpawnCatalog.Entry Entry = null!;
        internal Actor Actor = null!;
        internal ZNet Net = null!;
        internal ZDOID Character;
        internal long Profile;
        internal string AuditActor = "", Receipt = System.Guid.NewGuid().ToString("N");
        internal int Created;
        internal bool Complete, Uncertain, Audited;
    }
    internal static void Initialize(ProductRegistration registration, ManualLogSource logger)
    {
        log = logger;
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "Spawn.Create", MischiefCategory.Economy);
        operation = CruciblePlugin.Services.Operations.Register(registration,
            new OperationDescriptor(HexSpawnData.OperationId, HexSpawnData.Version), new Codec(), Start, Recover);
    }
    // Who asked, as the server knows them. A dedicated server holds a connected player's character as a ZDO, not as a GameObject,
    // unless the player is near the server's own reference point, so the request is anchored on the ZDO (identity, profile, pose).
    sealed class Actor
    {
        internal ZDOID Id; internal long Profile; internal Vector3 Position; internal Quaternion Rotation;
    }
    static Actor Of(Player player) => new Actor { Id = player.m_nview.GetZDO().m_uid, Profile = player.GetPlayerID(), Position = player.transform.position, Rotation = player.transform.rotation };
    static bool Current(AuthorityContext context, ZNet? expected, out Actor? actor, out string auditActor)
    {
        actor = null; auditActor = "host";
        var net = ZNet.instance; var session = CruciblePlugin.Session;
        if (!Plugin.Ready || !Plugin.Active || Plugin.Settings?.Enabled.Value != true || net == null || !net.IsServer()
            || expected != null && net != expected || !session.AuthorityReady || !session.IsAuthority
            || context.Session.Generation != session.Generation || context.Session.WorldId != session.WorldId) return false;
        if (context.IsLocal)
        {
            var local = Player.m_localPlayer;
            if (!HexMovement.Owned(local) || !MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.Economy).Allowed) return false;
            actor = Of(local);
            return true;
        }
        if (!long.TryParse(context.SenderId, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid)) return false;
        var peer = net.GetPeers().FirstOrDefault(p => p != null && p.IsReady() && p.m_uid == uid && p.m_rpc != null && p.m_rpc.IsConnected());
        if (peer == null || !MeadworksMischief.DecideForPeer(peer.m_rpc, MischiefCategory.Economy).Allowed || ZNetScene.instance == null) return false;
        auditActor = peer.m_socket?.GetHostName() ?? "";
        var zdo = ZDOMan.instance?.GetZDO(peer.m_characterID);
        if (zdo == null || zdo.m_uid != peer.m_characterID || zdo.GetBool(ZDOVars.s_dead, false)) return false;
        var loaded = ZNetScene.instance.FindInstance(zdo)?.GetComponent<Player>();
        if (loaded != null && (loaded.IsDead() || loaded.m_nview == null || !loaded.m_nview.IsValid())) return false;
        actor = new Actor { Id = zdo.m_uid, Profile = zdo.GetLong(ZDOVars.s_playerID, 0L),
            Position = loaded != null ? loaded.transform.position : zdo.GetPosition(), Rotation = loaded != null ? loaded.transform.rotation : zdo.GetRotation() };
        return true;
    }
    static OperationExecution<HexSpawnReply> Start(AuthorityContext context, HexSpawnRequest request)
    {
        if (!HexSpawnData.Valid(request) || !Current(context, null, out var actor, out var auditActor)) return Refuse(context, request, "SpawnDenied");
        var entry = HexSpawnCatalog.Resolve(request);
        if (entry == null || !HexSpawnData.Supported(request, entry.MaxQuality, entry.Tameable) || actor!.Profile == 0)
            return Refuse(context, request, "SpawnUnsupported");
        // Capacity is bounded; completed bookkeeping may be evicted, never undecided/uncertain work.
        if (works.Count >= 256) foreach (var old in works.Where(pair => pair.Value.Complete).Select(pair => pair.Key).Take(64).ToArray()) works.Remove(old);
        if (works.Count >= 256 || works.Values.Count(w => !w.Complete) >= 4 || works.Values.Any(w => !w.Complete && w.Context.ActorId == context.ActorId))
            return Refuse(context, request, "SpawnBusy");
        var work = new Work { Context = context, Request = request, Entry = entry, Actor = actor, Net = ZNet.instance,
            Character = actor.Id, Profile = actor.Profile, AuditActor = auditActor };
        works.Add(context.Identity, work);
        context.MarkDecisionMade();
        return Poll(work);
    }
    static OperationExecution<HexSpawnReply> Refuse(AuthorityContext context, HexSpawnRequest request, string code)
    {
        // Crucible ActorId is the native profile ID and SenderId is the connection UID, not the platform audit identity.
        var net = ZNet.instance; var actor = context.IsLocal ? "host" : "";
        if (!context.IsLocal && net != null && net.IsServer() && context.Session.Generation == CruciblePlugin.Session.Generation
            && long.TryParse(context.SenderId, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid))
        {
            var peer = net.GetPeers().FirstOrDefault(p => p != null && p.IsReady() && p.m_uid == uid && p.m_rpc != null && p.m_rpc.IsConnected());
            actor = peer?.m_socket?.GetHostName() ?? "";
        }
        AuditSpawn(actor, request, 0);
        return OperationExecution<HexSpawnReply>.Rejected(code);
    }
    static void AuditSpawn(string actor, HexSpawnRequest request, int created)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(actor) && HexSpawnData.Valid(request) && CruciblePlugin.Services.Capabilities.TryResolve<Audit>(HexSpawnData.AuditCapability, HexSpawnData.Version, out var audit) && audit != null)
                audit(actor, request.Kind == HexSpawnKind.Item ? "item" : "creature", request.Prefab,
                    request.Amount, created, request.Level, request.Tamed, created > 0);
        }
        catch { log?.LogDebug("Optional Thing spawn audit unavailable; creation will not retry."); }
    }
    static OperationExecution<HexSpawnReply> Poll(Work work)
    {
        if (work.Uncertain) return OperationExecution<HexSpawnReply>.RecoveryRequired();
        if (work.Complete) return Reply(work);
        var request = work.Request;
        if (!Current(work.Context, work.Net, out var actor, out _) || actor!.Profile != work.Profile
            || actor.Id != work.Character || work.Entry.Prefab == null || ZNetScene.instance == null
            || ZNetScene.instance.GetPrefab(request.Prefab) != work.Entry.Prefab) return Finish(work);
        if (budgetFrame != Time.frameCount) { budgetFrame = Time.frameCount; used = 0; }
        while (work.Created < request.Amount && used < 32)
        {
            // The native transport is drained on Unity's main thread; permission is checked for each bounded batch.
            used++;
            var remaining = request.Amount - work.Created;
            var itemPrefab = request.Kind == HexSpawnKind.Item ? work.Entry.Prefab.GetComponent<ItemDrop>() : null;
            if (request.Kind == HexSpawnKind.Item && (itemPrefab?.m_itemData?.m_shared == null || request.Level > itemPrefab.m_itemData.m_shared.m_maxQuality
                || itemPrefab.m_itemData.m_shared.m_maxStackSize < 1)) return Finish(work);
            var position = actor.Position + actor.Rotation * Vector3.forward * 2f + Vector3.up;
            if (!Finite(position)) return Finish(work);
            try
            {
                var instance = UnityEngine.Object.Instantiate(work.Entry.Prefab, position, Quaternion.identity);
                var view = instance.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.IsOwner()) throw new InvalidOperationException("Native spawn ownership unavailable.");
                if (request.Kind == HexSpawnKind.Item)
                {
                    var drop = instance.GetComponent<ItemDrop>();
                    var amount = Math.Min(remaining, itemPrefab!.m_itemData.m_shared.m_maxStackSize);
                    drop.m_itemData.m_stack = amount; drop.m_itemData.m_quality = request.Level;
                    drop.m_itemData.m_durability = drop.m_itemData.GetMaxDurability(); drop.m_itemData.m_cheated = true;
                    if (request.Inventory)
                    {
                        var zdo = view.GetZDO(); zdo.Set(ReceiptField, work.Receipt); zdo.Set(ActorField, work.Profile); zdo.Set(StackField, amount);
                    }
                    drop.Save(); work.Created += amount;
                }
                else
                {
                    var creature = instance.GetComponent<Character>(); creature.SetLevel(request.Level);
                    if (request.Tamed)
                    {
                        var tameable = instance.GetComponent<Tameable>();
                        if (tameable == null || instance.GetComponent<MonsterAI>() == null) throw new InvalidOperationException("Native tame contract unavailable.");
                        tameable.Tame();
                        if (!creature.IsTamed()) throw new InvalidOperationException("Native tame did not complete.");
                    }
                    work.Created++;
                }
            }
            catch
            {
                // An external Awake/initialization hook may have created a native object before throwing.
                // Keep its world state and volatile bookkeeping uncertain; never recreate it or report a definite zero.
                work.Uncertain = true; log?.LogWarning("Hex spawn initialization is uncertain; native objects preserved and creation will not retry.");
                return OperationExecution<HexSpawnReply>.RecoveryRequired();
            }
        }
        return work.Created == request.Amount ? Finish(work) : OperationExecution<HexSpawnReply>.Pending(() => Poll(work));
    }
    static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    static OperationExecution<HexSpawnReply> Reply(Work work) => OperationExecution<HexSpawnReply>.Success(new HexSpawnReply(work.Receipt, work.Created, work.Created != work.Request.Amount));
    static OperationExecution<HexSpawnReply> Finish(Work work)
    {
        work.Complete = true;
        if (!work.Audited)
        {
            work.Audited = true;
            AuditSpawn(work.AuditActor, work.Request, work.Created);
        }
        return Reply(work);
    }
    static OperationExecution<HexSpawnReply> Recover(RequestIdentity identity)
    {
        // Recovery reconciles exact known counts only. A canceled/old-session batch never resumes creation.
        return works.TryGetValue(identity, out var work) && !work.Uncertain ? Finish(work) : OperationExecution<HexSpawnReply>.RecoveryRequired();
    }
    internal static void Shutdown() { operation?.Dispose(); operation = null; works.Clear(); log = null; budgetFrame = -1; used = 0; }
}
