// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Uses the existing desired-state authority/owner command route, as Shepherd's container mark does.
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Ember.Config;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Ember.Runtime
{
    public static class TorchKeepCommands
    {
        const string Operation = "meadworks.ember.torch_keep", OwnerCommand = "meadworks.ember.torch_keep_owner";
        static IDisposable? operation, owner;
        static Task<OperationResult<string>>? pending;
        static Player? pendingActor;
        static long pendingSession;

        sealed class Request
        {
            internal ZDOID Target, Character;
            internal long Actor, Peer;
            internal bool Keep;
        }

        sealed class Codec : IOperationCodec<Request, string>
        {
            public byte[] EncodeRequest(Request value)
            {
                using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
                writer.Write(value.Target.UserID); writer.Write(value.Target.ID); writer.Write(value.Keep);
                writer.Write(value.Character.UserID); writer.Write(value.Character.ID); writer.Write(value.Actor); writer.Write(value.Peer);
                return stream.ToArray();
            }
            public Request DecodeRequest(byte[] bytes)
            {
                if (bytes == null || bytes.Length != 41 || bytes[12] > 1) throw new InvalidDataException("Malformed torch request");
                using var stream = new MemoryStream(bytes); using var reader = new BinaryReader(stream);
                var value = new Request { Target = new ZDOID(reader.ReadInt64(), reader.ReadUInt32()), Keep = reader.ReadBoolean(),
                    Character = new ZDOID(reader.ReadInt64(), reader.ReadUInt32()), Actor = reader.ReadInt64(), Peer = reader.ReadInt64() };
                if (value.Target == ZDOID.None) throw new InvalidDataException("Missing torch target");
                return value;
            }
            public byte[] EncodeReply(string value)
            {
                if (value != "kept" && value != "scheduled" && value != "refused") throw new InvalidDataException("Invalid torch reply");
                return System.Text.Encoding.ASCII.GetBytes(value);
            }
            public string DecodeReply(byte[] bytes)
            {
                if (bytes == null || bytes.Length > 9) throw new InvalidDataException("Malformed torch reply");
                string value = System.Text.Encoding.ASCII.GetString(bytes); EncodeReply(value); return value;
            }
        }

        internal static void Initialize(ProductRegistration registration)
        {
            Shutdown();
            var version = new System.Version(1, 0);
            operation = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(Operation, version), new Codec(), Apply);
            owner = CruciblePlugin.Services.PeerCommands.Register(registration, new OperationDescriptor(OwnerCommand, version), new Codec(), ApplyOwner);
        }

        public static void RequestKeep(Fireplace fire, bool keep)
        {
            var actor = Player.m_localPlayer;
            if (pending != null || !actor || actor.IsDead() || !TorchScheduleRuntime.Switchable(fire)
                || !PrivateArea.CheckAccess(fire.transform.position)) return;
            if (Vector3.Distance(actor.transform.position, fire.transform.position) > actor.m_maxInteractDistance + fire.GetHoverOffset()) return;
            var view = fire.GetComponent<ZNetView>();
            if (!view.HasOwner()) view.ClaimOwnership();
            pendingActor = actor; pendingSession = CruciblePlugin.Session.Generation;
            pending = CruciblePlugin.RequestClient.InvokeAsync<Request, string>(Operation,
                new Request { Target = view.GetZDO().m_uid, Keep = keep }, CancellationToken.None);
        }

        static OperationExecution<string> Apply(AuthorityContext context, Request request)
        {
            if (!TorchScheduleRuntime.Enabled || !ZNet.instance || !ZNet.instance.IsServer()
                || context.Session.Generation != CruciblePlugin.Session.Generation) return OperationExecution<string>.Success("refused");
            long peerId;
            ZDO? character;
            if (context.IsLocal)
            {
                var actor = Player.m_localPlayer; peerId = ZNet.GetUID();
                var view = actor ? actor.GetComponent<ZNetView>() : null;
                character = actor && !actor.IsDead() && view && view.IsValid() ? view.GetZDO() : null;
            }
            else
            {
                if (!long.TryParse(context.SenderId, NumberStyles.Integer, CultureInfo.InvariantCulture, out peerId))
                    return OperationExecution<string>.Success("refused");
                var peer = ZNet.instance.GetPeer(peerId);
                if (peer == null || !peer.IsReady() || peer.m_rpc == null || !peer.m_rpc.IsConnected())
                    return OperationExecution<string>.Success("refused");
                character = peer.m_characterID.IsNone() ? null : ZDOMan.instance?.GetZDO(peer.m_characterID);
            }
            if (character == null || character.GetBool(ZDOVars.s_dead) || character.GetLong(ZDOVars.s_playerID) == 0 || character.GetOwner() != peerId)
                return OperationExecution<string>.Success("refused");
            request.Character = character.m_uid; request.Actor = character.GetLong(ZDOVars.s_playerID); request.Peer = peerId;
            if (!Resolve(request, false, out var target, out _)) return OperationExecution<string>.Success("refused");
            context.MarkDecisionMade();
            return Poll(CruciblePlugin.PeerCommandClient.SendAsync<Request, string>(target!.GetOwner().ToString(CultureInfo.InvariantCulture),
                OwnerCommand, context.Identity.RequestId, request, CancellationToken.None));
        }

        static OperationExecution<string> Poll(Task<OperationResult<string>> task)
        {
            if (!task.IsCompleted) return OperationExecution<string>.Pending(() => Poll(task));
            return task.Status == TaskStatus.RanToCompletion && task.Result.State == OperationState.Succeeded
                ? OperationExecution<string>.Success(task.Result.Reply) : OperationExecution<string>.RecoveryRequired();
        }

        static OperationExecution<string> ApplyOwner(PeerCommandContext context, Request request)
        {
            if ((!context.IsLocal && !context.ServerConnectionVerified) || context.Session.Generation != CruciblePlugin.Session.Generation)
                return OperationExecution<string>.Rejected("AuthorityUnavailable");
            if (!Resolve(request, true, out var target, out var fire)) return OperationExecution<string>.Success("refused");
            context.MarkDecisionMade();
            target!.Set(TorchScheduleRuntime.KeepKey, request.Keep);
            if (request.Keep) TorchScheduleRuntime.SetOn(fire!, true);
            else target.Set(TorchScheduleRuntime.PhaseKey, 0);
            TorchScheduleRuntime.Apply(fire!);
            return OperationExecution<string>.Success(request.Keep ? "kept" : "scheduled");
        }

        static bool Resolve(Request request, bool mustOwn, out ZDO? target, out Fireplace? fire)
        {
            target = null; fire = null;
            if (!TorchScheduleRuntime.Enabled || request.Actor == 0 || request.Peer == 0 || request.Target == ZDOID.None
                || !ZNetScene.instance || ZDOMan.instance == null) return false;
            target = ZDOMan.instance.GetZDO(request.Target);
            if (target == null || target.m_uid != request.Target || !target.HasOwner() || !TorchScheduleRuntime.Listed(target)
                || target.GetLong(ZDOVars.s_creator) == 0) return false;
            var prefab = ZNetScene.instance.GetPrefab(target.GetPrefab());
            if (!prefab || !prefab.GetComponent<Fireplace>() || !prefab.GetComponent<Piece>()) return false;
            if (mustOwn)
            {
                var instance = ZNetScene.instance.FindInstance(request.Target);
                fire = instance ? instance.GetComponent<Fireplace>() : null;
                if (!fire || !TorchScheduleRuntime.PlayerFire(fire, true) || fire.GetComponent<ZNetView>().GetZDO().m_uid != request.Target
                    || !WardAccess(request.Actor, fire.transform.position)) return false;
            }
            var actor = ZDOMan.instance.GetZDO(request.Character);
            var actorPrefab = actor != null ? ZNetScene.instance.GetPrefab(actor.GetPrefab()) : null;
            if (actor == null || actor.GetOwner() != request.Peer || actor.GetLong(ZDOVars.s_playerID) != request.Actor
                || actor.GetBool(ZDOVars.s_dead) || !actorPrefab || !actorPrefab.GetComponent<Player>()) return false;
            var point = target.GetPosition(); var position = actor.GetPosition();
            float range = actorPrefab.GetComponent<Player>().m_maxInteractDistance + prefab.GetComponent<Fireplace>().GetHoverOffset();
            return Finite(point) && Finite(position) && range > 0f && Vector3.Distance(point, position) <= range;
        }

        static bool Finite(Vector3 point) => !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z)
            && !float.IsInfinity(point.x) && !float.IsInfinity(point.y) && !float.IsInfinity(point.z);

        static bool WardAccess(long actor, Vector3 point)
        {
            bool covered = false;
            foreach (var ward in PrivateArea.m_allAreas)
            {
                if (!ward || !ward.IsEnabled() || !ward.IsInside(point, 0f)) continue;
                covered = true;
                if (ward.m_piece.GetCreator() == actor || ward.IsPermitted(actor)) return true;
            }
            return !covered;
        }

        internal static void Tick()
        {
            var player = Player.m_localPlayer;
            if (pending != null && pending.IsCompleted)
            {
                if (player && pendingActor == player && pendingSession == CruciblePlugin.Session.Generation)
                {
                    string key = pending.Status == TaskStatus.RanToCompletion && pending.Result.State == OperationState.Succeeded
                        ? pending.Result.Reply == "kept" ? "torch_kept" : pending.Result.Reply == "scheduled" ? "torch_scheduled" : "torch_refused"
                        : "torch_refused";
                    player.Message(MessageHud.MessageType.Center, EmberText.Text(key));
                }
                pending = null; pendingActor = null;
            }
            if (!player || !TorchScheduleRuntime.Enabled || !player.TakeInput()
                || !MeadworksInput.Pressed(Plugin.Settings!.Shortcut(EmberSettings.Torches.TorchSwitchKey))) return;
            var hovered = player.GetHoverObject();
            var fire = hovered ? hovered.GetComponentInParent<Fireplace>() : null;
            if (fire && TorchScheduleRuntime.Switchable(fire))
                RequestKeep(fire, !fire.GetComponent<ZNetView>().GetZDO().GetBool(TorchScheduleRuntime.KeepKey));
        }

        internal static void Shutdown()
        {
            operation?.Dispose(); owner?.Dispose(); operation = owner = null;
            pending = null; pendingActor = null;
        }
    }
}
