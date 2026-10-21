// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Magpie.Config;
using ExtraPotions.Magpie.Items;
using ExtraPotions.Magpie.Merging;
using ExtraPotions.Magpie.Runtime.Loot;
using ExtraPotions.Magpie.Runtime.Pickup;
using ExtraPotions.Magpie.StepAside;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Magpie.Runtime.Merge
{
    /// <summary>
    /// Design B1 to B10 and section 3.3 (G05): scans the loaded items this peer owns once per Merge Scan Interval, plans
    /// merges with the portable <see cref="MergePlanner"/> and runs each plan's ordered steps: source token (C12), phase 1
    /// on the target with the intent record, barrier, phase 2 on the source. Items with an intent record are reconciled
    /// by <see cref="MergeReconcile"/>. Merging runs only where <see cref="MergeBarrier"/> passes: single player and the
    /// server or host. A remote client waits for the V5 probe (no loss window in the meantime). Magpie never claims
    /// ownership to merge (B6).
    /// </summary>
    internal static class MergeEngine
    {
        private static readonly List<ItemDrop> Owned = new List<ItemDrop>();
        private static readonly List<GroundItem> Ground = new List<GroundItem>();
        private static double nextScan;
        private static long nonceCounter;

        public static int Merged { get; private set; }
        public static int RolledBack { get; private set; }

        public static void Tick()
        {
            if (ZNet.instance == null || ZNetScene.instance == null || !MagpieRuntime.ModuleOn(MagpieModule.Merge)) return;
            if (!MergeBarrier.MayMerge(Role())) return;
            var settings = MagpieRuntime.Settings!;
            var now = (double)Time.time;
            if (now < nextScan) return;
            nextScan = now + settings.Get<float>(MagpieSettings.MergeScanInterval);
            var merge = new MergeSettings(settings.Get<float>(MagpieSettings.MergeRadius), settings.Get<int>(MagpieSettings.MergeMinCount),
                settings.Get<int>(MagpieSettings.MaxMergesPerScan), settings.Get<float>(MagpieSettings.MergeSettleTime),
                settings.List(MagpieSettings.MergeIgnoreItems).Names);
            var world = ZNet.instance.GetTimeSeconds();
            Collect();
            Reconcile(world, merge);
            foreach (var plan in MergePlanner.Plan(Ground, merge, world, NewNonce))
                Execute(plan);
        }

        public static void Reset()
        {
            Owned.Clear();
            Ground.Clear();
            nextScan = 0;
        }

        private static MergeRole Role()
        {
            var net = ZNet.instance;
            if (net == null) return MergeRole.None;
            if (!net.IsServer()) return MergeRole.RemoteClient;
            return net.GetPeers().Count == 0 && !net.IsDedicated() ? MergeRole.SinglePlayer : MergeRole.Server;
        }

        private static string NewNonce() =>
            (++nonceCounter).ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 12);

        /// <summary>Loaded items this peer owns, as planner input; the list index is the planner id.</summary>
        private static void Collect()
        {
            Owned.Clear();
            Ground.Clear();
            foreach (var item in ItemDrop.s_instances)
            {
                if (item == null || item.m_itemData == null) continue;
                var view = item.m_nview;
                if (view == null || !view.IsValid() || !view.IsOwner()) continue;
                Ground.Add(ToGround(Owned.Count, item, view.GetZDO()));
                Owned.Add(item);
            }
        }

        private static GroundItem ToGround(int id, ItemDrop item, ZDO zdo)
        {
            var data = item.m_itemData;
            var position = item.transform.position;
            var body = item.m_body;
            var moving = body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > 0.01f;
            var token = zdo.GetString(MagpieCore.InstanceTokenKey, "");
            return new GroundItem(id, Identity(item, data), data.m_stack, data.m_shared.m_maxStackSize,
                new Point3(position.x, position.y, position.z), (long)zdo.m_uid.ID, SpawnTicks(zdo),
                ownedLocally: true, valid: true, moving: moving, ageSeconds: item.GetTimeSinceSpawned(),
                pulled: PickupEngine.IsPulled(item), pendingIntent: zdo.GetString(MagpieCore.MergeInKey, "").Length > 0,
                token: token.Length > 0 ? token : null);
        }

        /// <summary>B5: the full saved identity except count and position, plus Magpie's loot mark.</summary>
        private static ItemIdentity Identity(ItemDrop item, ItemDrop.ItemData data) =>
            new ItemIdentity(Utils.GetPrefabName(item.gameObject), data.m_quality, data.m_variant, data.m_crafterID, data.m_crafterName ?? "",
                data.m_durability, data.m_worldLevel, data.m_customData, CheatedMarks.IsCheated(item, data), LootMarks.IsMarked(item));

        /// <summary>The saved spawn time the despawn rule reads (ZDO <c>s_spawnTime</c>, ticks; verify V3). 0 when the item has none.</summary>
        private static long SpawnTicks(ZDO zdo) => zdo.GetLong(ZDOVars.s_spawnTime, 0L);

        private static void Execute(MergePlan plan)
        {
            var target = Owned[(int)plan.Target.Id];
            var source = Owned[(int)plan.Source.Id];
            if (!StillOwned(target) || !StillOwned(source)) return;
            var targetZdo = target.m_nview.GetZDO();
            var sourceZdo = source.m_nview.GetZDO();

            // Step 2: the source carries a persisted instance token (C12) the reconcile scan can find it by.
            var identity = MeadworksInstanceIdentity.Ensure(sourceZdo, MagpieCore.InstanceTokenKey);
            if (!identity.HasToken) return;
            var intent = plan.Intent(identity.Token!.Value.Value);

            // Step 3, phase 1: the target grows first and records the intent; it keeps the later spawn time (B8).
            target.m_itemData.m_stack = plan.TargetStackAfter;
            targetZdo.Set(MagpieCore.MergeInKey, intent.Format());
            var later = Math.Max(SpawnTicks(targetZdo), SpawnTicks(sourceZdo));
            if (later > 0) targetZdo.Set(ZDOVars.s_spawnTime, later);
            target.Save();

            // Step 4: barrier. Only roles where it passes at once reach this point (MergeBarrier).
            // Step 5, phase 2: the source shrinks or goes, after phase 1 in the same ordered stream.
            if (!StillOwned(source) || !StillOwned(target))
            {
                MagpieRuntime.Log?.Verbose("Merge phase 2 deferred: ownership changed; reconcile decides.");
                return;
            }
            var completed = intent.WithPhase2Saved().Format();
            if (plan.DestroysSource)
            {
                // The removal is on the target before Destroy. A save that keeps the removal cannot roll those items back.
                targetZdo.Set(MagpieCore.MergeInKey, completed);
                target.Save();
                ZNetScene.instance.Destroy(source.gameObject);
            }
            else
            {
                // Flag, reduced stack, and nonce are one step. The flag is not a later write that a save can skip.
                source.m_itemData.m_stack = plan.SourceStackAfter;
                sourceZdo.Set(MagpieCore.MergeOutKey, MergeIntent.AppendNonce(sourceZdo.GetString(MagpieCore.MergeOutKey, ""), plan.Nonce));
                targetZdo.Set(MagpieCore.MergeInKey, completed);
                source.Save();
                target.Save();
            }
            Merged++;
            MagpieRuntime.Log?.Debug($"Merged {plan.Moved} {plan.Target.Identity.Prefab} into one stack ({plan.TargetStackAfter}).");
        }

        /// <summary>Section 3.3 reconcile: intent records older than 10 s are cleared or, when phase 2 never landed, rolled back.</summary>
        private static void Reconcile(double world, MergeSettings settings)
        {
            var searchRadius = MergeReconcile.SearchRadius(settings);
            foreach (var target in Owned)
            {
                if (!StillOwned(target)) continue;
                var zdo = target.m_nview.GetZDO();
                var text = zdo.GetString(MagpieCore.MergeInKey, "");
                if (text.Length == 0) continue;
                var intent = MergeIntent.Parse(text);
                if (intent == null)
                {
                    zdo.Set(MagpieCore.MergeInKey, "");
                    continue;
                }
                var position = target.transform.position;
                var ready = ZNetScene.instance.IsAreaReady(position);
                switch (MergeReconcile.Decide(intent, world, ready, Observe(intent, position, searchRadius)))
                {
                    case ReconcileAction.RollBackTarget:
                        target.m_itemData.m_stack = MergeReconcile.StackAfterRollback(target.m_itemData.m_stack, intent.Moved, intent.PreMergeStack);
                        zdo.Set(MagpieCore.MergeInKey, "");
                        target.Save();
                        RolledBack++;
                        MagpieRuntime.Log?.Verbose($"Merge rolled back: {intent.Moved} removed from the target (nonce {intent.Nonce}).");
                        break;
                    case ReconcileAction.FinishDestroy:
                        var sourceDrop = FindSource(intent, position, searchRadius);
                        if (sourceDrop != null && StillOwned(sourceDrop))
                        {
                            // Leave the flag. The next pass clears it once this source is gone, so the removal is not also a rollback.
                            ZNetScene.instance.Destroy(sourceDrop.gameObject);
                            MagpieRuntime.Log?.Verbose($"Merge removal finished for nonce {intent.Nonce}.");
                        }
                        break;
                    case ReconcileAction.Clear:
                        zdo.Set(MagpieCore.MergeInKey, "");
                        break;
                }
            }
        }

        private static SourceObservation Observe(MergeIntent intent, Vector3 near, float radius)
        {
            var item = FindSource(intent, near, radius);
            if (item == null)
                return SourceObservation.NotFound;
            return MergeIntent.HasNonce(item.m_nview.GetZDO().GetString(MagpieCore.MergeOutKey, ""), intent.Nonce)
                ? SourceObservation.FoundWithNonce
                : SourceObservation.FoundWithoutNonce;
        }

        private static ItemDrop FindSource(MergeIntent intent, Vector3 near, float radius)
        {
            var limit = radius * radius;
            foreach (var item in ItemDrop.s_instances)
            {
                var view = item != null ? item.m_nview : null;
                if (view == null || !view.IsValid() || (item.transform.position - near).sqrMagnitude > limit) continue;
                if (view.GetZDO().GetString(MagpieCore.InstanceTokenKey, "") != intent.SourceToken) continue;
                return item;
            }
            return null;
        }

        private static bool StillOwned(ItemDrop item) =>
            item != null && item.m_nview != null && item.m_nview.IsValid() && item.m_nview.IsOwner();
    }
}
