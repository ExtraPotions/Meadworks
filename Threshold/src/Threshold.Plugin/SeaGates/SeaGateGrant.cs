// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGateGrant.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The server's permission for ships to jump through one gate, held by the ship owner's client.</summary>
    public readonly struct JumpGrant
    {
        public readonly long SourceId;
        public readonly long DestId;
        public readonly Vector3 DestAnchor;
        public readonly Vector3 DestPartner;
        public readonly int DestSides;
        public readonly float ReceivedAt;

        public JumpGrant(long sourceId, long destId, Vector3 destAnchor, Vector3 destPartner, int destSides, float receivedAt)
        {
            SourceId = sourceId;
            DestId = destId;
            DestAnchor = destAnchor;
            DestPartner = destPartner;
            DestSides = destSides;
            ReceivedAt = receivedAt;
        }

        /// <summary>The destination gate's frame, anchor first.</summary>
        public GateGeometry DestGeometry => new GateGeometry(DestAnchor, DestPartner);
    }

    /// <summary>Asking the server whether a ship may jump from one gate to the gate its helmsman picked. The ship
    /// owner's client asks (<see cref="RequestRpc"/>, routed to the server, never to a pillar's owner); the server
    /// decides (<see cref="SeaGateGrantServer"/>) and answers with a grant or a denial, which this client keeps per
    /// source gate for <see cref="LifeSeconds"/>. Answers are taken only from the server, so no other client can forge
    /// one. The latest answer wins: a grant clears the gate's denial and a denial clears its grant.</summary>
    public static class SeaGateGrant
    {
        public const string RequestRpc = "threshold.SeaGateRequest";
        public const string GrantRpc = "threshold.SeaGateGrant";
        public const string DenyRpc = "threshold.SeaGateDeny";

        /// <summary>How long a grant or a denial counts after it arrives.</summary>
        public const float LifeSeconds = 10f;

        // At most one request per gate in this time, while an answer is on its way.
        private const float RequestGapSeconds = 2f;

        // Here rather than with the server code: these fields initialise on every machine (EnsureRegistered reads
        // this class's statics), so a client knows the words the server sends it.
        internal static readonly string DeniedClosed = Language.Add("wf_sg_denied_closed", "This sea gate is closed");
        internal static readonly string DeniedPrivate = Language.Add("wf_sg_denied_private", "Only the destination gate's owner may sail there");
        internal static readonly string DeniedAdmin = Language.Add("wf_sg_denied_admin", "Only a server admin may sail to the destination gate");

        private static readonly Dictionary<long, JumpGrant> grants = new Dictionary<long, JumpGrant>();
        private static readonly Dictionary<long, Denial> denials = new Dictionary<long, Denial>();
        private static readonly Dictionary<long, float> requestedAt = new Dictionary<long, float>();

        // The instance registered on, not a bool: every new session constructs a fresh ZRoutedRpc with empty
        // handler tables, and an unknown routed method is dropped silently.
        private static ZRoutedRpc registeredOn;

        public static void EnsureRegistered()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || registeredOn == rpc)
                return;
            registeredOn = rpc;
            Clear();
            rpc.Register<long, long, ZDOID>(RequestRpc, SeaGateGrantServer.OnRequest);
            rpc.Register<ZPackage>(GrantRpc, OnGrant);
            rpc.Register<long, long, string>(DenyRpc, OnDeny);
        }

        /// <summary>Ship owner's client, a ship stopped in the gate with a destination picked: ask the server, unless a
        /// grant for that destination is held or a request is already on its way. The ship goes along so the server
        /// can judge access for its helmsman.</summary>
        public static void Request(LoadedGate gate, long destId, Ship ship)
        {
            if (gate == null || gate.Id == 0L || destId == 0L || ZRoutedRpc.instance == null)
                return;
            long id = gate.Id;
            if (TryGet(id, destId, out _))
                return;
            if (requestedAt.TryGetValue(id, out float sentAt) && Time.time - sentAt < RequestGapSeconds)
                return;
            EnsureRegistered();
            requestedAt[id] = Time.time;
            ZDO shipZdo = ship != null && ship.m_nview != null && ship.m_nview.IsValid() ? ship.m_nview.GetZDO() : null;
            ZRoutedRpc.instance.InvokeRoutedRPC(RequestRpc, id, destId, shipZdo != null ? shipZdo.m_uid : ZDOID.None);
        }

        /// <summary>A grant from this gate to that one, received in the last 10 seconds.</summary>
        public static bool TryGet(long sourceGateId, long destId, out JumpGrant grant)
        {
            if (grants.TryGetValue(sourceGateId, out grant) && grant.DestId == destId && Time.time - grant.ReceivedAt < LifeSeconds)
                return true;
            grant = default;
            return false;
        }

        /// <summary>The denial token for a jump from this gate to that one, received in the last 10 seconds, or null;
        /// read once.</summary>
        public static string TakeDenial(long sourceGateId, long destId)
        {
            if (!denials.TryGetValue(sourceGateId, out Denial denial) || denial.DestId != destId)
                return null;
            denials.Remove(sourceGateId);
            return Time.time - denial.At < LifeSeconds ? denial.Token : null;
        }

        /// <summary>Forgets this gate's answers and pending request, for a new pick.</summary>
        public static void Forget(long sourceGateId)
        {
            grants.Remove(sourceGateId);
            denials.Remove(sourceGateId);
            requestedAt.Remove(sourceGateId);
        }

        /// <summary>Forgets every grant, denial and pending request: a world unload, or a new session.</summary>
        public static void Clear()
        {
            grants.Clear();
            denials.Clear();
            requestedAt.Clear();
        }

        /// <summary>Server: the grant, carrying the destination itself, since the asking client usually holds neither
        /// destination pillar's ZDO. The wire format lives here beside <see cref="OnGrant"/>, which reads it.</summary>
        internal static void SendGrant(long target, long sourceId, long destId, ZDO destAnchor, ZDO destPartner)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(sourceId);
            pkg.Write(destId);
            pkg.Write(destAnchor.GetPosition());
            pkg.Write(destPartner.GetPosition());
            pkg.Write(SeaGateFields.GetSides(destAnchor));
            ZRoutedRpc.instance.InvokeRoutedRPC(target, GrantRpc, pkg);
        }

        internal static void SendDenial(long target, long sourceId, long destId, string reasonToken)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(target, DenyRpc, sourceId, destId, reasonToken);
        }

        private static void OnGrant(long sender, ZPackage pkg)
        {
            if (!SenderIdentity.IsFromServer(sender))
                return;
            JumpGrant grant = new JumpGrant(pkg.ReadLong(), pkg.ReadLong(), pkg.ReadVector3(), pkg.ReadVector3(),
                pkg.ReadInt(), Time.time);
            grants[grant.SourceId] = grant;
            denials.Remove(grant.SourceId);
        }

        private static void OnDeny(long sender, long sourceId, long destId, string reasonToken)
        {
            if (!SenderIdentity.IsFromServer(sender))
                return;
            denials[sourceId] = new Denial(destId, reasonToken, Time.time);
            grants.Remove(sourceId);
        }

        private readonly struct Denial
        {
            public readonly long DestId;
            public readonly string Token;
            public readonly float At;

            public Denial(long destId, string token, float at)
            {
                DestId = destId;
                Token = token;
                At = at;
            }
        }
    }

    /// <summary>A world unload (disconnect, quit to menu) drops every grant and denial, so nothing from one world is
    /// read in the next. Runs whatever the settings say: clearing is always safe, and a switch turned off mid-session
    /// must not leave old grants behind.</summary>
    [HarmonyPatch(typeof(Game), "OnDestroy")]
    public static class SeaGateGrantTeardownPatch
    {
        [HarmonyPostfix]
        public static void Postfix() => SeaGateGrant.Clear();
    }
}
