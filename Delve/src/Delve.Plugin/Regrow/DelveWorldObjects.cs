// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/Regrow.cs: Init, SpotKeyAt, IsWhole, Remember, Tick (zone visits and biome entry notices), Sweep and the
// ZDO.Deserialize postfix). The MIT notice is in THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: one Delve engine service for ancient armor, obsidian, flametal and ice; spots and zone visits
// live in Delve's JSON sidecar (RestockRegen's record file is carried over once, copy only when absent); visits are
// two-clock stamps, so a kind can wait in in-game days or real hours; the per-spot decision is Delve.Core's
// RegrowRules.DecideAt with the shared guards plus the player base guard; restorations count against the engine's action
// budget; biome notices use Delve's cooldown and catalog texts in the server language.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Localization;
using ExtraPotions.Delve.Regrow;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Delve.Runtime
{
    public sealed class DelveWorldObjects : IDelveService
    {
        private static DelveWorldObjects? current;
        private static readonly int health = "health".GetStableHashCode();
        // MineRock keeps one "Health<n>" float per chunk, written only once the chunk has been hit.
        private static readonly int[] chunkHealth = Enumerable.Range(0, 64).Select(i => ("Health" + i).GetStableHashCode()).ToArray();

        private readonly Dictionary<int, int> intactOf = new Dictionary<int, int>();
        private readonly Dictionary<int, KindInfo> kindOf = new Dictionary<int, KindInfo>();
        private readonly Dictionary<(int, int, int), RecordSpot> spots = new Dictionary<(int, int, int), RecordSpot>();
        private readonly NoticeCooldown cooldown = new NoticeCooldown();
        private readonly HashSet<long> told = new HashSet<long>();
        private DelveEngine? engine;
        private bool ready;

        public IReadOnlyList<DelveKind> Kinds { get; } = new[] { DelveKind.AncientArmor, DelveKind.Obsidian, DelveKind.Flametal, DelveKind.Ice };

        private DelveEngine E => engine ?? throw new InvalidOperationException("Delve world objects are not started.");

        private static Vec3 V(Vector3 v) => DelveWorld.ToVec(v);

        private static (int, int) Zone(Vector3 p) => RegrowRules.Zone(V(p));

        internal IEnumerable<(string Id, DelveKind Kind, string Prefab, Vec3 Position,
            (ClockKind Clock, double? Remaining, bool Due, bool DryRun, bool Requested) Timing)> ReadSites(DelveEngine e, Stamp now)
        {
            if (!ready || !ReferenceEquals(engine, e)) yield break;
            foreach (var item in spots)
            {
                var spot = item.Value;
                if (!kindOf.TryGetValue(spot.Prefab, out var info) || !e.Active(info.Kind)) continue;
                if (!Finite(spot.Position.X) || !Finite(spot.Position.Y) || !Finite(spot.Position.Z)) continue;
                var (clock, delay) = e.ClockOf(info.Kind);
                bool requested = e.RegenerateNow(info.Kind);
                Stamp? visit = e.Sidecar.Visits.TryGetValue(RegrowRules.Zone(spot.Position), out var since) ? since : (Stamp?)null;
                var timing = ClockRules.Inspect(clock, delay, visit, now, requested);
                var id = info.Kind + ":" + item.Key.Item1.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_"
                    + item.Key.Item2.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" + item.Key.Item3.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var prefab = ZNetScene.instance.GetPrefab(spot.Prefab)?.name ?? spot.Prefab.ToString(System.Globalization.CultureInfo.InvariantCulture);
                yield return (id, info.Kind, prefab, spot.Position, (clock, timing.Remaining, timing.Due, e.DryRun(info.Kind), requested));
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void Start(DelveEngine e)
        {
            engine = e;
            current = this;
            intactOf.Clear();
            kindOf.Clear();
            spots.Clear();
            foreach (var kind in Kinds)
            {
                var info = KindCatalog.Of(kind);
                foreach (string name in info.Whole)
                {
                    if (ZNetScene.instance.GetPrefab(name) == null)
                    {
                        e.Log.Warning("Delve " + kind + ": prefab " + name + " not found, skipped.");
                        continue;
                    }
                    int hash = name.GetStableHashCode();
                    intactOf[hash] = hash;
                    kindOf[hash] = info;
                    foreach (string suffix in info.DamagedSuffixes)
                        if (ZNetScene.instance.GetPrefab(name + suffix) != null) intactOf[(name + suffix).GetStableHashCode()] = hash;
                }
            }
            CarryOverRestockRegen(e);
            foreach (var spot in e.Sidecar.Spots)
                if (kindOf.ContainsKey(spot.Prefab) && KeyAt(DelveWorld.ToVector(spot.Position), spot.Prefab) == null) spots[SpotIndex.Key(spot.Position)] = spot;
            if (spots.Count != e.Sidecar.Spots.Count) SyncSidecar(); // drops spots of kinds Delve no longer tracks and duplicates
            ready = true;
            int before = spots.Count;
            foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
                if (intactOf.ContainsKey(zdo.GetPrefab())) Remember(zdo);
            foreach (var spot in spots.Values)
            {
                var zone = RegrowRules.Zone(spot.Position);
                if (!e.Sidecar.Visits.ContainsKey(zone))
                {
                    e.Sidecar.Visits[zone] = e.Now; // a zone never seen visited starts its clock now
                    e.MarkSidecarDirty();
                }
            }
            e.Log.Verbose("Delve world objects: " + spots.Count + " spots remembered (" + (spots.Count - before) + " new this start), " + e.Sidecar.Visits.Count + " zones.");
        }

        /// <summary>RestockRegen's worlds_local/&lt;world&gt;.restockregen.txt, once, copy only when absent (design 10).</summary>
        private static void CarryOverRestockRegen(DelveEngine e)
        {
            if (e.Sidecar.ImportedRestockRegen || e.Sidecar.ReadOnly) return;
            try
            {
                var w = ZNet.World;
                var source = w.m_fileSource == FileHelpers.FileSource.Cloud ? FileHelpers.FileSource.Local : w.m_fileSource;
                var path = Path.Combine(SaveSystem.GetWorldsSaveRootPath(source), w.m_name + ".restockregen.txt");
                if (!File.Exists(path)) return;
                var file = RecordFile.Parse(File.ReadAllText(path));
                var added = e.Sidecar.ImportRestockRegen(file);
                e.MarkSidecarDirty();
                e.Log.Info("Delve carried over RestockRegen's records: " + added.Spots + " spots and " + added.Visits + " zone visits (" + file.Errors.Count + " lines skipped); the file is left in place.");
            }
            catch (Exception error)
            {
                e.Log.Warning("Delve could not read RestockRegen's records: " + error.GetType().Name + ": " + error.Message);
            }
        }

        private void SyncSidecar()
        {
            E.Sidecar.Spots.Clear();
            E.Sidecar.Spots.AddRange(spots.Values);
            E.MarkSidecarDirty();
        }

        public void Stop(DelveEngine e)
        {
            ready = false;
            if (current == this) current = null;
            engine = null;
            spots.Clear();
            told.Clear();
        }

        /// <summary>The key of the remembered spot at this position, allowing for rounding, and for drift on a floating kind.</summary>
        private (int, int, int)? KeyAt(Vector3 p, int wholePrefab)
        {
            if (kindOf.TryGetValue(wholePrefab, out var info) && info.Drifts)
            {
                foreach (var kv in spots)
                    if (kv.Value.Prefab == wholePrefab && Vec3.Flat(kv.Value.Position, V(p)) < SpotIndex.DriftRadius) return kv.Key;
                return null;
            }
            var k = SpotIndex.Key(V(p));
            if (spots.ContainsKey(k)) return k;
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        var n = (k.Item1 + dx, k.Item2 + dy, k.Item3 + dz);
                        if (spots.TryGetValue(n, out var s) && Vec3.SqrDistance(s.Position, V(p)) < 0.04f) return n;
                    }
            return null;
        }

        private bool IsWhole(ZDO zdo)
        {
            bool drifts = kindOf.TryGetValue(zdo.GetPrefab(), out var info) && info.Drifts;
            bool anyChunkHit = drifts && chunkHealth.Any(key => zdo.GetFloat(key, float.MaxValue) != float.MaxValue);
            return RegrowRules.IsWhole(kindOf.ContainsKey(zdo.GetPrefab()), zdo.GetString(health), zdo.GetBool(ZDOVars.s_dead), anyChunkHit, drifts);
        }

        private void Remember(ZDO zdo)
        {
            int whole = intactOf[zdo.GetPrefab()];
            Vector3 p = zdo.GetPosition();
            if (KeyAt(p, whole) != null) return;
            var spot = new RecordSpot(whole, V(p), V(zdo.GetRotation().eulerAngles));
            spots[SpotIndex.Key(spot.Position)] = spot;
            E.Sidecar.Spots.Add(spot);
            var zone = Zone(p);
            if (!E.Sidecar.Visits.ContainsKey(zone)) E.Sidecar.Visits[zone] = E.Now;
            E.MarkSidecarDirty();
        }

        public void Check(DelveEngine e, ISet<DelveKind> sweeping)
        {
            if (!ready) return;
            var active = Kinds.Where(e.Active).ToList();
            if (active.Count == 0) return;
            Visits(e, active);
            Biomes(e, active);
            var due = active.Where(sweeping.Contains).ToList();
            if (due.Count > 0) Sweep(e, due);
        }

        /// <summary>Zones near players are visited now (a player within the kind's visit radius of any point of the zone).</summary>
        private void Visits(DelveEngine e, List<DelveKind> active)
        {
            float radius = active.Max(k => e.Settings.Get(DelveSettings.Kind(k).VisitRadius!));
            int reach = Mathf.CeilToInt(radius / ZoneSystem.c_ZoneSize);
            foreach (var player in e.Players)
            {
                if (player.Position.y >= DelveDungeonIndex.InteriorHeight) continue;
                var center = Zone(player.Position);
                for (int dx = -reach; dx <= reach; dx++)
                    for (int dz = -reach; dz <= reach; dz++)
                    {
                        var zone = (center.Item1 + dx, center.Item2 + dz);
                        if (!e.Sidecar.Visits.TryGetValue(zone, out var last) || !RegrowRules.ZoneNear(zone, V(player.Position), radius)) continue;
                        var since = last.CompletedWith(e.Now);
                        if (ClockRules.Elapsed(ClockKind.InGameDays, since, e.Now) < 0.01 && ClockRules.Elapsed(ClockKind.RealHours, since, e.Now) < 0.1) continue;
                        e.Sidecar.Visits[zone] = e.Now;
                        e.MarkSidecarDirty();
                    }
            }
        }

        /// <summary>Players entering a biome with a world kind on are told, at most once per cooldown (design D3).</summary>
        private void Biomes(DelveEngine e, List<DelveKind> active)
        {
            if (WorldGenerator.instance == null) return;
            double minutes = Time.realtimeSinceStartup / 60.0;
            double cooldownMinutes = e.Settings.Get(DelveSettings.Notices.BiomeCooldownMinutes);
            var present = new HashSet<long>();
            foreach (var player in e.Players)
            {
                present.Add(player.Uid);
                var biome = player.Position.y < DelveDungeonIndex.InteriorHeight ? WorldGenerator.instance.GetBiome(player.Position) : Heightmap.Biome.None;
                foreach (var kind in active)
                {
                    var info = KindCatalog.Of(kind);
                    if (info.Biome == null || !Enum.TryParse<Heightmap.Biome>(info.Biome, out var target)) continue;
                    if (!cooldown.Enter(player.Uid, info.Biome, biome == target, minutes, cooldownMinutes)) continue;
                    var (clock, delay) = e.ClockOf(kind);
                    DelveNotices.Tell(player, DelveText.ServerFormat(NoticeText.Biome(kind), DelveNotices.Duration(clock, ClockRules.WholeUnitsLeft(ClockRules.Clamp(clock, delay)))));
                }
            }
            foreach (var uid in told.Except(present).ToList()) cooldown.Forget(uid);
            told.Clear();
            told.UnionWith(present);
        }

        private void Sweep(DelveEngine e, List<DelveKind> due)
        {
            var standing = new Dictionary<(int, int, int), ZDO>();
            foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values.ToList())
            {
                if (!intactOf.ContainsKey(zdo.GetPrefab())) continue;
                Remember(zdo);
                var key = KeyAt(zdo.GetPosition(), intactOf[zdo.GetPrefab()]) ?? SpotIndex.Key(V(zdo.GetPosition()));
                if (!standing.TryGetValue(key, out ZDO other) || !IsWhole(other)) standing[key] = zdo;
            }
            var counts = due.ToDictionary(k => k, k => (Tracked: 0, Waiting: 0, Deferred: 0, Acted: 0, Blocked: new Dictionary<GuardResult, int>()));
            var empty = new GuardInput(new Vec3(0, 0, 0), 0f);
            foreach (var kv in spots.ToList())
            {
                if (!kindOf.TryGetValue(kv.Value.Prefab, out var info) || !counts.ContainsKey(info.Kind)) continue;
                var kind = info.Kind;
                var c = counts[kind];
                c.Tracked++;
                standing.TryGetValue(kv.Key, out ZDO here);
                var state = here == null ? SpotState.Missing : IsWhole(here) ? SpotState.Whole : SpotState.Damaged;
                var (clock, delay) = e.ClockOf(kind);
                bool now = e.RegenerateNow(kind);
                Vector3 pos = DelveWorld.ToVector(kv.Value.Position);
                e.Sidecar.Visits.TryGetValue(RegrowRules.Zone(kv.Value.Position), out var lastVisit);
                Stamp? last = e.Sidecar.Visits.ContainsKey(RegrowRules.Zone(kv.Value.Position)) ? lastVisit : (Stamp?)null;
                var cheap = RegrowRules.DecideAt(state, last, e.Now, clock, delay, now, empty);
                if (cheap == SpotOutcome.Whole)
                {
                    counts[kind] = c;
                    continue;
                }
                if (cheap == SpotOutcome.Waiting)
                {
                    c.Waiting++;
                    counts[kind] = c;
                    continue;
                }
                var ks = DelveSettings.Kind(kind);
                var guard = e.Guard(pos, e.Settings.Get(ks.VisitRadius!), e.Settings.Get(ks.BuildClearance!), DelveWorld.HeldByPlayer(here));
                var outcome = RegrowRules.DecideAt(state, last, e.Now, clock, delay, now, guard);
                GuardResult? blocked = outcome == SpotOutcome.Near ? GuardResult.PlayerNear : outcome == SpotOutcome.Built ? GuardResult.Built
                    : guard.BaseGuard && guard.Bases.Any(a => a.Contains(guard.Center)) ? GuardResult.PlayerBase : (GuardResult?)null;
                if (blocked.HasValue)
                {
                    c.Blocked[blocked.Value] = c.Blocked.TryGetValue(blocked.Value, out var n) ? n + 1 : 1;
                    counts[kind] = c;
                    continue;
                }
                if (e.Budget <= 0)
                {
                    c.Deferred++;
                    counts[kind] = c;
                    continue;
                }
                e.Budget--;
                c.Acted++;
                counts[kind] = c;
                bool dryRun = e.DryRun(kind);
                if (!dryRun)
                {
                    if (here != null) DelveWorld.Remove(here);
                    DelveWorld.Create(kv.Value.Prefab, pos, DelveWorld.ToVector(kv.Value.Rotation));
                }
                e.Record(kind, "restored " + (ZNetScene.instance.GetPrefab(kv.Value.Prefab)?.name ?? "?") + " at (" + pos.x.ToString("0") + ", " + pos.z.ToString("0") + ")"
                    + (here != null ? " (replacing a damaged one)" : " (was gone)") + (now ? " (regenerate now)" : ""), dryRun);
            }
            foreach (var kind in due)
            {
                var c = counts[kind];
                e.Swept(kind, c.Deferred);
                e.Report(kind, new KindReport(c.Tracked, c.Waiting, c.Deferred, c.Acted, c.Blocked, true, e.DryRun(kind)));
            }
        }

        [HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
        private static class SeeObjects
        {
            /// <summary>A stepped-aside part is never patched (design 9).</summary>
            private static bool Prepare() => !DelveModules.StepAside.SteppedAside(Integration.DelveModule.Resources);

            /// <summary>Remember an object the moment a player's update brings it in, so one generated and taken between two sweeps is not lost.</summary>
            private static void Postfix(ZDO __instance)
            {
                var service = current;
                if (service == null || !service.ready || ZNet.instance == null || !ZNet.instance.IsServer()) return;
                try
                {
                    if (service.intactOf.TryGetValue(__instance.GetPrefab(), out var whole) && service.kindOf.TryGetValue(whole, out var info) && service.E.Active(info.Kind))
                        service.Remember(__instance);
                }
                catch (Exception error) { Plugin.Log?.Warning("Delve world object update: " + error.GetType().Name + ": " + error.Message); }
            }
        }
    }
}
