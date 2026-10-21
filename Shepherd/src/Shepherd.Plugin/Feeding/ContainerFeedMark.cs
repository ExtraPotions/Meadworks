// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Uses the existing Keel desired-state owner command route for design8.1's container hover action.
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Feeding;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using ExtraPotions.Shepherd.Runtime.Integration;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Shepherd.Runtime.Feeding
{
    internal static class ContainerFeedMark
    {
        const string Operation = "meadworks.shepherd.feed_mark", OwnerCommand = "meadworks.shepherd.feed_mark_owner";
        static Harmony? harmony;
        static IDisposable? operation, owner;
        static Task<OperationResult<string>>? pending;
        static Player? pendingActor;
        static long pendingSession;
        static bool Active => NativeFeeding.FeedingActive && ShepherdSettings.ContainerFeeding.Value && !ShepherdSources.ContainersOwnedElsewhere;
        static bool DenyMode => ShepherdSettings.Selection.Value == ContainerSelection.AllExceptDenied;
        static bool Available => Active && (DenyMode || CruciblePlugin.Services.Capabilities.Providers(FeedSources.BurrowFeedSource).Count == 0);
        sealed class Request
        {
            internal ZDOID Target, Character;
            internal long Actor, Peer;
            internal bool Deny, Value;
        }
        sealed class Codec : IOperationCodec<Request,string>
        {
            public byte[] EncodeRequest(Request value)
            {
                using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
                writer.Write(value.Target.UserID); writer.Write(value.Target.ID); writer.Write(value.Deny); writer.Write(value.Value);
                writer.Write(value.Character.UserID); writer.Write(value.Character.ID); writer.Write(value.Actor); writer.Write(value.Peer);
                return stream.ToArray();
            }
            public Request DecodeRequest(byte[] bytes)
            {
                if (bytes == null || bytes.Length != 42 || bytes[12] > 1 || bytes[13] > 1) throw new InvalidDataException("Malformed feed mark request");
                using var stream = new MemoryStream(bytes); using var reader = new BinaryReader(stream);
                var value = new Request { Target = new ZDOID(reader.ReadInt64(), reader.ReadUInt32()), Deny = reader.ReadBoolean(), Value = reader.ReadBoolean(),
                    Character = new ZDOID(reader.ReadInt64(), reader.ReadUInt32()), Actor = reader.ReadInt64(), Peer = reader.ReadInt64() };
                if (value.Target == ZDOID.None) throw new InvalidDataException("Missing feed mark target");
                return value;
            }
            public byte[] EncodeReply(string value)
            {
                if (value != "on" && value != "off" && value != "refused") throw new InvalidDataException("Invalid feed mark reply");
                return System.Text.Encoding.ASCII.GetBytes(value);
            }
            public string DecodeReply(byte[] bytes)
            {
                if (bytes == null || bytes.Length > 7) throw new InvalidDataException("Malformed feed mark reply");
                var value = System.Text.Encoding.ASCII.GetString(bytes); EncodeReply(value); return value;
            }
        }
        internal static void Initialize(ProductRegistration registration)
        {
            Shutdown();
            operation = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(Operation, new System.Version(1,0)), new Codec(), Apply);
            owner = CruciblePlugin.Services.PeerCommands.Register(registration, new OperationDescriptor(OwnerCommand, new System.Version(1,0)), new Codec(), ApplyOwner);
            if (!Plugin.AllowsUi) return;
            harmony = new Harmony(Plugin.Guid + ".feed_mark");
            harmony.CreateClassProcessor(typeof(Use)).Patch(); harmony.CreateClassProcessor(typeof(Hover)).Patch();
        }
        [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
        static class Use
        {
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            static bool Prefix(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result)
            {
                if (hold || character != Player.m_localPlayer || !Available || !MeadworksInput.Allows(InputContext.Gameplay)
                    || !(MeadworksInput.ShiftHeld || MeadworksInput.GamepadActive && alt)) return true;
                var actor = Player.m_localPlayer;
                if (actor == null || actor.IsDead() || !Eligible(__instance, actor.GetPlayerID())) return true;
                __result = true;
                if (pending != null) { actor.Message(MessageHud.MessageType.Center, ShepherdLocalization.Text("feed_mark_busy")); return false; }
                bool deny = DenyMode;
                var request = new Request { Target = __instance.m_nview.GetZDO().m_uid, Deny = deny,
                    Value = !__instance.m_nview.GetZDO().GetBool(deny ? ContainerEligibility.DenyKey : ContainerEligibility.MarkKey) };
                pendingSession = CruciblePlugin.Session.Generation; pendingActor = actor;
                pending = CruciblePlugin.RequestClient.InvokeAsync<Request,string>(Operation, request, CancellationToken.None);
                return false;
            }
        }
        [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
        static class Hover
        {
            [HarmonyPostfix]
            static void Postfix(Container __instance, ref string __result)
            {
                var actor = Player.m_localPlayer;
                if (!Available || actor == null || !Eligible(__instance, actor.GetPlayerID())) return;
                bool allowed = DenyMode ? !__instance.m_nview.GetZDO().GetBool(ContainerEligibility.DenyKey)
                    : __instance.m_nview.GetZDO().GetBool(ContainerEligibility.MarkKey);
                string modifier = "Shift";
                if (MeadworksInput.GamepadActive && global::Localization.instance != null)
                    modifier = global::Localization.instance.GetBoundKeyString(ZInput.IsNonClassicFunctionality() ? "JoyAltKeys" : "JoyAltPlace");
                string hint = ShepherdLocalization.Format("feed_mark_hint", ShepherdLocalization.Text(allowed ? "feed_mark_on" : "feed_mark_off"), modifier);
                __result += "\n" + (global::Localization.instance != null ? global::Localization.instance.Localize(hint) : hint);
            }
        }
        static bool Eligible(Container container, long actor)
        {
            if (!FeedSources.IsUsable(container) || TroughPieces.IsTrough(container) || container.m_privacy == Container.PrivacySetting.Private
                || container.GetComponentInParent<TombStone>() != null) return false;
            var piece = container.GetComponentInParent<Piece>();
            return piece != null && piece.IsPlacedByPlayer() && container.CheckAccess(actor) && WardAccess(actor, container.transform.position);
        }
        static OperationExecution<string> Apply(AuthorityContext context, Request request)
        {
            if (!Available || !ZNet.instance || !ZNet.instance.IsServer() || context.Session.Generation != CruciblePlugin.Session.Generation)
                return OperationExecution<string>.Success("refused");
            Player? actor; long peerId; ZDO? actorZdo;
            if (context.IsLocal)
            {
                actor = Player.m_localPlayer; peerId = ZNet.GetUID();
                actorZdo = actor != null && !actor.IsDead() && actor.m_nview != null && actor.m_nview.IsValid() ? actor.m_nview.GetZDO() : null;
            }
            else
            {
                if (!long.TryParse(context.SenderId, NumberStyles.Integer, CultureInfo.InvariantCulture, out peerId)) return OperationExecution<string>.Success("refused");
                var peer = ZNet.instance.GetPeer(peerId);
                if (peer == null || !peer.IsReady() || peer.m_rpc == null || !peer.m_rpc.IsConnected()) return OperationExecution<string>.Success("refused");
                // A dedicated server simulates no avatars, so the requesting player is known by the character ZDO the peer owns.
                actorZdo = peer.m_characterID.IsNone() ? null : ZDOMan.instance?.GetZDO(peer.m_characterID);
            }
            if (actorZdo == null || actorZdo.GetBool(ZDOVars.s_dead) || actorZdo.GetLong(ZDOVars.s_playerID) == 0 || actorZdo.GetOwner() != peerId)
                return OperationExecution<string>.Success("refused");
            request.Character = actorZdo.m_uid; request.Actor = actorZdo.GetLong(ZDOVars.s_playerID); request.Peer = peerId;
            if (!Resolve(request, false, out var targetZdo, out _)) return OperationExecution<string>.Success("refused");
            context.MarkDecisionMade();
            return Poll(CruciblePlugin.PeerCommandClient.SendAsync<Request,string>(targetZdo!.GetOwner().ToString(CultureInfo.InvariantCulture),
                OwnerCommand, context.Identity.RequestId, request, CancellationToken.None));
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
            if (!Resolve(request, true, out _, out var container)) return OperationExecution<string>.Success("refused");
            context.MarkDecisionMade();
            container!.m_nview.GetZDO().Set(request.Deny ? ContainerEligibility.DenyKey : ContainerEligibility.MarkKey, request.Value);
            return OperationExecution<string>.Success(request.Deny != request.Value ? "on" : "off");
        }
        // The authority resolves the container from its ZDO (a dedicated server holds no objects: the prefab says what the ZDO is);
        // the container's owner repeats every eligibility check on the live container before the mark changes (mustOwn).
        static bool Resolve(Request request, bool mustOwn, out ZDO? targetZdo, out Container? container)
        {
            targetZdo = null; container = null;
            if (!Available || request.Deny != DenyMode || request.Actor == 0 || request.Peer == 0 || request.Target == ZDOID.None) return false;
            targetZdo = ZDOMan.instance?.GetZDO(request.Target);
            if (targetZdo == null || targetZdo.m_uid != request.Target) return false;
            Vector3 point;
            if (mustOwn)
            {
                container = ZNetScene.instance?.FindInstance(request.Target)?.GetComponent<Container>();
                if (container == null || !Eligible(container, request.Actor) || container.m_nview.GetZDO().m_uid != request.Target || !container.m_nview.IsOwner()) return false;
                point = container.transform.position;
            }
            else
            {
                var prefab = ZNetScene.instance?.GetPrefab(targetZdo.GetPrefab());
                if (prefab == null || prefab.GetComponent<Container>() == null) return false;
                point = targetZdo.GetPosition();
            }
            var actor = ZDOMan.instance?.GetZDO(request.Character);
            var actorPrefab = actor != null ? ZNetScene.instance?.GetPrefab(actor.GetPrefab()) : null;
            if (actor == null || actor.GetOwner() != request.Peer || actor.GetLong(ZDOVars.s_playerID) != request.Actor || actor.GetBool(ZDOVars.s_dead)
                || actorPrefab == null || actorPrefab.GetComponent<Player>() == null) return false;
            var position = actor.GetPosition();
            return Finite(position) && Finite(point) && Vector3.Distance(position, point) <= 5f;
        }
        static bool Finite(Vector3 point) => !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z)
            && !float.IsInfinity(point.x) && !float.IsInfinity(point.y) && !float.IsInfinity(point.z);
        static bool WardAccess(long actor, Vector3 point)
        {
            bool covered = false;
            foreach (var ward in PrivateArea.m_allAreas)
            {
                if (ward == null || !ward.IsEnabled() || !ward.IsInside(point, 0)) continue;
                covered = true; if (ward.m_piece.GetCreator() == actor || ward.IsPermitted(actor)) return true;
            }
            return !covered;
        }
        internal static void Tick()
        {
            if (pending == null || !pending.IsCompleted) return;
            if (pendingActor != null && pendingActor == Player.m_localPlayer && pendingSession == CruciblePlugin.Session.Generation)
            {
                string key = "feed_mark_unknown";
                if (pending.Status == TaskStatus.RanToCompletion)
                {
                    var result = pending.Result;
                    if (result.State == OperationState.Succeeded) key = result.Reply == "on" ? "feed_mark_on" : result.Reply == "off" ? "feed_mark_off" : "feed_mark_refused";
                    else if (result.State == OperationState.Rejected || result.State == OperationState.Cancelled) key = "feed_mark_refused";
                }
                pendingActor.Message(MessageHud.MessageType.Center, ShepherdLocalization.Text(key));
            }
            pending = null; pendingActor = null;
        }
        internal static void Shutdown()
        {
            harmony?.UnpatchSelf(); harmony = null;
            operation?.Dispose(); owner?.Dispose(); operation = owner = null; pending = null; pendingActor = null;
        }
    }
}
