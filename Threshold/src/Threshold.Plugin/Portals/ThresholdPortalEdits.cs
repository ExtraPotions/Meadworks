// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: access mode cycle and hub edits (T12b, TH05). Behavior as Wayfare's ModeCycle (GPL-3.0-or-later,
// the reference): the request goes to the server, which resolves who asked (never the portal's owner machine), decides
// with Threshold.Core's PortalEdits and writes where it owns the ZDO or nobody does; otherwise it forwards the decided
// fields to the ZDO's owner, which takes them only from the server. The request is a Crucible operation (C02).
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Threshold.Access;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Localization;
using ExtraPotions.Threshold.Portals;
using ExtraPotions.Threshold.Runtime.Travel;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Threshold.Runtime.Portals
{
    public static class ThresholdPortalEdits
    {
        public const string OperationId = "meadworks.threshold.portal";
        const string FieldsRpc = "threshold.SetFields";

        static readonly AccessTools.FieldRef<TeleportWorld, ZNetView> View = AccessTools.FieldRefAccess<TeleportWorld, ZNetView>("m_nview");
        static readonly int ModeHash = PortalFields.ModeKey.GetStableHashCode();
        static readonly int OwnerHash = PortalFields.OwnerKey.GetStableHashCode();
        static readonly int HubHash = PortalFields.HubKey.GetStableHashCode();

        static IDisposable? operation;
        static Task<OperationResult<EditReply>>? pending;

        /// <summary>Called after the server answered an edit this client asked for (the window rebuilds).</summary>
        public static Action? Answered { get; set; }

        sealed class Codec : IOperationCodec<PortalEdit, EditReply>
        {
            public byte[] EncodeRequest(PortalEdit value) => PortalEditCodec.EncodeRequest(value);
            public PortalEdit DecodeRequest(byte[] bytes) => PortalEditCodec.DecodeRequest(bytes);
            public byte[] EncodeReply(EditReply value) => PortalEditCodec.EncodeReply(value);
            public EditReply DecodeReply(byte[] bytes) => PortalEditCodec.DecodeReply(bytes);
        }

        sealed class HubNameInput : TextReceiver
        {
            readonly PortalId portal;
            readonly string current;
            internal HubNameInput(PortalId portal, string current) { this.portal = portal; this.current = current; }
            public string GetText() => current;
            public void SetText(string text) => Request(PortalEdit.SetHub(portal, text));
        }

        public static void Initialize(ProductRegistration registration)
        {
            operation = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(OperationId, new System.Version(1, 0)), new Codec(), Answer);
        }

        public static void Patch(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(TeleportWorld), "Awake"), postfix: new HarmonyMethod(typeof(ThresholdPortalEdits), nameof(AfterAwake)));
            harmony.Patch(AccessTools.Method(typeof(TeleportWorld), nameof(TeleportWorld.Interact), new[] { typeof(Humanoid), typeof(bool), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(ThresholdPortalEdits), nameof(BeforeInteract)));
            harmony.Patch(AccessTools.Method(typeof(TeleportWorld), nameof(TeleportWorld.GetHoverText)),
                postfix: new HarmonyMethod(typeof(ThresholdPortalEdits), nameof(AfterHoverText)));
            harmony.Patch(AccessTools.Method(typeof(TeleportWorld), nameof(TeleportWorld.SetText), new[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(ThresholdPortalEdits), nameof(BeforeSetText)));
            harmony.Patch(AccessTools.Method(typeof(Piece), nameof(Piece.SetCreator)),
                prefix: new HarmonyMethod(typeof(ThresholdPortalEdits), nameof(BeforeSetCreator)),
                postfix: new HarmonyMethod(typeof(ThresholdPortalEdits), nameof(AfterSetCreator)));
        }

        /// <summary>A tag change on a Private or Admin portal goes to the server, which allows only the owner or an admin
        /// (user decision 7); a Public portal keeps the game's own tag path.</summary>
        static bool BeforeSetText(TeleportWorld __instance, string text)
        {
            try
            {
                var settings = Plugin.Settings;
                if (settings == null || !ThresholdModules.PortalRoutingActive()) return true;
                var entry = ThresholdTravel.EntryOf(__instance);
                if (entry == null || PortalAccessRules.EffectiveMode(entry.Mode, settings.Get(ThresholdSettings.Access.AccessModesEnabled)) == AccessMode.Public) return true;
                Request(PortalEdit.SetTag(entry.Id, text ?? "", Splatform.PlatformManager.DistributionPlatform.LocalUser.PlatformUserID.ToString()));
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("Threshold: tag change check failed, the tag is not changed: " + e.GetType().Name);
                return false;
            }
        }

        /// <summary>Whether the game is setting the creator now (a placement): true only while no creator is stored.</summary>
        static void BeforeSetCreator(Piece __instance, out bool __state) => __state = __instance != null && __instance.GetCreator() == 0L;

        /// <summary>A portal placed now gets the server's Default Access Mode (user decision 2); existing portals never.</summary>
        static void AfterSetCreator(Piece __instance, long uid, bool __state)
        {
            try
            {
                var settings = Plugin.Settings;
                if (__instance == null || settings == null || !ThresholdModules.PortalRoutingActive()) return;
                var portal = __instance.GetComponentInChildren<TeleportWorld>();
                var view = __instance.GetComponent<ZNetView>();
                if (portal == null || view == null || !view.IsValid() || !view.IsOwner() || __instance.GetCreator() != uid) return;
                var zdo = view.GetZDO();
                if (!PortalPlacement.Applies(__state, zdo.GetInt(ModeHash, PortalFields.NoMode))) return;
                var write = PortalPlacement.Fields(settings.Get(ThresholdSettings.Access.DefaultAccessMode), uid, settings.Get(ThresholdSettings.Access.AccessModesEnabled));
                if (write == null) return;
                Apply(zdo, write.Mode.HasValue, write.Mode.HasValue ? (int)write.Mode.Value : 0, write.Owner.HasValue, write.Owner ?? 0L, false, "");
            }
            catch (Exception e) { Plugin.Log?.Warning("Threshold: default access mode on placement: " + e.GetType().Name); }
        }

        /// <summary>Client: sends one edit to the server (one at a time).</summary>
        public static bool Request(PortalEdit edit)
        {
            if (pending != null || !ThresholdModules.PortalRoutingActive()) return false;
            pending = CruciblePlugin.RequestClient.InvokeAsync<PortalEdit, EditReply>(OperationId, edit, CancellationToken.None);
            return true;
        }

        /// <summary>Opens the game's text input for a hub name on <paramref name="portal"/> (Set as hub, or rename).</summary>
        public static void AskHubName(TeleportWorld portal)
        {
            var entry = ThresholdTravel.EntryOf(portal);
            if (entry == null || TextInput.instance == null) return;
            var receiver = new HubNameInput(entry.Id, entry.HubName);
            string topic = ThresholdText.Text(ThresholdTextKeys.PickerHubName);
            var source = Plugin.Source;
            if (source == null || ExtraPotions.Meadworks.Crucible.UI.KitDialogs.Input(source, topic, "", receiver.GetText(),
                receiver.SetText, characterLimit: HubRules.MaxNameLength) == null)
                TextInput.instance.RequestText(receiver, topic, HubRules.MaxNameLength);
        }

        /// <summary>From the plugin's Update: shows the answer to this client's edit.</summary>
        public static void Tick()
        {
            if (pending == null || !pending.IsCompleted) return;
            var done = pending;
            pending = null;
            var player = Player.m_localPlayer;
            if (done.Status == TaskStatus.RanToCompletion && done.Result.State == OperationState.Succeeded)
            {
                var reply = done.Result.Reply;
                if (reply.Refusal != EditRefusal.None && player != null)
                    player.Message(MessageHud.MessageType.Center, reply.Refusal == EditRefusal.LimitReached
                        ? ThresholdText.Format(PortalEdits.TextKey(reply.Refusal), reply.Argument) : ThresholdText.Text(PortalEdits.TextKey(reply.Refusal)));
            }
            else if (player != null)
            {
                player.Message(MessageHud.MessageType.Center, ThresholdText.Text(ThresholdTextKeys.PortalEditNoReply));
            }
            ThresholdPortals.RefreshNow();
            Answered?.Invoke();
        }

        /// <summary>Server: decide with the sender's identity resolved here, then write or forward.</summary>
        static OperationExecution<EditReply> Answer(AuthorityContext context, PortalEdit edit)
        {
            var net = ZNet.instance;
            var settings = Plugin.Settings;
            if (net == null || !net.IsServer() || ZDOMan.instance == null || settings == null || !ThresholdModules.ServerActive()
                || context.Session.Generation != CruciblePlugin.Session.Generation)
                return OperationExecution<EditReply>.Rejected("ThresholdInactive");
            ThresholdPortals.RefreshNow();
            var world = ThresholdPortals.Current;
            var target = world.FirstOrDefault(p => p.Id.Equals(edit.Portal));
            var zdo = ZDOMan.instance.GetZDO(new ZDOID(edit.Portal.User, edit.Portal.Id));
            Actor(context, net, out long actor, out bool admin);
            var facts = new EditFacts(zdo != null ? target : null, world, actor, admin, zdo != null ? zdo.GetLong(ZDOVars.s_creator, 0L) : 0L,
                settings.Get(ThresholdSettings.Access.AccessModesEnabled), settings.Get(ThresholdSettings.Hubs.HubEditing), settings.Get(ThresholdSettings.Hubs.MaxHubs));
            var outcome = PortalEdits.Decide(edit, facts);
            if (outcome.Allowed && outcome.Writes.Count > 0)
            {
                context.MarkDecisionMade();
                foreach (var write in outcome.Writes) Write(write);
                ThresholdPortals.RefreshNow();
            }
            return OperationExecution<EditReply>.Success(new EditReply(outcome.Refusal, outcome.Argument));
        }

        /// <summary>The asking player as the server sees them: the host's own player for a local call, else the peer's
        /// character ZDO (the game never fills a remote peer's player id); admin from the server's admin list.</summary>
        static void Actor(AuthorityContext context, ZNet net, out long playerId, out bool admin)
        {
            playerId = 0L;
            admin = false;
            if (context.IsLocal)
            {
                var local = Player.m_localPlayer;
                playerId = local != null ? local.GetPlayerID() : 0L;
                admin = net.LocalPlayerIsAdminOrHost();
                return;
            }
            if (!long.TryParse(context.SenderId, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid)) return;
            var peer = net.GetPeer(uid);
            if (peer == null || !peer.IsReady()) return;
            if (peer.m_playerID != 0L) playerId = peer.m_playerID;
            else if (!peer.m_characterID.IsNone())
            {
                var character = ZDOMan.instance.GetZDO(peer.m_characterID);
                playerId = character != null ? character.GetLong(ZDOVars.s_playerID, 0L) : 0L;
            }
            var host = peer.m_socket != null ? peer.m_socket.GetHostName() : "";
            admin = context.IsAdmin && host.Length > 0 && net.IsAdmin(host);
        }

        static void Write(PortalWrite write)
        {
            var zdo = ZDOMan.instance.GetZDO(new ZDOID(write.Portal.User, write.Portal.Id));
            if (zdo == null) return;
            if (write.Tag != null)
            {
                // The game's own tag path on the ZDO's owner; written here where the server owns it or nobody does.
                if (zdo.IsOwner() || !zdo.HasOwner())
                {
                    zdo.UpdateConnection(ZDOExtraData.ConnectionType.Portal, ZDOID.None);
                    zdo.Set(ZDOVars.s_tag, write.Tag);
                    zdo.Set(ZDOVars.s_tagauthor, write.TagAuthor ?? "");
                }
                else ZRoutedRpc.instance.InvokeRoutedRPC(zdo.GetOwner(), zdo.m_uid, "RPC_SetTag", write.Tag, write.TagAuthor ?? "");
                if (!write.Mode.HasValue && !write.Owner.HasValue && write.Hub == null) return;
            }
            if (zdo.IsOwner() || !zdo.HasOwner())
            {
                Apply(zdo, write.Mode.HasValue, write.Mode.HasValue ? (int)write.Mode.Value : 0, write.Owner.HasValue, write.Owner ?? 0L, write.Hub != null, write.Hub ?? "");
                return;
            }
            var package = new ZPackage();
            package.Write(write.Mode.HasValue);
            package.Write(write.Mode.HasValue ? (int)write.Mode.Value : 0);
            package.Write(write.Owner.HasValue);
            package.Write(write.Owner ?? 0L);
            package.Write(write.Hub != null);
            package.Write(write.Hub ?? "");
            ZRoutedRpc.instance.InvokeRoutedRPC(zdo.GetOwner(), zdo.m_uid, FieldsRpc, package);
        }

        static void Apply(ZDO zdo, bool hasMode, int mode, bool hasOwner, long owner, bool hasHub, string hub)
        {
            if (hasMode) zdo.Set(ModeHash, mode);
            if (hasOwner) zdo.Set(OwnerHash, owner);
            if (hasHub) zdo.Set(HubHash, HubRules.NormalizeName(hub));
        }

        static void AfterAwake(TeleportWorld __instance)
        {
            var view = __instance != null ? View(__instance) : null;
            if (view == null || !view.IsValid()) return;
            view.Register<ZPackage>(FieldsRpc, (sender, package) => OnFields(view, sender, package));
        }

        /// <summary>The ZDO's owner: fields the server decided, taken only from the server.</summary>
        static void OnFields(ZNetView view, long sender, ZPackage package)
        {
            try
            {
                var net = ZNet.instance;
                if (net == null || net.IsServer() || package == null || !view.IsValid() || !view.IsOwner()) return;
                var server = net.GetServerPeer();
                if (server == null || server.m_uid != sender) return;
                bool hasMode = package.ReadBool(); int mode = package.ReadInt();
                bool hasOwner = package.ReadBool(); long owner = package.ReadLong();
                bool hasHub = package.ReadBool(); string hub = package.ReadString();
                Apply(view.GetZDO(), hasMode, mode, hasOwner, owner, hasHub, hub);
            }
            catch (Exception e) { Plugin.Log?.Warning("Threshold: portal fields from the server rejected: " + e.GetType().Name); }
        }

        /// <summary>The mode cycle key on a portal cycles its access mode; the tag edit key on a hub renames it; plain Use
        /// keeps the game's tag input (U1, U6).</summary>
        static bool BeforeInteract(TeleportWorld __instance, Humanoid human, bool hold, ref bool __result)
        {
            try
            {
                var settings = Plugin.Settings;
                if (hold || settings == null || !(human is Player player) || player != Player.m_localPlayer || !ThresholdModules.PortalRoutingActive()) return true;
                bool cycle = settings.Shortcut(ThresholdSettings.Picker.ModeCycleKey).IsPressed();
                bool rename = !cycle && settings.Shortcut(ThresholdSettings.Picker.TagEditKey).IsPressed();
                if (!cycle && !rename) return true;
                var entry = ThresholdTravel.EntryOf(__instance);
                if (entry == null) return true;
                if (rename && !entry.IsHub) return true;
                __result = true;
                if (!PrivateArea.CheckAccess(__instance.transform.position))
                {
                    player.Message(MessageHud.MessageType.Center, "$piece_noaccess");
                    return false;
                }
                if (cycle) Request(PortalEdit.Cycle(entry.Id));
                else AskHubName(__instance);
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("Threshold: portal interaction failed, the game's runs: " + e);
                return true;
            }
        }

        /// <summary>Hover lines: the hub name, walk in to choose, and the keys of the mode cycle and hub rename.</summary>
        static void AfterHoverText(TeleportWorld __instance, ref string __result)
        {
            try
            {
                var settings = Plugin.Settings;
                if (settings == null || !ThresholdModules.PortalRoutingActive()) return;
                var entry = ThresholdTravel.EntryOf(__instance);
                if (entry == null) return;
                var lines = new System.Text.StringBuilder(__result ?? "");
                if (entry.IsHub) lines.Append('\n').Append(ThresholdText.Format(ThresholdTextKeys.HoverHub, entry.HubName));
                lines.Append('\n').Append(ThresholdText.Text(ThresholdModules.Mode == TeleportMode.TargetTeleport ? ThresholdTextKeys.HoverTarget : ThresholdTextKeys.HoverWalkIn));
                if (settings.Get(ThresholdSettings.Access.AccessModesEnabled))
                    lines.Append('\n').Append(ThresholdText.Format(ThresholdTextKeys.HoverCycleMode, settings.Shortcut(ThresholdSettings.Picker.ModeCycleKey).ToString(),
                        ThresholdText.Text(ThresholdTextKeys.Mode(entry.Mode))));
                if (entry.IsHub)
                    lines.Append('\n').Append(ThresholdText.Format(ThresholdTextKeys.HoverSetTag, settings.Shortcut(ThresholdSettings.Picker.TagEditKey).ToString()));
                __result = lines.ToString();
            }
            catch (Exception e) { Plugin.Log?.Warning("Threshold: hover text: " + e.GetType().Name); }
        }

        public static void Shutdown()
        {
            operation?.Dispose();
            operation = null;
            pending = null;
            Answered = null;
        }
    }
}
