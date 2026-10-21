// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the server portal list and its sync (design D1, T11). Behavior as Wayfare's PortalRegistry and
// PortalSync (GPL-3.0-or-later, the reference): the server's list is the only complete one, so a client asks the server
// with the version it holds, and the server answers only when its list differs. Asks follow PortalAskSchedule: on join and
// when the picker opens, every 5 seconds while it is open, once a minute otherwise.
// The ask is a Crucible operation (C02 session and sender identity); the wire form and pages are Threshold.Core's.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Threshold.Portals;
using ExtraPotions.Threshold.Runtime.Map;
using ExtraPotions.Threshold.Runtime.Travel;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Threshold.Runtime.Portals
{
    public static class ThresholdPortals
    {
        public const string OperationId = "meadworks.threshold.portals";
        const float RebuildSeconds = 1f;

        static readonly int ModeHash = PortalFields.ModeKey.GetStableHashCode();
        static readonly int OwnerHash = PortalFields.OwnerKey.GetStableHashCode();
        static readonly int HubHash = PortalFields.HubKey.GetStableHashCode();

        static IDisposable? operation;
        static readonly PortalListAssembly assembly = new PortalListAssembly();
        static readonly Dictionary<int, (string Name, bool AllowsAll)?> prefabs = new Dictionary<int, (string, bool)?>();
        static Task<OperationResult<PortalListPage>>? pending;
        static long session = -1;
        static readonly PortalAskSchedule schedule = new PortalAskSchedule();

        // Server (and host, single player): the last list read, its version and pages, rebuilt at most once a second.
        static IReadOnlyList<PortalEntry> local = Array.Empty<PortalEntry>();
        static IReadOnlyList<IReadOnlyList<PortalEntry>> localPages = Array.Empty<IReadOnlyList<PortalEntry>>();
        static long localVersion;
        static float localBuiltAt = float.NegativeInfinity;
        static ZDOMan? localFrom;

        sealed class Codec : IOperationCodec<PortalListRequest, PortalListPage>
        {
            public byte[] EncodeRequest(PortalListRequest value) => PortalListCodec.EncodeRequest(value);
            public PortalListRequest DecodeRequest(byte[] bytes) => PortalListCodec.DecodeRequest(bytes);
            public byte[] EncodeReply(PortalListPage value) => PortalListCodec.EncodeReply(value);
            public PortalListPage DecodeReply(byte[] bytes) => PortalListCodec.DecodeReply(bytes);
        }

        public static void Initialize(ProductRegistration registration)
        {
            operation = CruciblePlugin.Services.Operations.Register(registration, new OperationDescriptor(OperationId, new System.Version(1, 0)), new Codec(), Answer);
        }

        /// <summary>Every portal Threshold knows now: the server's own list where this machine is the server, else the
        /// last complete list the server sent (empty until the first arrives).</summary>
        public static IReadOnlyList<PortalEntry> Current
        {
            get
            {
                var net = ZNet.instance;
                if (net != null && net.IsServer()) { RebuildLocal(false); return local; }
                return assembly.Portals;
            }
        }

        /// <summary>Changes whenever <see cref="Current"/> was replaced.</summary>
        public static long Revision
        {
            get
            {
                var net = ZNet.instance;
                return net != null && net.IsServer() ? localVersion : assembly.Generation;
            }
        }

        /// <summary>Asks the server at once (the map opened); where this machine is the server, rereads its list.</summary>
        public static void RefreshNow()
        {
            var net = ZNet.instance;
            if (net != null && net.IsServer()) RebuildLocal(true);
            else schedule.Request();
        }

        /// <summary>From the plugin's Update. A client of a server asks while Threshold is active and a character is in
        /// the world, page by page until a list is complete: every 5 s while the picker is open, else once a minute.</summary>
        public static void Tick(bool active)
        {
            if (CruciblePlugin.Session.Generation != session)
            {
                session = CruciblePlugin.Session.Generation;
                ResetSession();
            }
            var net = ZNet.instance;
            if (!active || net == null || net.IsServer() || Player.m_localPlayer == null) return;
            if (pending != null)
            {
                if (!pending.IsCompleted) return;
                var done = pending;
                pending = null;
                if (done.Status == TaskStatus.RanToCompletion && done.Result.State == OperationState.Succeeded && done.Result.Reply != null)
                {
                    bool complete = assembly.Accept(done.Result.Reply);
                    bool morePages = !complete && !done.Result.Reply.IsUnchanged && assembly.Next().Page > 0;
                    if (morePages) schedule.Request();
                }
            }
            var time = Time.realtimeSinceStartupAsDouble;
            if (!schedule.Due(time, ThresholdTravel.InSession || ThresholdMapPins.Showing)) return;
            schedule.Sent(time);
            pending = CruciblePlugin.RequestClient.InvokeAsync<PortalListRequest, PortalListPage>(OperationId, assembly.Next(), CancellationToken.None);
        }

        /// <summary>Server: one page of the current list, or "unchanged" for the version the client holds.</summary>
        static OperationExecution<PortalListPage> Answer(AuthorityContext context, PortalListRequest request)
        {
            var net = ZNet.instance;
            if (net == null || !net.IsServer() || ZDOMan.instance == null || !ThresholdModules.ServerActive()
                || context.Session.Generation != CruciblePlugin.Session.Generation)
                return OperationExecution<PortalListPage>.Rejected("ThresholdInactive");
            RebuildLocal(false);
            if (request.Held == localVersion) return OperationExecution<PortalListPage>.Success(PortalListPage.Unchanged(localVersion));
            int page = request.Page < localPages.Count ? request.Page : 0;
            return OperationExecution<PortalListPage>.Success(new PortalListPage(localVersion, page, localPages.Count, localPages[page]));
        }

        static void RebuildLocal(bool force)
        {
            var zdos = ZDOMan.instance;
            if (zdos == null) return;
            if (!force && localFrom == zdos && Time.unscaledTime - localBuiltAt < RebuildSeconds) return;
            ThresholdPortalDiscovery.EnsureDiscovered();
            var list = ReadLocal(zdos);
            localFrom = zdos;
            localBuiltAt = Time.unscaledTime;
            var version = PortalListVersion.Of(list);
            if (version == localVersion && localPages.Count > 0) return;
            local = list;
            localVersion = version;
            localPages = PortalListPager.Split(list, PortalListCodec.PageBudget);
        }

        /// <summary>This machine's own portal list, complete only where it is the server.</summary>
        static List<PortalEntry> ReadLocal(ZDOMan zdos)
        {
            var portals = new List<PortalEntry>();
            foreach (var zdo in zdos.GetPortalList())
            {
                if (zdo == null || !zdo.IsValid() || !ThresholdPortalDiscovery.IsPortalPrefab(zdo.GetPrefab())) continue;
                var prefab = Prefab(zdo.GetPrefab());
                if (prefab == null) continue;
                var at = zdo.GetPosition();
                var position = new MapPoint(at.x, at.y, at.z);
                if (!position.IsFinite) continue;
                portals.Add(new PortalEntry(new PortalId(zdo.m_uid.UserID, zdo.m_uid.ID), prefab.Value.Name, zdo.GetString(ZDOVars.s_tag),
                    position, PortalFields.Mode(zdo.GetInt(ModeHash, PortalFields.NoMode)), zdo.GetLong(OwnerHash, 0L),
                    zdo.GetString(HubHash, ""), prefab.Value.AllowsAll, zdo.GetRotation().eulerAngles.y));
            }
            return portals;
        }

        static (string Name, bool AllowsAll)? Prefab(int hash)
        {
            if (prefabs.TryGetValue(hash, out var known)) return known;
            var scene = ZNetScene.instance;
            var prefab = scene != null ? scene.GetPrefab(hash) : null;
            if (prefab == null) return null; // not cached: the scene may not know it yet
            var portal = prefab.GetComponentInChildren<TeleportWorld>();
            (string, bool)? value = portal != null ? (prefab.name, portal.m_allowAllItems) : ((string, bool)?)null;
            prefabs[hash] = value;
            return value;
        }

        static void ResetSession()
        {
            pending = null;
            schedule.Reset();
            assembly.Reset();
            local = Array.Empty<PortalEntry>();
            localPages = Array.Empty<IReadOnlyList<PortalEntry>>();
            localVersion = 0L;
            localBuiltAt = float.NegativeInfinity;
            localFrom = null;
            prefabs.Clear();
        }

        public static void Shutdown()
        {
            operation?.Dispose();
            operation = null;
            ResetSession();
            session = -1;
        }
    }
}
