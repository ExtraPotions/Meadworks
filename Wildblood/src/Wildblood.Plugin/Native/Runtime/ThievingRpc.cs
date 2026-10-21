// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration; v2 root-bound ask with retained owner outcomes.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Transfers;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    /// <summary>The existing creature-view request from the robbed player's authoritative client to the captured creature owner.
    /// The sender saves a durable native debit receipt before dispatch. Unknown replies retain that debit for recovery.
    /// <para>v2 (product 0.3): the ask carries the requester's stable root and exact item fingerprint. The owner writes the
    /// root-bound pouch entry and its outcome into the creature's ZDO in one owner update, replays an archived outcome
    /// for a repeated ask instead of banking twice, and answers through <see cref="StealEscrow.AnswerV2"/>.</para></summary>
    public static class ThievingRpc
    {
        public const string Steal = "ecr_steal_ask";
        public const string StealV2 = "wb_steal_ask2";

        internal static bool Active => Plugin.NativeRulesReady && WildbloodModules.Active(WildbloodModule.Traits)
            && CruciblePlugin.Session.AuthorityReady && CruciblePlugin.Session.WorldId != 0
            && ZNet.instance != null && ZNet.m_world != null && ZNet.instance.GetWorldUID() == CruciblePlugin.Session.WorldId;

        public static bool Register(ZNetView nview, EliteController controller)
        {
            if (!Active) return false;
            ZRoutedRpc bus = ZRoutedRpc.instance;
            ZNet network = ZNet.instance;
            long generation = CruciblePlugin.Session.Generation, world = CruciblePlugin.Session.WorldId;
            string session = CruciblePlugin.Session.SessionId;
            ThievingStatus.TryEnsureRegistered(); // routed owner status/ack handlers, once per bus
            bool Current() => ReferenceEquals(bus, ZRoutedRpc.instance) && ReferenceEquals(network, ZNet.instance)
                && generation == CruciblePlugin.Session.Generation && world == CruciblePlugin.Session.WorldId
                && session == CruciblePlugin.Session.SessionId;
            return bus != null && NativeRpcOwnership.View(nview, NativeRpcOwner.CreatureThieving, () =>
            {
                nview.Register<int, ZPackage>(Steal, (sender, id, pkg) => { if (Current()) Bank(nview, controller, sender, id, pkg); });
                nview.Register<ZPackage>(StealV2, (sender, pkg) => { if (Current()) BankV2(nview, controller, sender, pkg); });
            }, Steal, StealV2);
        }

        public static bool CanSend(ZNetView view) => NativeRpcOwnership.OwnsView(view, NativeRpcOwner.CreatureThieving);

        /// <summary>Player-client side, once <see cref="StealEscrow.Hold"/> holds the item and its durable debit is saved:
        /// sends the v2 ask prepared with that intent (root, counter, fingerprint, exact item bytes).</summary>
        public static void Send(ZNetView creatureView, long owner, int id, ItemDrop.ItemData item)
        {
            if (!Active || !CanSend(creatureView) || owner == 0 || id <= 0) return;
            byte[]? ask = TheftTransfers.AskBytes(id);
            if (ask == null) return;
            creatureView.InvokeRPC(owner, StealV2, new ZPackage(ask));
        }

        // Existing v1 bool replies are retained for a v1 ask; they do not prove durable owner banking.
        private static void Bank(ZNetView nview, EliteController controller, long sender, int id, ZPackage pkg)
        {
            try
            {
                bool banked = Admissible(nview, controller, sender, id) && BankItem(nview, controller, pkg, out var item)
                    && PouchStore.TryAdd(nview.GetZDO(), new PouchStore.Entry { Item = item!, StealerId = sender, StealCounter = id },
                        PouchStore.ResolvedMaxItems(controller.Rules, controller.Traits));
                StealEscrow.Answer(sender, id, banked);
            }
            catch (System.Exception)
            {
                // A write or reply may already have happened. Absence is UNKNOWN, never a manufactured refusal.
                Log.Warn("Thieving owner outcome RecoveryRequired; no speculative reply or retry.");
            }
        }

        // v2: an archived outcome is replayed exactly; otherwise the bank decision and its outcome are written together.
        private static void BankV2(ZNetView nview, EliteController controller, long sender, ZPackage pkg)
        {
            try
            {
                if (pkg == null || pkg.Size() <= 0 || pkg.Size() > TheftWire.MaxItem + 256) return;
                TheftAsk ask = TheftWire.DecodeAsk(pkg.GetArray());
                if (!Admissible(nview, controller, sender, ask.Counter))
                {
                    // Not bankable here. The current owner archives the refusal (or replays an earlier outcome) so the
                    // requester can settle from replicated proof; a non-owner's Denied is only a hint and writes nothing.
                    TheftStatusAnswer refusal = TheftStatusAnswer.Denied;
                    if (nview.IsValid() && nview.IsOwner())
                    {
                        ZDO owned = nview.GetZDO();
                        TheftOutcomeArchive held = TheftOutcomeArchive.Decode(owned.GetByteArray(TraitKeys.TheftOutcomes.GetStableHashCode()));
                        List<PouchStore.Entry> current = PouchStore.Load(owned);
                        refusal = TheftOwnerStatus.Decide(held, (r, f) => PouchStore.HoldsExact(current, r, f), ask.Root, ask.Fingerprint, out bool fenced);
                        if (fenced) owned.Set(TraitKeys.TheftOutcomes, held.Encode());
                        if (refusal == TheftStatusAnswer.Unknown) return; // full archive or conflict: no answer, debit retained
                    }
                    StealEscrow.AnswerV2(sender, ask.Root, ask.Fingerprint, refusal);
                    return;
                }
                ZDO zdo = nview.GetZDO();
                TheftOutcomeArchive archive = TheftOutcomeArchive.Decode(zdo.GetByteArray(TraitKeys.TheftOutcomes.GetStableHashCode()));
                List<PouchStore.Entry> pouch = PouchStore.Load(zdo);
                TheftStatusAnswer known = TheftOwnerStatus.Answer(archive, (r, f) => PouchStore.HoldsExact(pouch, r, f), ask.Root, ask.Fingerprint);
                if (known != TheftStatusAnswer.Unknown || archive.Contains(ask.Root))
                {
                    if (known != TheftStatusAnswer.Unknown) StealEscrow.AnswerV2(sender, ask.Root, ask.Fingerprint, known);
                    return; // a repeated ask replays the retained answer; a conflicting one gets none
                }
                if (!archive.CanRecord)
                {
                    StealEscrow.AnswerV2(sender, ask.Root, ask.Fingerprint, TheftStatusAnswer.Denied); // full: refused, not banked
                    return;
                }
                ItemDrop.ItemData? item = null;
                // A late ask (past the window the requester waits before asking for status) is archived Denied, never banked:
                // the requester may already have settled from another owner's fence.
                bool fresh = ZNet.instance != null && TheftWire.Fresh(ask.SentAt, ZNet.instance.GetTimeSeconds());
                bool banked = fresh && TheftWire.ItemMatches(ask.Fingerprint, ask.Item) && BankItem(nview, controller, new ZPackage(ask.Item), out item)
                    && pouch.Count < PouchStore.ResolvedMaxItems(controller.Rules, controller.Traits);
                if (banked)
                {
                    pouch.Add(new PouchStore.Entry { Item = item!, StealerId = sender, StealCounter = ask.Counter, Root = ask.Root, Fingerprint = ask.Fingerprint });
                }
                if (!archive.Record(ask.Root, ask.Fingerprint, banked)) return;
                // Content and outcome in one owner update: both keys are set before this frame's ZDO is sent.
                if (banked) PouchStore.Save(zdo, pouch);
                zdo.Set(TraitKeys.TheftOutcomes, archive.Encode());
                StealEscrow.AnswerV2(sender, ask.Root, ask.Fingerprint, banked ? TheftStatusAnswer.Approved : TheftStatusAnswer.Denied);
            }
            catch (System.Exception)
            {
                Log.Warn("Thieving v2 owner outcome RecoveryRequired; no speculative reply or retry.");
            }
        }

        // This proves a current native participant, not the historical landed hit absent from the source payload.
        private static bool SenderPlayer(long sender)
        {
            if (sender == 0) return false;
            foreach (Character character in Character.GetAllCharacters())
                if (character is Player player && player.m_nview != null && player.m_nview.IsValid()
                    && player.m_nview.GetZDO().GetOwner() == sender) return true;
            return false;
        }

        // Only the owner writes the pouch, and it re-checks what the player saw: a live Thieving creature.
        private static bool Admissible(ZNetView nview, EliteController controller, long sender, int id) =>
            Active && CanSend(nview) && nview.IsOwner() && controller.Ready && controller.Creature != null
            && ReferenceEquals(controller.View, nview) && !controller.Creature.IsDead()
            && !controller.Creature.IsTamed() && controller.Traits.Has(Mutation.Thieving) && id > 0 && SenderPlayer(sender);

        private static bool BankItem(ZNetView nview, EliteController controller, ZPackage pkg, out ItemDrop.ItemData? loaded)
        {
            loaded = null;
            if (pkg == null || pkg.Size() <= 0 || pkg.Size() > 1024 * 1024) return false;
            var (prefabHash, item) = ItemDrop.ItemData.Load(pkg, Version.Item.ChunksNCheats);
            if (pkg.GetPos() != pkg.Size() || prefabHash == 0 || item == null || !PouchStore.TryResolvePrefab(prefabHash, item)
                || item.m_stack <= 0 || item.m_stack > item.m_shared.m_maxStackSize || item.m_quality <= 0
                || (item.m_shared.m_maxQuality > 0 && item.m_quality > item.m_shared.m_maxQuality)
                || float.IsNaN(item.m_durability) || float.IsInfinity(item.m_durability))
            {
                if (Log.Diagnostics)
                {
                    Log.Diag($"{nview.name}: steal request named an unresolvable prefab ({prefabHash}), refused");
                }
                return false;
            }
            loaded = item;
            return true;
        }
    }
}
