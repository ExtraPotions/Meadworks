// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: existing reply transport retained; durable local phases replace timeout refund/late retake;
// v2 root-bound answers for live replies and explicit status recovery.
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Transfers;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    /// <summary>The existing routed owner reply, backed by the player's durable local debit and settlement receipts.</summary>
    public static class StealEscrow
    {
        public const string Reply = "ecr_steal_reply";
        public const string ReplyV2 = "wb_steal_answer";
        private static ZRoutedRpc? _registeredOn;

        public static int Hold(Inventory inventory, ItemDrop.ItemData item, ZNetView source, long owner)
        {
            if (!ThievingRpc.Active || item == null || !EnsureRegistered()) return 0;
            return TheftTransfers.Hold(inventory, item, source, owner);
        }

        public static void Answer(long asker, int id, bool banked)
        {
            if (ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(asker, Reply, id, banked);
        }

        /// <summary>Owner side: one v2 answer (Approved, Denied or Unknown) to the exact asker.</summary>
        public static void AnswerV2(long asker, string root, string fingerprint, TheftStatusAnswer answer)
        {
            if (ZRoutedRpc.instance != null && asker != 0)
                ZRoutedRpc.instance.InvokeRoutedRPC(asker, ReplyV2, new ZPackage(TheftWire.EncodeAnswer(root, fingerprint, answer)));
        }

        /// <summary>Requester side: the answer handlers, needed before a hold sends and before an explicit recovery asks.</summary>
        internal static bool EnsureRegistered()
        {
            ZRoutedRpc bus = ZRoutedRpc.instance;
            if (bus == null) return false;
            if (!NativeRpcOwnership.Routed(bus, NativeRpcOwner.StealReply, () =>
            {
                bus.Register<int, bool>(Reply, (sender, id, banked) =>
                    Guard.Run("StealEscrow.Reply", answer => TheftTransfers.Reply(bus, answer.sender, answer.id, answer.banked), (sender, id, banked)));
                bus.Register<ZPackage>(ReplyV2, (sender, pkg) =>
                    Guard.Run("StealEscrow.ReplyV2", answer => {
                        if (answer.pkg != null && answer.pkg.Size() > 0 && answer.pkg.Size() <= 512)
                            TheftTransfers.Answer(bus, answer.sender, answer.pkg.GetArray());
                    }, (sender, pkg)));
            }, () => { if (ReferenceEquals(_registeredOn, bus)) _registeredOn = null; }, Reply, ReplyV2)) return false;
            _registeredOn = bus;
            return true;
        }
    }
}
