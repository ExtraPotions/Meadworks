// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGateRegistry.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Threshold.Portals;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>One gate whose two pillars are both loaded on this machine and name each other.</summary>
    public sealed class LoadedGate
    {
        public readonly SeaGatePillar Anchor;
        public readonly SeaGatePillar Partner;
        public readonly GateGeometry Geometry;

        public LoadedGate(SeaGatePillar anchor, SeaGatePillar partner)
        {
            Anchor = anchor;
            Partner = partner;
            Geometry = new GateGeometry(anchor.transform.position, partner.transform.position);
        }

        /// <summary>The gate's id: its anchor's id.</summary>
        public long Id => Anchor.Id;
        public ZDO AnchorZdo => Anchor.Zdo;
        public int Sides => SeaGateFields.GetSides(AnchorZdo);
        public string Name => SeaGateFields.GetName(AnchorZdo);

        public bool IsAlive => Anchor != null && Partner != null && Anchor.IsValid && Partner.IsValid &&
                               SeaGateFields.IsMutual(Anchor.Zdo, Partner.Zdo);
    }

    /// <summary>The sea gate pillars loaded on this machine (each <see cref="SeaGatePillar"/> adds itself on Awake and
    /// leaves on destroy) and the gates they form. The gate list is rebuilt when a pillar comes or goes; otherwise, since
    /// pairing changes arrive as ZDO writes, once a second the loaded pillars' ids and data revisions are folded into
    /// one number and the list is rebuilt only when that changed, so the same <see cref="LoadedGate"/> objects stay while
    /// nothing did. Only loaded gates: the complete list lives on the server (<see cref="SeaGateIndex"/>).</summary>
    public static class SeaGateRegistry
    {
        private const float RebuildSeconds = 1f;

        private static readonly List<SeaGatePillar> pillars = new List<SeaGatePillar>();
        private static readonly List<LoadedGate> gates = new List<LoadedGate>();
        private static bool dirty = true;
        private static float builtAt;
        private static long builtFold;

        public static IReadOnlyList<SeaGatePillar> Pillars => pillars;

        public static IReadOnlyList<LoadedGate> Gates
        {
            get
            {
                if (dirty || Time.time - builtAt > RebuildSeconds)
                    Refresh();
                return gates;
            }
        }

        internal static void Add(SeaGatePillar pillar)
        {
            if (!pillars.Contains(pillar))
                pillars.Add(pillar);
            dirty = true;
        }

        internal static void Remove(SeaGatePillar pillar)
        {
            pillars.Remove(pillar);
            dirty = true;
        }

        /// <summary>Marks the gate list stale, for a module that just changed a pairing.</summary>
        public static void Invalidate() => dirty = true;

        public static SeaGatePillar FindLoaded(long id)
        {
            if (id == 0L)
                return null;
            foreach (SeaGatePillar pillar in pillars)
            {
                if (pillar != null && pillar.IsValid && pillar.Id == id)
                    return pillar;
            }
            return null;
        }

        public static LoadedGate FindGate(long gateId)
        {
            foreach (LoadedGate gate in Gates)
            {
                if (gate.Id == gateId && gate.IsAlive)
                    return gate;
            }
            return null;
        }

        /// <summary>The gate a pillar belongs to, as anchor or partner; null when it is unpaired or its partner is not loaded.</summary>
        public static LoadedGate GateOf(SeaGatePillar pillar)
        {
            foreach (LoadedGate gate in Gates)
            {
                if ((gate.Anchor == pillar || gate.Partner == pillar) && gate.IsAlive)
                    return gate;
            }
            return null;
        }

        private static void Refresh()
        {
            bool forced = dirty;
            dirty = false;
            builtAt = Time.time;
            long fold = PillarFold();
            if (!forced && fold == builtFold)
                return;
            builtFold = fold;
            Rebuild();
        }

        /// <summary>Every loaded pillar's id and data revision, in list order (a gone or invalid one as a gap).</summary>
        private static long PillarFold()
        {
            ListHash hash = ListHash.Start(pillars.Count);
            foreach (SeaGatePillar pillar in pillars)
            {
                ZDO zdo = pillar != null && pillar.IsValid ? pillar.Zdo : null;
                if (zdo == null)
                {
                    hash.Add(0L);
                    continue;
                }
                hash.Add(zdo.m_uid.UserID);
                hash.Add(zdo.m_uid.ID);
                hash.Add(zdo.DataRevision);
            }
            return hash.Value;
        }

        private static void Rebuild()
        {
            gates.Clear();
            pillars.RemoveAll(p => p == null);
            foreach (SeaGatePillar pillar in pillars)
            {
                if (!pillar.IsValid)
                    continue;
                SeaGatePillar partner = FindLoaded(SeaGateFields.GetPartner(pillar.Zdo));
                if (partner == null || !SeaGateFields.IsMutual(pillar.Zdo, partner.Zdo) || !SeaGateFields.IsAnchor(pillar.Zdo, partner.Zdo))
                    continue;
                gates.Add(new LoadedGate(pillar, partner));
            }
        }
    }
}
