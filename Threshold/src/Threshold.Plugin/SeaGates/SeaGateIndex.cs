// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGateIndex.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

using ExtraPotions.Threshold.Access;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>One gate as the server lists it to clients.</summary>
    public readonly struct SeaGateInfo
    {
        public readonly long Id;
        public readonly Vector3 Position;
        public readonly string Name;
        public readonly AccessMode Mode;
        public readonly long Owner;

        public SeaGateInfo(long id, Vector3 position, string name, AccessMode mode, long owner)
        {
            Id = id;
            Position = position;
            Name = name;
            Mode = mode;
            Owner = owner;
        }
    }

    /// <summary>The server's complete list of gates and the client snapshot of it, <c>threshold.RequestSeaGates</c>/
    /// <c>threshold.SeaGateList</c>, the same shape as Threshold's portal list: a client only ever holds the pillar ZDOs near
    /// it, the server holds all of them (<see cref="SeaGateScan"/>, <see cref="SeaGateIndexServer"/>). A client asks
    /// when the picker opens and every few seconds while sea gate icons are on the map, saying which version of the list
    /// it holds; only the server answers, only when that list has changed, and a client accepts a list only from the
    /// server. Where this machine is the server (a host, single player) the snapshot is filled locally, without a
    /// message.</summary>
    public static class SeaGateIndex
    {
        public const string RequestRpc = "threshold.RequestSeaGates";
        public const string ListRpc = "threshold.SeaGateList";

        private const int MaxGates = 100000;

        private static readonly List<SeaGateInfo> snapshot = new List<SeaGateInfo>();

        // The version of the server's list the snapshot is (sent with every request); 0 while none is held. A server
        // without versions (an older Wayfare) sends none and every list again, as before.
        private static long held;

        // The instance registered on, not a bool: every new session constructs a fresh ZRoutedRpc with empty
        // handler tables, and an unknown routed method is dropped silently.
        private static ZRoutedRpc registeredOn;

        /// <summary>The last list the server sent (or this machine built, where it is the server).</summary>
        public static IReadOnlyList<SeaGateInfo> Gates => snapshot;

        /// <summary>Registers the RPCs (and the gate edits', <see cref="SeaGatePickerEdits"/>) on a new session's
        /// <c>ZRoutedRpc</c> and forgets the last session's gates. Called
        /// from the <c>ZRoutedRpc</c> constructor patch on every machine, a dedicated server included; it also starts
        /// the ticker that runs the server's scan, the picker and the map icons.</summary>
        public static void EnsureRegistered()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || registeredOn == rpc)
                return;
            registeredOn = rpc;
            snapshot.Clear();
            held = 0L;
            SeaGateIndexServer.Reset();
            rpc.Register<long>(RequestRpc, OnRequest);
            rpc.Register<ZPackage>(ListRpc, OnList);
            SeaGatePickerEdits.Register(rpc);
        }

        /// <summary>Asks for the list, unless the one held is still current. No explicit target routes to the server; on
        /// the server itself the request is queued directly.</summary>
        public static void RequestFromServer()
        {
            if (ZNet.instance == null || ZRoutedRpc.instance == null)
                return;
            EnsureRegistered();
            if (ZNet.instance.IsServer())
                SeaGateIndexServer.Enqueue(ZRoutedRpc.instance.m_id, 0L);
            else
                ZRoutedRpc.instance.InvokeRoutedRPC(RequestRpc, held);
        }

        /// <summary>Server: a gate id to its two pillar ZDOs, from the index of every pillar (<see cref="SeaGateScan"/>),
        /// only when they name each other. Never searches the world: a pillar is in the index from the moment it reaches
        /// the server.</summary>
        public static bool TryFindOnServer(long gateId, out ZDO anchor, out ZDO partner)
        {
            anchor = null;
            partner = null;
            if (gateId == 0L || ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null)
                return false;
            return SeaGateIndexServer.TryFind(gateId, out anchor, out partner);
        }

        internal static void SetSnapshot(List<SeaGateInfo> gates)
        {
            snapshot.Clear();
            snapshot.AddRange(gates);
        }

        /// <summary>The list, then its version, which a client from before versions never reads.</summary>
        internal static void Send(long peer, List<SeaGateInfo> gates, long version)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(gates.Count);
            foreach (SeaGateInfo gate in gates)
            {
                pkg.Write(gate.Id);
                pkg.Write(gate.Position);
                pkg.Write(gate.Name ?? "");
                pkg.Write((int)gate.Mode);
                pkg.Write(gate.Owner);
            }
            pkg.Write(version);
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, ListRpc, pkg);
        }

        private static void OnRequest(long sender, long senderHolds)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer())
                return;
            if (SeaGateConfig.On)
                SeaGateIndexServer.Enqueue(sender, senderHolds);
        }

        private static void OnList(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer() || !SenderIdentity.IsFromServer(sender))
                return;
            int count = pkg.ReadInt();
            if (count < 0 || count > MaxGates)
                return;
            List<SeaGateInfo> gates = new List<SeaGateInfo>(count);
            for (int i = 0; i < count; i++)
            {
                gates.Add(new SeaGateInfo(pkg.ReadLong(), pkg.ReadVector3(), pkg.ReadString(),
                    (AccessMode)pkg.ReadInt(), pkg.ReadLong()));
            }
            held = pkg.GetPos() < pkg.Size() ? pkg.ReadLong() : 0L;
            SetSnapshot(gates);
        }
    }
}
