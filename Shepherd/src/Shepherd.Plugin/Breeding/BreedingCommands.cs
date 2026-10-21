// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Reuses KeelPenCommands' authenticated authority-to-owner route and HexActions' exact native command lifetime.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Breeding;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using ExtraPotions.Shepherd.Runtime.Integration;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Shepherd.Runtime.Breeding
{
    public static class BreedingCommands
    {
        const string Operation = "meadworks.shepherd.breeding", OwnerCommand = "meadworks.shepherd.breeding_owner";
        static IDisposable? operation, owner;
        static Terminal.ConsoleCommand? command;
        static Task<OperationResult<string>>? pending;
        static Player? pendingActor;
        static Terminal? pendingTerminal;
        static long pendingSession;
        // This synchronized flag is checked alongside the authenticated requesting peer's C02 World decision,
        // rather than incorrectly checking the receiving animal owner's personal cheat permission.
        static bool Enabled => Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out _)
            && ShepherdSettings.Enabled.Value && ShepherdSettings.BreedingCommands.Entry.Value;
        sealed class Request
        {
            internal ZDOID Target, Character;
            internal BreedingVerb Verb;
            internal int Level;
            internal long Actor, Peer;
            internal bool Admin, Host;
            internal string Platform = "";
        }
        sealed class Codec : IOperationCodec<Request,string>
        {
            static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
            public byte[] EncodeRequest(Request value)
            {
                var platform = Utf8.GetBytes(value.Platform);
                if (platform.Length > 128) throw new InvalidDataException("Platform identity exceeds breeding request bound");
                using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
                writer.Write(value.Target.UserID); writer.Write(value.Target.ID); writer.Write((byte)value.Verb); writer.Write(value.Level);
                writer.Write(value.Character.UserID); writer.Write(value.Character.ID); writer.Write(value.Actor); writer.Write(value.Peer);
                writer.Write(value.Admin); writer.Write(value.Host); writer.Write((byte)platform.Length); writer.Write(platform);
                return stream.ToArray();
            }
            static bool Bit(BinaryReader reader)
            {
                byte value = reader.ReadByte(); if (value > 1) throw new InvalidDataException("Invalid breeding flag"); return value == 1;
            }
            public Request DecodeRequest(byte[] bytes)
            {
                if (bytes == null || bytes.Length < 48 || bytes.Length > 176) throw new InvalidDataException("Malformed breeding request");
                using var stream = new MemoryStream(bytes); using var reader = new BinaryReader(stream);
                var value = new Request { Target = new ZDOID(reader.ReadInt64(), reader.ReadUInt32()), Verb = (BreedingVerb)reader.ReadByte(),
                    Level = reader.ReadInt32(), Character = new ZDOID(reader.ReadInt64(), reader.ReadUInt32()), Actor = reader.ReadInt64(), Peer = reader.ReadInt64(),
                    Admin = Bit(reader), Host = Bit(reader) };
                int length = reader.ReadByte();
                if (length > 128 || stream.Length - stream.Position != length || value.Target == ZDOID.None
                    || (value.Verb == BreedingVerb.Breed ? value.Level != 0 : value.Verb != BreedingVerb.Stars || value.Level < 1 || value.Level > 10))
                    throw new InvalidDataException("Invalid breeding target or verb");
                value.Platform = Utf8.GetString(reader.ReadBytes(length)); return value;
            }
            public byte[] EncodeReply(string value)
            {
                if (value != "ok" && value != "off" && value != "refused" && !(value.StartsWith("blocked:", StringComparison.Ordinal)
                    && Enum.TryParse<ConfigBlockReason>(value.Substring(8), out var reason) && Enum.IsDefined(typeof(ConfigBlockReason), reason)))
                    throw new InvalidDataException("Invalid breeding reply");
                return Utf8.GetBytes(value);
            }
            public string DecodeReply(byte[] bytes)
            {
                if (bytes == null || bytes.Length > 64) throw new InvalidDataException("Malformed breeding reply");
                var value = Utf8.GetString(bytes); EncodeReply(value); return value;
            }
        }
        public static void Initialize(ProductRegistration registration)
        {
            Shutdown();
            operation = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(Operation, new System.Version(1,0)), new Codec(), Apply);
            owner = CruciblePlugin.Services.PeerCommands.Register(registration, new OperationDescriptor(OwnerCommand, new System.Version(1,0)), new Codec(), ApplyOwner);
            if (Plugin.AllowsUi && !Terminal.commands.ContainsKey("shepherd"))
                command = new Terminal.ConsoleCommand("shepherd", ShepherdLocalization.Text("command_help"), (Terminal.ConsoleEventArgs args) =>
                    args.Context?.AddString(Execute(args.Args.Skip(1).ToArray(), args.Context)));
        }
        internal static string Execute(string[] args, Terminal? terminal = null)
        {
            var line = BreedingCommandLine.Parse(args, HusbandryBreedingSettings.MaxOffspringLevel.Value);
            if (line.Verb == BreedingVerb.Help) return ShepherdLocalization.Text("command_help");
            if (!Enabled) return ShepherdLocalization.Text("command_off");
            if (!MeadworksMischief.Allows(Plugin.Guid, MischiefCategory.World, out var reason)) return MeadworksBlockedOptions.Reason(reason);
            if (pending != null) return ShepherdLocalization.Text("command_busy");
            var actor = Player.m_localPlayer;
            var target = actor != null ? actor.GetHoverCreature() : null;
            var tame = target != null ? target.GetComponent<Tameable>() : null;
            if (actor == null || actor.IsDead() || tame == null || target!.IsDead() || !tame.IsTamed()
                || tame.m_nview == null || !tame.m_nview.IsValid()) return ShepherdLocalization.Text("command_target");
            pendingSession = CruciblePlugin.Session.Generation; pendingActor = actor; pendingTerminal = terminal;
            pending = CruciblePlugin.RequestClient.InvokeAsync<Request,string>(Operation,
                new Request { Target = tame.m_nview.GetZDO().m_uid, Verb = line.Verb, Level = line.Level }, CancellationToken.None);
            return ShepherdLocalization.Text("command_sent");
        }
        static OperationExecution<string> Apply(AuthorityContext context, Request request)
        {
            if (!Enabled || !ZNet.instance || !ZNet.instance.IsServer() || context.Session.Generation != CruciblePlugin.Session.Generation)
                return OperationExecution<string>.Success("off");
            Player? actor = null; long peerId; ZNetPeer? peer = null; ZDO? actorZdo;
            if (context.IsLocal)
            {
                var decision = MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.World);
                if (!decision.Allowed) return OperationExecution<string>.Success("blocked:" + decision.Reason);
                actor = Player.m_localPlayer; peerId = ZNet.GetUID();
                actorZdo = actor != null && !actor.IsDead() && actor.m_nview != null && actor.m_nview.IsValid() ? actor.m_nview.GetZDO() : null;
            }
            else
            {
                if (!long.TryParse(context.SenderId, NumberStyles.Integer, CultureInfo.InvariantCulture, out peerId)) return OperationExecution<string>.Success("refused");
                peer = ZNet.instance.GetPeer(peerId);
                if (peer == null || !peer.IsReady() || peer.m_rpc == null || !peer.m_rpc.IsConnected()) return OperationExecution<string>.Success("refused");
                var decision = MeadworksMischief.DecideForPeer(peer.m_rpc, MischiefCategory.World);
                if (!decision.Allowed) return OperationExecution<string>.Success("blocked:" + decision.Reason);
                // A dedicated server simulates no avatars, so the requesting player is known by the character ZDO the peer owns.
                actorZdo = peer.m_characterID.IsNone() ? null : ZDOMan.instance?.GetZDO(peer.m_characterID);
            }
            if (actorZdo == null || actorZdo.GetBool(ZDOVars.s_dead) || actorZdo.GetLong(ZDOVars.s_playerID) == 0 || actorZdo.GetOwner() != peerId)
                return OperationExecution<string>.Success("refused");
            // Every authorization field is replaced by current native connection facts before owner dispatch.
            request.Character = actorZdo.m_uid; request.Actor = actorZdo.GetLong(ZDOVars.s_playerID); request.Peer = peerId;
            request.Host = context.IsLocal; request.Platform = peer?.m_rpc.GetSocket()?.GetHostName() ?? "";
            request.Admin = peer != null && request.Platform.Length > 0 && ZNet.instance.IsAdmin(request.Platform);
            if (!Resolve(request, false, out var targetZdo, out _, out var blocked)) return OperationExecution<string>.Success(blocked);
            context.MarkDecisionMade();
            var task = CruciblePlugin.PeerCommandClient.SendAsync<Request,string>(targetZdo!.GetOwner().ToString(CultureInfo.InvariantCulture),
                OwnerCommand, context.Identity.RequestId, request, CancellationToken.None);
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
            if (!Resolve(request, true, out _, out var tame, out var refused)) return OperationExecution<string>.Success(refused);
            if (request.Verb == BreedingVerb.Stars)
            {
                if (tame!.m_character.GetLevel() == request.Level) return OperationExecution<string>.Success("ok");
                context.MarkDecisionMade(); tame.m_character.SetLevel(request.Level);
            }
            else
            {
                var parent = tame!.GetComponent<Procreation>();
                if (parent == null) return OperationExecution<string>.Success("refused");
                if (parent.IsPregnant()) return OperationExecution<string>.Success("ok");
                bool captures = NativeBreeding.Active;
                context.MarkDecisionMade(); parent.MakePregnant();
                if (!captures && parent.m_nview.IsValid() && parent.m_nview.IsOwner()) BreedingHooks.CaptureSire(parent);
            }
            return OperationExecution<string>.Success("ok");
        }
        // The authority resolves the target from its ZDO (a dedicated server holds no animal objects: the prefab says what the ZDO is);
        // the animal's owner repeats every check on the live animal before anything changes (mustOwn).
        static bool Resolve(Request request, bool mustOwn, out ZDO? targetZdo, out Tameable? tame, out string reason)
        {
            targetZdo = null; tame = null; reason = "refused";
            if (!Enabled) { reason = "off"; return false; }
            var decision = request.Host
                ? MischiefGate.Decide(MischiefCategory.World, new AuthorityFacts(SessionRole.Host, true, true,
                    ServerCrucible.Present, PeerClass.RequiredOnAllPeers, ServerProduct.Present), MeadworksMischief.Policy)
                : MischiefGate.DecideForPeer(MischiefCategory.World, MeadworksMischief.Policy, request.Admin, request.Platform);
            if (!decision.Allowed) { reason = "blocked:" + decision.Reason; return false; }
            if (request.Actor == 0 || request.Peer == 0 || request.Target == ZDOID.None
                || (request.Verb == BreedingVerb.Breed ? request.Level != 0
                    : request.Verb != BreedingVerb.Stars || request.Level < 1 || request.Level > HusbandryBreedingSettings.MaxOffspringLevel.Value)) return false;
            targetZdo = ZDOMan.instance?.GetZDO(request.Target);
            if (targetZdo == null || targetZdo.m_uid != request.Target) return false;
            Vector3 point; float radius;
            if (mustOwn)
            {
                var instance = ZNetScene.instance?.FindInstance(request.Target);
                tame = instance != null ? instance.GetComponent<Tameable>() : null;
                if (tame == null || tame.m_nview == null || !tame.m_nview.IsValid() || tame.m_nview.GetZDO().m_uid != request.Target
                    || !tame.m_nview.IsOwner() || tame.m_character == null || tame.m_character is Player
                    || tame.m_character.IsDead() || !tame.IsTamed()) return false;
                point = tame.transform.position; radius = tame.m_character.GetRadius();
            }
            else
            {
                var prefab = ZNetScene.instance?.GetPrefab(targetZdo.GetPrefab());
                var character = prefab != null ? prefab.GetComponent<Character>() : null;
                if (prefab == null || prefab.GetComponent<Tameable>() == null || character == null || character is Player
                    || !targetZdo.GetBool(ZDOVars.s_tamed) || targetZdo.GetFloat(ZDOVars.s_health, 1f) <= 0f) return false;
                point = targetZdo.GetPosition(); radius = 2f;
            }
            var actor = ZDOMan.instance?.GetZDO(request.Character);
            var actorPrefab = actor != null ? ZNetScene.instance?.GetPrefab(actor.GetPrefab()) : null;
            if (actor == null || actor.GetOwner() != request.Peer || actor.GetLong(ZDOVars.s_playerID) != request.Actor || actor.GetBool(ZDOVars.s_dead)
                || actorPrefab == null || actorPrefab.GetComponent<Player>() == null) return false;
            var position = actor.GetPosition();
            return Finite(position) && Finite(point) && Vector3.Distance(position, point) <= 5f + radius && WardAccess(request.Actor, point);
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
        public static void Tick()
        {
            if (pending == null || !pending.IsCompleted) return;
            if (pendingActor != null && pendingActor == Player.m_localPlayer && pendingSession == CruciblePlugin.Session.Generation && pendingTerminal != null)
            {
                string text = ShepherdLocalization.Text("command_unknown");
                if (pending.Status == TaskStatus.RanToCompletion && pending.Result.State == OperationState.Succeeded)
                {
                    string reply = pending.Result.Reply;
                    text = reply == "ok" ? ShepherdLocalization.Text("command_done") : reply == "off" ? ShepherdLocalization.Text("command_off")
                        : reply.StartsWith("blocked:", StringComparison.Ordinal) && Enum.TryParse<ConfigBlockReason>(reply.Substring(8), out var reason)
                        ? MeadworksBlockedOptions.Reason(reason) : ShepherdLocalization.Text("command_refused");
                }
                else if (pending.Status == TaskStatus.RanToCompletion && (pending.Result.State == OperationState.Rejected || pending.Result.State == OperationState.Cancelled))
                    text = ShepherdLocalization.Text("command_refused");
                pendingTerminal.AddString(text);
            }
            pending = null; pendingActor = null; pendingTerminal = null;
        }
        public static void Shutdown()
        {
            operation?.Dispose(); owner?.Dispose(); operation = owner = null;
            if (command != null && Terminal.commands.TryGetValue("shepherd", out var current) && ReferenceEquals(current, command)) Terminal.commands.Remove("shepherd");
            command = null; pending = null; pendingActor = null; pendingTerminal = null;
        }
    }
}
