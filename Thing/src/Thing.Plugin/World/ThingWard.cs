// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, behaviour only: authenticated session ward override for the requesting character; no mod code adopted.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Commands;
using ExtraPotions.Thing.Config;
using ExtraPotions.Thing.Integration;
using ExtraPotions.Thing.Moderation;
using ExtraPotions.Thing.WorldAdmin;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    public static class ThingWard
    {
        const string OperationId = "meadworks.thing.ward";
        static IDisposable? operation;
        static Harmony? harmony;
        static readonly WardOverrideGrants grants = new WardOverrideGrants();
        static readonly Dictionary<string, Binding> bindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
        static readonly WardConfirmation confirmation = new WardConfirmation();
        // Status requests are event-driven (see WardStatusSchedule): each one is a Crucible Operation whose request id the
        // authority keeps for the session, so a per-frame poll would exhaust the shared request ledger.
        static readonly WardStatusSchedule schedule = new WardStatusSchedule();
        static volatile bool settingsChanged;
        static Task<OperationResult<IReadOnlyList<string>>>? pending;
        static long generation = -1, revision, sentRevision;
        static string character = "", queued = "", sentCharacter = "", sentVerb = "";
        static double sentAt;
        static bool wanted;

        sealed class Binding
        {
            public ZNetPeer? Peer;
            public string Character = "";
            public long PlayerId;
            public long Generation;
        }
        sealed class Codec : IOperationCodec<IReadOnlyList<string>, IReadOnlyList<string>>
        {
            public byte[] EncodeRequest(IReadOnlyList<string> value) => RemoteAdmin.EncodeRequest(value);
            public IReadOnlyList<string> DecodeRequest(byte[] bytes) => RemoteAdmin.DecodeRequest(bytes);
            public byte[] EncodeReply(IReadOnlyList<string> value) => RemoteAdmin.EncodeReply(value);
            public IReadOnlyList<string> DecodeReply(byte[] bytes) => RemoteAdmin.DecodeReply(bytes);
        }

        public static void Initialize(ProductRegistration registration)
        {
            generation = CruciblePlugin.Session.Generation;
            character = CharacterId();
            operation = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(OperationId, new System.Version(1, 0)), new Codec(), Apply);
            harmony = new Harmony(Plugin.Guid + ".ward");
            harmony.Patch(AccessTools.Method(typeof(PrivateArea), "HaveLocalAccess"), postfix: new HarmonyMethod(typeof(ThingWard), nameof(AfterLocalAccess)));
            if (Plugin.Settings != null) Plugin.Settings.Changed += OnSettingsChanged;
        }

        // May run off the main thread (BepInEx raises it where the value was set): only flag it for Tick.
        static void OnSettingsChanged() => settingsChanged = true;

        public static bool Run(string[] args)
        {
            if (args.Length == 0 || !args[0].Equals("ward", StringComparison.OrdinalIgnoreCase)) return false;
            if (args.Length > 2 || (args.Length == 2 && !args[1].Equals("on", StringComparison.OrdinalIgnoreCase) && !args[1].Equals("off", StringComparison.OrdinalIgnoreCase)))
            { Print("ward_usage"); return true; }
            if (operation == null || !ThingModules.Active(ThingModule.WorldTools) || CharacterId().Length == 0 || !Connected())
            { Print("ward_unavailable"); return true; }
            var currentGeneration = CruciblePlugin.Session.Generation;
            var currentCharacter = CharacterId();
            if (generation != currentGeneration) { Reset(); generation = currentGeneration; character = currentCharacter; }
            else if (character != currentCharacter) { ResetClient(); character = currentCharacter; }
            wanted = args.Length == 1 ? !wanted : args[1].Equals("on", StringComparison.OrdinalIgnoreCase);
            revision++;
            queued = wanted ? "on" : "off";
            if (!wanted) { confirmation.Clear(); schedule.Clear(); }
            return true;
        }

        static OperationExecution<IReadOnlyList<string>> Apply(AuthorityContext context, IReadOnlyList<string> request)
        {
            var net = ZNet.instance;
            if (!net || !net.IsServer() || !ThingServer.Active || !ThingModules.ProductActive(out _) || Plugin.Settings == null
                || context.Session.Generation != CruciblePlugin.Session.Generation)
                return OperationExecution<IReadOnlyList<string>>.Rejected("ThingInactive");
            if (request.Count != 2 || (request[0] != "on" && request[0] != "off" && request[0] != "status"))
                return OperationExecution<IReadOnlyList<string>>.Rejected("MalformedWard");
            var peer = !context.IsLocal && long.TryParse(context.SenderId, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid)
                ? net.GetPeers().FirstOrDefault(p => p != null && p.IsReady() && p.m_uid == uid) : null;
            var id = context.IsLocal ? "host" : peer?.m_socket?.GetHostName() ?? "";
            var admin = context.IsLocal || (context.IsAdmin && id.Length > 0 && net.IsAdmin(id));
            var role = net.IsDedicated() ? ThingRole.DedicatedServer : net.GetPeers().Count > 0 ? ThingRole.Host : ThingRole.SinglePlayer;
            var verdict = AdminCheck.Decide(ThingAction.WardOverride, new RequestFacts(role, context.IsLocal, admin, false), Plugin.Settings.Rules());
            if (!AdminCheck.Permits(verdict)) { context.MarkDecisionMade(); Revoke(id); return OperationExecution<IReadOnlyList<string>>.Success(new[] { "off", request[1], AdminCheck.TextKey(verdict) }); }
            if (!Resolve(id, peer, out var player) || CharacterId(player!) != request[1])
            { context.MarkDecisionMade(); Revoke(id); return OperationExecution<IReadOnlyList<string>>.Success(new[] { "off", request[1], "ward_unavailable" }); }
            context.MarkDecisionMade();
            var actor = new Actor(id, player!.GetPlayerName());
            if (request[0] == "off")
            {
                Revoke(id);
                ThingServer.Audit(actor, ThingAction.WardOverride, default, AuditOutcome.Done, "session override off");
            }
            else if (request[0] == "on")
            {
                grants.Grant(id);
                bindings[id] = new Binding { Peer = peer, Character = request[1], PlayerId = player.GetPlayerID(), Generation = context.Session.Generation };
                ThingServer.Audit(actor, ThingAction.WardOverride, default, AuditOutcome.Done, "session override on");
            }
            var enabled = grants.Has(id) && bindings.TryGetValue(id, out var binding) && ReferenceEquals(binding.Peer, peer)
                && binding.Generation == context.Session.Generation && binding.Character == request[1] && binding.PlayerId == player.GetPlayerID();
            return OperationExecution<IReadOnlyList<string>>.Success(new[] { enabled ? "on" : "off", request[1], enabled ? "ward_on" : "ward_off" });
        }

        static bool Resolve(string id, ZNetPeer? peer, out Player? player)
        {
            player = null;
            var net = ZNet.instance;
            if (!net || !net.IsServer() || id.Length == 0) return false;
            if (peer == null) { if (id != "host" || net.IsDedicated()) return false; player = Player.m_localPlayer; }
            else
            {
                if (!peer.IsReady() || peer.m_rpc == null || !peer.m_rpc.IsConnected() || peer.m_socket == null || !net.GetPeers().Any(p => ReferenceEquals(p, peer))
                    || peer.m_socket.GetHostName() != id) return false;
                player = ZNetScene.instance?.FindInstance(peer.m_characterID)?.GetComponent<Player>();
            }
            return player && player.GetPlayerID() != 0 && CharacterId(player).Length > 0;
        }

        public static void Tick()
        {
            var currentGeneration = CruciblePlugin.Session.Generation;
            var currentCharacter = CharacterId();
            if (generation != currentGeneration)
            {
                Reset(); generation = currentGeneration; character = currentCharacter;
            }
            if (ZNet.instance && ZNet.instance.IsServer())
            {
                foreach (var id in grants.Prune(id => Plugin.Settings != null && Plugin.Settings.Get(ThingSettings.World.WardOverrideAllowed)
                    && ThingServer.Active && ThingModules.ProductActive(out _) && bindings.TryGetValue(id, out var binding)
                    && binding.Generation == currentGeneration
                    && (binding.Peer == null || ZNet.instance.IsAdmin(id)) && Resolve(id, binding.Peer, out var player)
                    && CharacterId(player!) == binding.Character && player!.GetPlayerID() == binding.PlayerId))
                {
                    bindings.Remove(id);
                    ThingServer.Audit(new Actor(id, ""), ThingAction.WardOverride, default, AuditOutcome.Done, "session override revoked");
                }
            }
            if (currentCharacter != character || !ThingModules.Active(ThingModule.WorldTools) || !Connected())
            {
                ResetClient(); character = currentCharacter; return;
            }
            if (settingsChanged)
            {
                settingsChanged = false;
                // The synced server switch is also checked locally in AfterLocalAccess, so turning it off stops the bypass
                // at once; the status request then lets the server confirm or revoke the grant.
                if (Plugin.Settings == null || !Plugin.Settings.Get(ThingSettings.World.WardOverrideAllowed)) confirmation.Clear();
                schedule.Request();
            }
            var now = Time.realtimeSinceStartupAsDouble;
            if (pending != null && pending.IsCompleted)
            {
                var completed = pending; pending = null;
                if (sentRevision == revision && sentCharacter == character)
                {
                    var reply = completed.Status == TaskStatus.RanToCompletion && completed.Result.State == OperationState.Succeeded ? completed.Result.Reply : null;
                    if (reply != null && reply.Count == 3 && reply[1] == character && (reply[0] == "on" || reply[0] == "off"))
                    {
                        if (wanted && reply[0] == "on") { confirmation.Confirm(generation, character, sentAt); schedule.Confirmed(sentAt); }
                        else { wanted = false; confirmation.Clear(); schedule.Clear(); }
                        if (sentVerb != "status" || reply[0] == "off") Print(reply[2]);
                    }
                    else { wanted = false; confirmation.Clear(); schedule.Clear(); Print("ward_unavailable"); }
                }
            }
            if (pending != null) return;
            var verb = queued.Length > 0 ? queued : schedule.Due(wanted, now) ? "status" : "";
            if (verb.Length == 0) return;
            queued = ""; sentVerb = verb; sentCharacter = character; sentRevision = revision; sentAt = now; schedule.Sent();
            pending = CruciblePlugin.RequestClient.InvokeAsync<IReadOnlyList<string>, IReadOnlyList<string>>(OperationId, new[] { verb, character }, CancellationToken.None);
        }

        static void AfterLocalAccess(ref bool __result)
        {
            if (!__result && wanted && ThingModules.Active(ThingModule.WorldTools) && Connected() && Plugin.Settings != null
                && Plugin.Settings.Get(ThingSettings.World.WardOverrideAllowed)
                && confirmation.Allows(CruciblePlugin.Session.Generation, CharacterId(), Time.realtimeSinceStartupAsDouble)) __result = true;
        }

        static string CharacterId() => CharacterId(Player.m_localPlayer);
        static string CharacterId(Player? player)
        {
            if (!player) return "";
            var view = player.GetComponent<ZNetView>();
            if (!view || !view.IsValid()) return "";
            // Tick asks every frame; ZDOID.ToString allocates, so keep the text of the last id.
            var uid = view.GetZDO().m_uid;
            if (lastUidText == null || uid != lastUid) { lastUid = uid; lastUidText = uid.ToString(); }
            return lastUidText;
        }
        static ZDOID lastUid;
        static string? lastUidText;
        static bool Connected()
        {
            var net = ZNet.instance;
            if (!net) return false;
            if (net.IsServer()) return Player.m_localPlayer;
            var peer = net.GetServerPeer();
            return peer != null && peer.IsReady() && peer.m_rpc != null && peer.m_rpc.IsConnected();
        }
        static void Revoke(string id) { grants.Revoke(id); bindings.Remove(id); }
        static void Print(string key) => global::Console.instance?.Print(ThingText.Text(key));
        static void ResetClient() { confirmation.Clear(); schedule.Clear(); pending = null; wanted = false; queued = ""; revision++; }
        static void Reset() { ResetClient(); grants.Clear(); bindings.Clear(); }
        public static void Shutdown() { if (Plugin.Settings != null) Plugin.Settings.Changed -= OnSettingsChanged; settingsChanged = false; operation?.Dispose(); operation = null; harmony?.UnpatchSelf(); harmony = null; Reset(); generation = -1; character = ""; }
    }
}
