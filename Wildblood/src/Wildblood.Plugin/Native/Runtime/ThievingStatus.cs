// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (B06d/B06e): explicit theft status and outcome acknowledgement. Both are routed to the exact current
// owner of the thief's ZDO, which answers from its retained outcome archive or an exact root-bound pouch entry, and fences
// a root it never banked as Denied. Nothing here banks, drops, refunds or resends an item.
using System;
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Transfers;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    internal static class ThievingStatus
    {
        public const string Status = "wb_steal_status";
        public const string Ack = "wb_steal_ack";
        private static ZRoutedRpc? _registeredOn;

        internal static bool TryEnsureRegistered()
        {
            ZRoutedRpc bus = ZRoutedRpc.instance;
            if (bus == null || !ThievingRpc.Active) return false;
            if (!NativeRpcOwnership.Routed(bus, NativeRpcOwner.TheftStatus, () =>
            {
                bus.Register<ZPackage>(Status, (sender, pkg) => Guard.Run("ThievingStatus.Status", () => Receive(bus, sender, pkg, false)));
                bus.Register<ZPackage>(Ack, (sender, pkg) => Guard.Run("ThievingStatus.Ack", () => Receive(bus, sender, pkg, true)));
            }, () => { if (ReferenceEquals(_registeredOn, bus)) _registeredOn = null; }, Status, Ack)) return false;
            _registeredOn = bus;
            TheftTransfers.Settled = Acknowledge;
            return true;
        }

        /// <summary>Requester side: asks the thief's current owner about one retained intent; returns the peer asked, or 0
        /// when the thief's ZDO is not known here or has no owner (the intent stays retained).</summary>
        internal static long Ask(TheftRequest request)
        {
            if (!TryEnsureRegistered() || !StealEscrow.EnsureRegistered()) return 0;
            return Send(Status, request);
        }

        /// <summary>Requester side, after a saved settlement: releases the owner's retained outcome and the world row.</summary>
        internal static void Acknowledge(TheftRequest request)
        {
            ThievingArchive.Release(request);
            if (TryEnsureRegistered()) Send(Ack, request);
        }

        private static long Send(string method, TheftRequest request)
        {
            ZDO? zdo = ZDOMan.instance?.GetZDO(new ZDOID(request.SourceUser, request.SourceId));
            long owner = zdo?.GetOwner() ?? 0L;
            ZRoutedRpc bus = ZRoutedRpc.instance;
            if (owner == 0 || bus == null) return 0;
            ZPackage pkg = new ZPackage();
            pkg.Write(request.SourceUser);
            pkg.Write(request.SourceId);
            pkg.Write(TheftWire.EncodeStatus(request.Root, request.Fingerprint));
            bus.InvokeRoutedRPC(owner, method, pkg);
            return owner;
        }

        // Owner side. A machine that does not own the ZDO is not authoritative and stays silent.
        private static void Receive(ZRoutedRpc bus, long sender, ZPackage pkg, bool ack)
        {
            if (!ReferenceEquals(bus, _registeredOn) || !ReferenceEquals(bus, ZRoutedRpc.instance) || !ThievingRpc.Active
                || !NativeRpcOwnership.OwnsRouted(bus, NativeRpcOwner.TheftStatus) || sender == 0 || pkg == null || pkg.Size() > 512) return;
            ZDOID source = new ZDOID(pkg.ReadLong(), pkg.ReadUInt());
            TheftStatusQuery query = TheftWire.DecodeStatus(pkg.ReadByteArray());
            if (pkg.GetPos() != pkg.Size()) return;
            ZDO? zdo = ZDOMan.instance?.GetZDO(source);
            if (zdo == null || !zdo.IsOwner()) return;
            TheftOutcomeArchive archive;
            try { archive = TheftOutcomeArchive.Decode(zdo.GetByteArray(TraitKeys.TheftOutcomes.GetStableHashCode())); }
            catch (System.IO.InvalidDataException) { return; } // an unreadable archive proves nothing either way
            if (ack)
            {
                if (archive.Acknowledge(query.Root, query.Fingerprint)) zdo.Set(TraitKeys.TheftOutcomes, archive.Encode());
                return;
            }
            var pouch = PouchStore.Load(zdo);
            // A root this thief never banked is fenced as Denied in the archive before answering, so a late ask replays it.
            var answer = TheftOwnerStatus.Decide(archive, (r, f) => PouchStore.HoldsExact(pouch, r, f), query.Root, query.Fingerprint, out bool recorded);
            if (recorded) zdo.Set(TraitKeys.TheftOutcomes, archive.Encode());
            StealEscrow.AnswerV2(sender, query.Root, query.Fingerprint, answer);
        }
    }
}
