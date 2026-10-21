// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Product-local gate admission over existing Crucible Operations and authority-to-owner PeerCommands.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Keel.Catalog;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Localization;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Keel.Runtime
{
    internal static class KeelPenCommands
    {
        const string Operation = "meadworks.keel.gate", OwnerCommand = "meadworks.keel.gate_owner";
        static IDisposable? operation, owner;
        static Task<OperationResult<string>>? pending;
        static long pendingSession;
        static Player? pendingActor;
        sealed class Request
        {
            internal ZDOID Ship, Character;
            internal int Gate;
            internal bool Open;
            internal long Actor, Peer;
        }
        sealed class Codec : IOperationCodec<Request, string>
        {
            public byte[] EncodeRequest(Request value)
            {
                using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
                writer.Write(value.Ship.UserID); writer.Write(value.Ship.ID); writer.Write(value.Gate); writer.Write(value.Open);
                writer.Write(value.Character.UserID); writer.Write(value.Character.ID); writer.Write(value.Actor); writer.Write(value.Peer);
                return stream.ToArray();
            }
            public Request DecodeRequest(byte[] bytes)
            {
                if (bytes == null || bytes.Length != 45) throw new InvalidDataException("Malformed gate request");
                using var stream = new MemoryStream(bytes); using var reader = new BinaryReader(stream);
                var value = new Request { Ship = new ZDOID(reader.ReadInt64(), reader.ReadUInt32()), Gate = reader.ReadInt32(), Open = reader.ReadBoolean(),
                    Character = new ZDOID(reader.ReadInt64(), reader.ReadUInt32()), Actor = reader.ReadInt64(), Peer = reader.ReadInt64() };
                if (value.Ship == ZDOID.None || value.Gate < 0 || value.Gate >= PenRules.GatesPerPen || bytes[16] > 1) throw new InvalidDataException("Invalid gate identity");
                return value;
            }
            public byte[] EncodeReply(string value)
            {
                if (value != "ok" && value != "refused" && value != "moving") throw new InvalidDataException("Invalid gate reply");
                return System.Text.Encoding.ASCII.GetBytes(value);
            }
            public string DecodeReply(byte[] bytes)
            {
                if (bytes == null || bytes.Length > 7) throw new InvalidDataException("Malformed gate reply");
                var value = System.Text.Encoding.ASCII.GetString(bytes); EncodeReply(value); return value;
            }
        }

        internal static void Initialize(ProductRegistration registration)
        {
            operation = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(Operation, new System.Version(1, 0)), new Codec(), Apply);
            owner = CruciblePlugin.Services.PeerCommands.Register(registration, new OperationDescriptor(OwnerCommand, new System.Version(1, 0)), new Codec(), ApplyOwner);
        }
        internal static bool Use(KeelPen pen, int gate)
        {
            var actor = Player.m_localPlayer;
            if (!pen.Live || actor == null || gate < 0 || gate >= PenRules.GatesPerPen || pending != null) return false;
            // A new manual interaction reads the replicated state. Unknown replies never repeat a toggle.
            var request = new Request { Ship = pen.View!.GetZDO().m_uid, Gate = gate, Open = !pen.Open(gate) };
            pendingSession = CruciblePlugin.Session.Generation; pendingActor = actor;
            pending = CruciblePlugin.RequestClient.InvokeAsync<Request, string>(Operation, request, CancellationToken.None);
            return true;
        }

        static OperationExecution<string> Apply(AuthorityContext context, Request request)
        {
            if (!KeelCatalog.Active || !ZNet.instance || !ZNet.instance.IsServer() || context.Session.Generation != CruciblePlugin.Session.Generation)
                return OperationExecution<string>.Rejected("KeelInactive");
            Player? actor = null;
            long peerId;
            if (context.IsLocal) { actor = Player.m_localPlayer; peerId = ZNet.GetUID(); }
            else
            {
                if (!long.TryParse(context.SenderId, NumberStyles.Integer, CultureInfo.InvariantCulture, out peerId)) return OperationExecution<string>.Rejected("ActorUnavailable");
                var peer = ZNet.instance.GetPeer(peerId);
                if (peer == null || !peer.IsReady() || peer.m_rpc == null || !peer.m_rpc.IsConnected()) return OperationExecution<string>.Rejected("ActorUnavailable");
                actor = ZNetScene.instance?.FindInstance(peer.m_characterID)?.GetComponent<Player>();
            }
            var actorView = actor != null ? actor.GetComponent<ZNetView>() : null;
            if (actor == null || actor.IsDead() || actor.GetPlayerID() == 0 || actorView == null || !actorView.IsValid() || actorView.GetZDO().GetOwner() != peerId)
                return OperationExecution<string>.Rejected("ActorUnavailable");
            // Payload character/peer fields are replaced by the authenticated native connection facts.
            request.Character = actorView.GetZDO().m_uid; request.Actor = actor.GetPlayerID(); request.Peer = peerId;
            if (!Resolve(request, false, out var pen, out var reason)) return OperationExecution<string>.Success(reason);
            long expectedOwner = pen!.View!.GetZDO().GetOwner();
            context.MarkDecisionMade();
            var task = CruciblePlugin.PeerCommandClient.SendAsync<Request, string>(expectedOwner.ToString(CultureInfo.InvariantCulture), OwnerCommand,
                context.Identity.RequestId, request, CancellationToken.None);
            return Poll(task);
        }
        static OperationExecution<string> Poll(Task<OperationResult<string>> task)
        {
            if (!task.IsCompleted) return OperationExecution<string>.Pending(() => Poll(task));
            if (task.Status != TaskStatus.RanToCompletion || task.Result.State != OperationState.Succeeded) return OperationExecution<string>.RecoveryRequired();
            return OperationExecution<string>.Success(task.Result.Reply);
        }
        static OperationExecution<string> ApplyOwner(PeerCommandContext context, Request request)
        {
            if ((!context.IsLocal && !context.ServerConnectionVerified) || context.Session.Generation != CruciblePlugin.Session.Generation)
                return OperationExecution<string>.Rejected("AuthorityUnavailable");
            if (!Resolve(request, true, out var pen, out var reason)) return OperationExecution<string>.Success(reason);
            context.MarkDecisionMade();
            pen!.View!.GetZDO().Set(PenRules.GateKey(request.Gate), request.Open);
            return OperationExecution<string>.Success("ok");
        }
        static bool Resolve(Request request, bool mustOwn, out KeelPen? pen, out string reason)
        {
            pen = null; reason = "refused";
            if (!KeelCatalog.Active || request.Gate < 0 || request.Gate >= PenRules.GatesPerPen || request.Actor == 0 || request.Peer == 0) return false;
            var instance = ZNetScene.instance?.FindInstance(request.Ship);
            pen = instance != null ? instance.GetComponentInChildren<KeelPen>() : null;
            if (pen == null || !pen.Live || pen.View!.GetZDO().m_uid != request.Ship || (mustOwn && !pen.View.IsOwner())) return false;
            if (!KeelCatalog.TryGet(Utils.GetPrefabName(instance!), out var catalog) || !catalog.Type.HasPen || !catalog.PenReady) return false;
            var character = ZDOMan.instance?.GetZDO(request.Character);
            var characterPrefab = character != null && ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(character.GetPrefab()) : null;
            if (character == null || character.GetOwner() != request.Peer || character.GetLong(ZDOVars.s_playerID, 0) != request.Actor || character.GetBool(ZDOVars.s_dead)
                || characterPrefab == null || characterPrefab.GetComponent<Player>() == null) return false;
            Vector3 point = pen.GatePoint(request.Gate), position = character.GetPosition();
            if (!Finite(position) || !Finite(point) || Vector3.Distance(position, point) > 5f || !WardAccess(request.Actor, point)) return false;
            var settings = Plugin.Settings!;
            if (PenRules.Gate(request.Gate, pen.Gates.Length, request.Open, pen.Speed, settings.Get(KeelSettings.Pens.ContainmentSpeed),
                settings.Get(KeelSettings.Pens.GatesOpenAtRestOnly)) != GateDecision.Accepted) { reason = "moving"; return false; }
            return true;
        }
        static bool WardAccess(long actor, Vector3 point)
        {
            // Native default CheckAccess semantics, evaluated for the authenticated actor rather than local host.
            bool covered = false;
            foreach (var ward in PrivateArea.m_allAreas)
            {
                if (ward == null || !ward.IsEnabled() || !ward.IsInside(point, 0)) continue;
                covered = true;
                if (ward.m_piece.GetCreator() == actor || ward.IsPermitted(actor)) return true;
            }
            return !covered;
        }
        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
        internal static void Tick()
        {
            if (pending == null || !pending.IsCompleted) return;
            if (pendingActor == Player.m_localPlayer && pendingSession == CruciblePlugin.Session.Generation && pendingActor != null
                && pending.Status == TaskStatus.RanToCompletion && pending.Result.State == OperationState.Succeeded && pending.Result.Reply == "moving")
                pendingActor.Message(MessageHud.MessageType.Center, KeelText.Text(KeelTextKeys.GateRefusedMoving));
            pending = null; pendingActor = null;
        }
        internal static void Shutdown() { operation?.Dispose(); owner?.Dispose(); operation = owner = null; pending = null; pendingActor = null; }
    }
}
