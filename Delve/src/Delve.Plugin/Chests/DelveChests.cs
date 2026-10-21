// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/Restocker.cs Sweep, Restock, the dry-run stamps; src/LootChests.cs FindPrefabs; src/Notifier.cs the
// ZDO.Deserialize postfix, its open and announced sets; src/History.cs the pending records written once the chest is
// free). The MIT notice is in THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: one Delve engine service; the per-chest decision is Delve.Core's ChestRefill, the opening
// decision ChestObservation; stamps are two-clock stamps in delve.chest.* keys; refills count against the engine's
// action budget and the rest wait for the next check; chests inside a dungeon set to Full are left to the full reset;
// messages are Delve catalog texts in the server language through Crucible's server notices; the authority's own
// player's openings are seen through Container.SetInUse and Container.Save postfixes (user decision R-Q4).
// The item blob is written only when no live container remains, including a host standing at the chest.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Chests;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Localization;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Delve.Runtime
{
    public sealed class DelveChests : IDelveService
    {
        private static readonly int sinceDay = DelveKeys.ChestSinceDay.GetStableHashCode();
        private static readonly int sinceUtc = DelveKeys.ChestSinceUtc.GetStableHashCode();
        private static readonly int firstBy = DelveKeys.ChestFirstBy.GetStableHashCode();
        private static readonly int firstDay = DelveKeys.ChestFirstDay.GetStableHashCode();
        private static readonly int lastBy = DelveKeys.ChestLastBy.GetStableHashCode();
        private static readonly int lastDay = DelveKeys.ChestLastDay.GetStableHashCode();

        private static DelveChests? current;
        private DelveEngine? engine;

        /// <summary>World-lifetime loot prefab index; the current exclusion view is applied at use.</summary>
        private readonly Dictionary<int, Container> prefabs = new Dictionary<int, Container>();
        private readonly ChestExclusions exclusions = new ChestExclusions();

        /// <summary>Dry run writes nothing: its stamps live here (lost on restart).</summary>
        private readonly Dictionary<ZDOID, Stamp?> dryStamps = new Dictionary<ZDOID, Stamp?>();

        /// <summary>When a player was seen opening or emptying a chest before the sweep stamped it.</summary>
        private readonly Dictionary<ZDOID, Stamp> seen = new Dictionary<ZDOID, Stamp>();
        private readonly HashSet<ZDOID> open = new HashSet<ZDOID>();
        private readonly HashSet<ZDOID> announced = new HashSet<ZDOID>();

        /// <summary>History records waiting until no player holds the chest.</summary>
        private readonly Dictionary<ZDOID, ChestHistory.Record> pending = new Dictionary<ZDOID, ChestHistory.Record>();

        public IReadOnlyList<DelveKind> Kinds { get; } = new[] { DelveKind.Chests };

        public void Start(DelveEngine e)
        {
            engine = e;
            current = this;
            prefabs.Clear();
            exclusions.Update(e.Settings.Get(DelveSettings.Chests.Exclude));
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null) continue;
                Container container = prefab.GetComponent<Container>();
                if (container == null || container.m_defaultItems == null || container.m_defaultItems.m_drops.Count == 0) continue;
                prefabs[prefab.name.GetStableHashCode()] = container;
                if (exclusions.Allows(prefab.name) && container.m_autoDestroyEmpty) e.Log.Info("Delve chests: " + prefab.name + " deletes itself when emptied and cannot be refilled.");
            }
            e.Log.Verbose("Delve chests: " + prefabs.Count + " indexed loot chest prefabs, " + exclusions.Count + " excluded names.");
        }

        public void Stop(DelveEngine e)
        {
            Flush();
            if (current == this) current = null;
            engine = null;
            prefabs.Clear();
            exclusions.Clear();
            dryStamps.Clear();
            seen.Clear();
            open.Clear();
            announced.Clear();
            pending.Clear();
        }

        private ChestOptions Options(DelveEngine e)
        {
            var (clock, delay) = e.ClockOf(DelveKind.Chests);
            return new ChestOptions(clock, delay, e.Settings.Get(DelveSettings.Chests.ResetOpened), e.Settings.Get(DelveSettings.Chests.RestockLoaded));
        }

        private Stamp? GetStamp(ZDO zdo, bool dryRun) =>
            dryRun && dryStamps.TryGetValue(zdo.m_uid, out var s) ? s : DelveKeys.Decode(zdo.GetFloat(sinceDay), zdo.GetLong(sinceUtc));

        private void SetStamp(ZDO zdo, Stamp? stamp, bool dryRun)
        {
            if (dryRun)
            {
                dryStamps[zdo.m_uid] = stamp;
                return;
            }
            // A plain Set bumps the data revision, so a client holding an older copy cannot hand a stale stamp back.
            zdo.Set(sinceDay, DelveKeys.EncodeDay(stamp));
            zdo.Set(sinceUtc, DelveKeys.EncodeUtc(stamp));
        }

        public void Check(DelveEngine e, ISet<DelveKind> sweeping)
        {
            Flush();
            exclusions.Update(e.Settings.Get(DelveSettings.Chests.Exclude));
            if (!sweeping.Contains(DelveKind.Chests)) return;
            bool dryRun = e.DryRun(DelveKind.Chests);
            var options = Options(e);
            int tracked = 0, waiting = 0, deferred = 0, acted = 0, loaded = 0, inFull = 0;
            foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values.ToList())
            {
                if (!prefabs.TryGetValue(zdo.GetPrefab(), out Container prefab) || !exclusions.Allows(prefab.name)) continue;
                tracked++;
                if (!DelveDungeonIndex.Allows(zdo.GetPosition(), DelveKind.Chests))
                {
                    inFull++;
                    continue;
                }
                var facts = Facts(zdo, dryRun);
                var decision = ChestRefill.Decide(facts, e.Now, options, e.RegenerateNow(DelveKind.Chests));
                switch (decision.Action)
                {
                    case ChestAction.SkipLoaded:
                        loaded++;
                        break;
                    case ChestAction.ClearStamp:
                        if (!decision.KeepOpening) seen.Remove(zdo.m_uid);
                        SetStamp(zdo, null, dryRun);
                        break;
                    case ChestAction.Stamp:
                        seen.Remove(zdo.m_uid);
                        SetStamp(zdo, decision.NewStamp, dryRun);
                        break;
                    case ChestAction.Waiting:
                        waiting++;
                        break;
                    case ChestAction.Restock:
                        seen.Remove(zdo.m_uid);
                        if (e.Budget <= 0)
                        {
                            deferred++;
                            if (decision.NewStamp.HasValue) SetStamp(zdo, decision.NewStamp, dryRun);
                            break;
                        }
                        e.Budget--;
                        if (dryRun)
                        {
                            SetStamp(zdo, null, true);
                            e.Record(DelveKind.Chests, "refilled " + prefab.name + " at " + Where(zdo) + ", replacing " + facts.ItemCount + " stacks", true);
                            acted++;
                        }
                        else if (Restock(zdo, prefab, out var rolled))
                        {
                            SetStamp(zdo, null, false);
                            e.Record(DelveKind.Chests, "refilled " + prefab.name + " at " + Where(zdo) + ": " + rolled, false);
                            acted++;
                        }
                        else if (decision.NewStamp.HasValue) SetStamp(zdo, decision.NewStamp, false); // rolled nothing: try again next sweep
                        break;
                }
            }
            e.Swept(DelveKind.Chests, deferred);
            e.Report(DelveKind.Chests, new KindReport(tracked, waiting, deferred, acted,
                new Dictionary<GuardResult, int> { [GuardResult.Held] = loaded }, true, dryRun, inFull > 0 ? inFull + " in Full dungeons" : null));
        }

        private ChestFacts Facts(ZDO zdo, bool dryRun)
        {
            Stamp? s = seen.TryGetValue(zdo.m_uid, out var at) ? at : (Stamp?)null;
            return new ChestFacts(ChestInventory.ItemCount(zdo.GetByteArray(ZDOVars.s_items)), GetStamp(zdo, dryRun), s,
                zdo.GetLong(ZDOVars.s_creator) != 0L, DelveWorld.HeldByPlayer(zdo), zdo.GetInt(ZDOVars.s_inUse) == 1, LiveInventory(zdo));
        }

        /// <summary>
        /// True when a container can still be showing the items it loaded: this machine has the view, or any current player is in its active area.
        /// </summary>
        private bool LiveInventory(ZDO zdo)
        {
            if (zdo == null) return false;
            if (DelveWorld.HeldByPlayer(zdo)) return true;
            var scene = ZNetScene.instance;
            if (scene == null) return false;
            var view = scene.FindInstance(zdo);
            if (view != null && view.GetComponent<Container>() != null) return true;
            Vector3 pos = zdo.GetPosition();
            IReadOnlyList<DelvePlayer> players = engine != null ? engine.Players : DelveWorld.Players();
            foreach (var player in players)
                if (ZNetScene.InActiveArea(pos, player.Position)) return true;
            return false;
        }

        /// <summary>
        /// Rolls the prefab's loot table into a fresh inventory of the prefab's size and writes it to the chest, as
        /// Container.AddDefaultItems and Container.Save do on a client. Whatever was inside is replaced. A table can roll
        /// nothing (drop chance below 1): then nothing is written and the chest tries again at the next sweep.
        /// A live container is left unchanged: its next save would put the old items back, or a second player could take the new roll.
        /// </summary>
        private bool Restock(ZDO zdo, Container prefab, out string rolled)
        {
            rolled = "";
            if (!ChestRefill.MayReplaceItems(DelveWorld.HeldByPlayer(zdo), LiveInventory(zdo))) return false;
            var inventory = new Inventory(prefab.m_name, null, prefab.m_width, prefab.m_height);
            var names = new List<string>();
            foreach (ItemDrop.ItemData item in prefab.m_defaultItems.GetDropListItems())
            {
                names.Add((item.m_dropPrefab != null ? item.m_dropPrefab.name : "?") + "x" + item.m_stack);
                inventory.AddItem(item);
            }
            if (inventory.NrOfItems() == 0) return false;
            if (!ChestRefill.MayReplaceItems(DelveWorld.HeldByPlayer(zdo), LiveInventory(zdo))) return false;
            var pkg = new ZPackage();
            inventory.Save(pkg);
            zdo.Set(ZDOVars.s_items, pkg.GetArray());
            rolled = string.Join(" ", names);
            return true;
        }

        private static string Where(ZDO zdo)
        {
            Vector3 p = zdo.GetPosition();
            return "(" + p.x.ToString("0") + ", " + p.z.ToString("0") + ")";
        }

        private void Flush()
        {
            foreach (var id in pending.Keys.ToList())
            {
                ZDO zdo = ZDOMan.instance?.GetZDO(id);
                if (zdo == null)
                {
                    pending.Remove(id);
                    continue;
                }
                if (DelveWorld.HeldByPlayer(zdo)) continue; // the owner's next change would be dropped
                var r = pending[id];
                zdo.Set(firstBy, r.FirstBy);
                zdo.Set(firstDay, ChestHistory.Encode(r.FirstDay));
                zdo.Set(lastBy, r.LastBy);
                zdo.Set(lastDay, ChestHistory.Encode(r.LastDay));
                pending.Remove(id);
            }
        }

        private ChestHistory.Record Read(ZDO zdo) =>
            pending.TryGetValue(zdo.m_uid, out var p) ? p
                : new ChestHistory.Record(zdo.GetString(firstBy), ChestHistory.Decode(zdo.GetInt(firstDay)), zdo.GetString(lastBy), ChestHistory.Decode(zdo.GetInt(lastDay)));

        private static HistoryWords Words() => new HistoryWords(DelveText.ServerText(DelveTextKeys.HistoryMany), DelveText.ServerText(DelveTextKeys.HistoryOnce),
            DelveText.ServerText(DelveTextKeys.HistoryYou), DelveText.ServerText(DelveTextKeys.HistoryToday), DelveText.ServerText(DelveTextKeys.HistoryYesterday),
            DelveText.ServerText(DelveTextKeys.HistoryDaysAgo));

        /// <summary>A chest update from the player who has it open (ZDOMan.RPC_ZDOData then ZDO.Deserialize on the authority).</summary>
        private void Observe(ZDO zdo, bool localHook = false)
        {
            var e = engine;
            if (e == null || !e.Running || !e.Active(DelveKind.Chests)) return;
            exclusions.Update(e.Settings.Get(DelveSettings.Chests.Exclude));
            if (!prefabs.TryGetValue(zdo.GetPrefab(), out Container prefab) || !exclusions.Allows(prefab.name)) return;
            if (zdo.GetLong(ZDOVars.s_creator) != 0L) return;
            ZDOID id = zdo.m_uid;
            bool dryRun = e.DryRun(DelveKind.Chests);
            int? count = ChestInventory.ItemCount(zdo.GetByteArray(ZDOVars.s_items));
            bool isOpen = zdo.GetInt(ZDOVars.s_inUse) == 1;
            if (!isOpen)
            {
                open.Remove(id);
                announced.Remove(id);
            }
            long owner = zdo.GetOwner();
            ZNetPeer? remote = owner != 0L && owner != ZDOMan.GetSessionID() ? ZNet.instance.GetPeer(owner) : null;
            bool localPlayer = !ZNet.instance.IsDedicated() && Player.m_localPlayer != null;
            var opener = ChestObservation.OpenerOf(isOpen, owner, ZDOMan.GetSessionID(), remote != null, localHook, localPlayer);
            long? tellUid = opener == ChestOpener.Remote ? remote!.m_uid : opener == ChestOpener.Local ? ZNet.GetUID() : (long?)null;
            string tellName = opener == ChestOpener.Remote ? remote!.m_playerName : opener == ChestOpener.Local ? Player.m_localPlayer.GetPlayerName() : "";
            bool newOpening = tellUid.HasValue && open.Add(id);
            var now = WorldClock.Now(ZNet.instance.GetTimeSeconds(), Math.Max(1, EnvMan.instance.m_dayLengthSec), DateTime.UtcNow);
            var o = ChestObservation.Observe(count, GetStamp(zdo, dryRun).HasValue, seen.ContainsKey(id), tellUid.HasValue, newOpening, e.Settings.Get(DelveSettings.Chests.ResetOpened));
            if (o.Seen == SeenChange.Start) seen[id] = now;
            else if (o.Seen == SeenChange.Clear)
            {
                seen.Remove(id);
                announced.Remove(id);
            }
            if (!tellUid.HasValue) return;
            int today = WorldClock.DayIndex(ZNet.instance.GetTimeSeconds(), Math.Max(1, EnvMan.instance.m_dayLengthSec));
            if (o.TellHistory && e.Settings.Get(DelveSettings.Chests.History))
            {
                var before = Read(zdo);
                var line = ChestHistory.Notice(before, tellName, today, Words(), DelveNotices.On);
                if (line != null) DelveNotices.Tell(tellUid.Value, line, ServerNoticePlacement.TopLeft);
                pending[id] = ChestHistory.Opened(before, tellName, today);
            }
            if (o.NoticeKey != null && DelveNotices.On && announced.Add(id))
            {
                var options = Options(e);
                int units = ChestRefill.UnitsLeft(Facts(zdo, dryRun), now, options);
                var key = ChestObservation.Key(count == 0, units);
                var text = units > 0 ? DelveText.ServerFormat(key, DelveNotices.Duration(options.Clock, units)) : DelveText.ServerText(key);
                DelveNotices.Tell(tellUid.Value, text, ServerNoticePlacement.Center);
            }
        }

        /// <summary>The authority's own player changes a chest locally (no ZDO.Deserialize): seen when it opens, closes and saves its items.</summary>
        private static void ObserveLocal(Container container)
        {
            var service = current;
            if (service == null || container == null || ZNet.instance == null || !ZNet.instance.IsServer() || ZNet.instance.IsDedicated()) return;
            var view = container.m_nview;
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            try { service.Observe(view.GetZDO(), localHook: true); }
            catch (Exception error) { Plugin.Log?.Warning("Delve local chest update: " + error.GetType().Name + ": " + error.Message); }
        }

        [HarmonyPatch(typeof(Container), nameof(Container.SetInUse))]
        private static class SeeLocalUse
        {
            private static bool Prepare() => !DelveModules.StepAside.SteppedAside(Integration.DelveModule.Chests);

            private static void Postfix(Container __instance) => ObserveLocal(__instance);
        }

        [HarmonyPatch(typeof(Container), "Save")]
        private static class SeeLocalSave
        {
            private static bool Prepare() => !DelveModules.StepAside.SteppedAside(Integration.DelveModule.Chests);

            private static void Postfix(Container __instance) => ObserveLocal(__instance);
        }

        [HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
        private static class SeeOpenings
        {
            /// <summary>A stepped-aside part is never patched (design 9).</summary>
            private static bool Prepare() => !DelveModules.StepAside.SteppedAside(Integration.DelveModule.Chests);

            private static void Postfix(ZDO __instance)
            {
                var service = current;
                if (service == null || ZNet.instance == null || !ZNet.instance.IsServer()) return;
                try { service.Observe(__instance); }
                catch (Exception error) { Plugin.Log?.Warning("Delve chest update: " + error.GetType().Name + ": " + error.Message); }
            }
        }
    }
}
