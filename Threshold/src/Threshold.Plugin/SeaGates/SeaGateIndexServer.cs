// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGateIndexServer.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Threshold.Portals;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The server's half of <see cref="SeaGateIndex"/>: the gates built from the pillars in
    /// <see cref="SeaGateScan"/>'s index, and the peers waiting for a list. Requests are answered in the next frame from
    /// the index, once it holds the whole world (a second or two after the world loaded); nothing is searched for on
    /// request. Each peer gets only the gates its player may target, by the portals' own access rule, so a private
    /// gate's position is never sent to a player who could not sail to it. A request carries the version of the list the
    /// peer already holds (<see cref="ListVersion"/>), and a list that has not changed since is not sent again. The
    /// gates and the id map are built again only when the pillars changed (<see cref="SeaGateScan.Fold"/>), checked at
    /// most once a frame.</summary>
    internal static class SeaGateIndexServer
    {
        // Peer -> the version of the list it holds (0: none).
        private static readonly Dictionary<long, long> waiting = new Dictionary<long, long>();

        // The gates and the pillars by gate id last built, the pillar fold they were built from, the frame it was read.
        private static readonly List<SeaGateInfo> gates = new List<SeaGateInfo>();
        private static readonly Dictionary<long, ZDO> byId = new Dictionary<long, ZDO>();
        private static readonly List<SeaGateInfo> visible = new List<SeaGateInfo>();
        private static long builtFold;
        private static int readFrame = -1;

        internal static void Reset()
        {
            waiting.Clear();
            builtFold = 0L;
            readFrame = -1;
        }

        /// <summary>A peer (or this machine's own routed id, for a host's own map) wants the list.</summary>
        internal static void Enqueue(long peer, long held) => waiting[peer] = held;

        /// <summary>Every frame on the server: advance the pass over the loaded world, answer the waiting peers once the
        /// index is complete.</summary>
        internal static void Tick()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || ZRoutedRpc.instance == null)
                return;
            SeaGateScan.Step();
            if (waiting.Count == 0 || !SeaGateScan.Seeded)
                return;
            AnswerWaiting(BuildGates());
        }

        /// <summary>Both receivers copy the list at once (the snapshot, the package), so one list serves every peer.</summary>
        private static void AnswerWaiting(List<SeaGateInfo> gates)
        {
            long self = ZRoutedRpc.instance.m_id;
            foreach (KeyValuePair<long, long> asked in waiting)
            {
                VisibleTo(gates, asked.Key);
                long version = ListVersion(visible);
                if (asked.Key == self)
                    SeaGateIndex.SetSnapshot(visible);
                else if (version != asked.Value)
                    SeaGateIndex.Send(asked.Key, visible, version);
            }
            waiting.Clear();
        }

        private static void VisibleTo(List<SeaGateInfo> gates, long peer)
        {
            long playerId = SenderIdentity.PlayerId(peer);
            bool isAdmin = SenderIdentity.IsAdmin(peer);
            visible.Clear();
            foreach (SeaGateInfo gate in gates)
            {
                if (GateAccess.MayTarget(gate.Mode, gate.Owner, playerId, isAdmin))
                    visible.Add(gate);
            }
        }

        /// <summary>One number for a list as a peer gets it: every field of every gate folded in order, never 0 (0 is
        /// "none held").</summary>
        internal static long ListVersion(List<SeaGateInfo> gates)
        {
            ListHash hash = ListHash.Start(gates.Count);
            foreach (SeaGateInfo gate in gates)
            {
                hash.Add(gate.Id);
                hash.Add(gate.Position.x, gate.Position.y, gate.Position.z);
                hash.Add(gate.Name != null ? gate.Name.GetStableHashCode() : 0);
                hash.Add((int)gate.Mode);
                hash.Add(gate.Owner);
            }
            return hash.Value;
        }

        /// <summary>Every mutual pair among the pillars, once, by its anchor. The list is kept: read it before the next
        /// frame.</summary>
        internal static List<SeaGateInfo> BuildGates()
        {
            Refresh();
            return gates;
        }

        /// <summary>A gate id to its two pillars, from the index; only when they name each other.</summary>
        internal static bool TryFind(long gateId, out ZDO anchor, out ZDO partner)
        {
            anchor = null;
            partner = null;
            Refresh();
            if (!byId.TryGetValue(gateId, out ZDO first) || !byId.TryGetValue(SeaGateFields.GetPartner(first), out ZDO second))
                return false;
            if (SeaGateFields.GetId(first) != gateId || !SeaGateFields.IsMutual(first, second) || !SeaGateFields.IsAnchor(first, second))
                return false;
            anchor = first;
            partner = second;
            return true;
        }

        /// <summary>Reads the pillars once a frame and builds the id map and the gates again only when they changed.</summary>
        private static void Refresh()
        {
            if (readFrame == Time.frameCount)
                return;
            readFrame = Time.frameCount;
            List<ZDO> pillars = SeaGateScan.Pillars();
            if (SeaGateScan.Fold == builtFold)
                return;
            builtFold = SeaGateScan.Fold;
            FillById(pillars);
            FillGates(pillars);
        }

        private static void FillById(List<ZDO> pillars)
        {
            byId.Clear();
            foreach (ZDO pillar in pillars)
            {
                long id = SeaGateFields.GetId(pillar);
                if (id != 0L)
                    byId[id] = pillar;
            }
        }

        private static void FillGates(List<ZDO> pillars)
        {
            gates.Clear();
            foreach (ZDO anchor in pillars)
            {
                if (!byId.TryGetValue(SeaGateFields.GetPartner(anchor), out ZDO partner))
                    continue;
                if (SeaGateFields.IsMutual(anchor, partner) && SeaGateFields.IsAnchor(anchor, partner))
                    gates.Add(ToInfo(anchor, partner));
            }
        }

        private static SeaGateInfo ToInfo(ZDO anchor, ZDO partner)
        {
            return new SeaGateInfo(SeaGateFields.GetId(anchor), (anchor.GetPosition() + partner.GetPosition()) * 0.5f,
                SeaGateFields.GetName(anchor), GateAccess.GetMode(anchor), GateAccess.GetOwner(anchor));
        }
    }
}
