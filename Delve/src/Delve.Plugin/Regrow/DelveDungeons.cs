// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/DungeonRegrow.cs: Init, AddDungeon, BossSpot, Remember, Tick, Welcome, Flush, Sweep, Occupied and the
// ZDO.Deserialize postfix). The MIT notice is in THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: one Delve engine service for the five dungeon resource kinds and the Queen; membership,
// welcome stages, room data and the per-dungeon decision are Delve.Core's DungeonSites, WelcomeMemory, RoomData and
// DungeonRegrowRules; records live in delve.<kind>.* keys on the generator (RestockRegen's keys are copied when Delve has
// none: converter carry-over); a dungeon acts only when its type's mode is Resources; regenerations count against the
// engine's action budget; visits are recorded in dry runs too (design B7); radii come from Delve.Sites.yml; notices are
// Delve catalog texts in the server language; the Queen's place is shared with a citadel's full reset.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Data;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Localization;
using ExtraPotions.Delve.Regrow;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Delve.Runtime
{
    public sealed class DelveDungeons : IDelveService
    {
        private static DelveDungeons? current;
        private readonly List<DungeonKind> kinds;
        private DelveEngine? engine;
        private SitesFile sites = SitesFile.Empty;
        private DateTime sitesStamp = DateTime.MinValue;

        public DelveDungeons()
        {
            kinds = new[] { DelveKind.MudPiles, DelveKind.FrostCaveCrystals, DelveKind.BlackCores, DelveKind.Morkhalla, DelveKind.Hole, DelveKind.Queen }
                .Select(k => new DungeonKind(this, KindCatalog.Of(k))).ToList();
            Kinds = kinds.Select(k => k.Kind).ToArray();
        }

        public IReadOnlyList<DelveKind> Kinds { get; }

        internal DelveEngine Engine => engine ?? throw new InvalidOperationException("Delve dungeons are not started.");

        internal IEnumerable<DungeonSiteReport> ReadSites(DelveEngine e, Stamp now)
        {
            if (!ReferenceEquals(engine, e)) yield break;
            foreach (var kind in kinds)
                if (e.Active(kind.Kind))
                    foreach (var site in kind.ReadSites(now)) yield return site;
        }

        public void Start(DelveEngine e)
        {
            engine = e;
            current = this;
            ReadSites();
            foreach (var k in kinds) k.Start();
        }

        public void Stop(DelveEngine e)
        {
            foreach (var k in kinds) k.Flush();
            foreach (var k in kinds) k.Clear();
            if (current == this) current = null;
            engine = null;
        }

        public void Check(DelveEngine e, ISet<DelveKind> sweeping)
        {
            ReadSites();
            foreach (var k in kinds)
            {
                if (!e.Active(k.Kind))
                {
                    k.Flush();
                    continue;
                }
                k.Visit();
                if (sweeping.Contains(k.Kind))
                {
                    long sweepStart = DelveCheckTiming.Start(e.CheckTiming);
                    try { k.Sweep(); }
                    finally { e.CheckTiming?.Stage("dungeon_sweep", sweepStart, k.Kind); }
                }
                k.Flush();
            }
        }

        /// <summary>The entrance and inside radii of a generator type (Delve.Sites.yml, read again when it changes).</summary>
        internal (float Entrance, float Inside) Radii(KindInfo info) =>
            sites.RadiiOf(info.Generator!, info.EntranceRadius, info.InsideRadius);

        private void ReadSites()
        {
            try
            {
                var path = Path.Combine(DelveImport.DataDirectory, SitesFile.FileName);
                var stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
                if (stamp == sitesStamp) return;
                sitesStamp = stamp;
                sites = stamp == DateTime.MinValue ? SitesFile.Empty : SitesFile.Parse(File.ReadAllText(path));
                foreach (var error in sites.Errors) engine?.Log.Warning("Delve " + SitesFile.FileName + ": " + error);
            }
            catch (Exception error)
            {
                engine?.Log.Warning("Delve " + SitesFile.FileName + " unreadable: " + error.GetType().Name + "; keeping the radii read before.");
            }
        }

        /// <summary>
        /// Where the game put the Queen in a citadel: her room's saved position and rotation (the generator's room data)
        /// applied to her place in the room prefab. False when the generator is gone or its rooms hold no boss room. The
        /// Queen's return and the full reset of a citadel both read it.
        /// </summary>
        internal static bool CitadelBossSpot(ZDO? generator, out Vector3 position, out Vector3 euler)
        {
            position = default;
            euler = default;
            var room = RoomData.Find(RoomData.Parse(generator?.GetByteArray(ZDOVars.s_roomData)), KindCatalog.Citadel.BossRoom.GetStableHashCode());
            if (!room.HasValue) return false;
            Quaternion rot = Quaternion.Euler(DelveWorld.ToVector(room.Value.Euler));
            position = DelveWorld.ToVector(room.Value.Position) + rot * DelveWorld.ToVector(KindCatalog.Citadel.BossOffset);
            euler = (rot * Quaternion.Euler(0f, KindCatalog.Citadel.BossYaw, 0f)).eulerAngles;
            return true;
        }

        [HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
        private static class SeeThings
        {
            /// <summary>A stepped-aside part is never patched (design 9).</summary>
            private static bool Prepare() => !DelveModules.StepAside.SteppedAside(Integration.DelveModule.Resources);

            /// <summary>Remember a thing the moment a player's update brings it in, so one generated and taken between two sweeps is not lost.</summary>
            private static void Postfix(ZDO __instance)
            {
                var service = current;
                if (service?.engine == null || ZNet.instance == null || !ZNet.instance.IsServer()) return;
                try
                {
                    foreach (var k in service.kinds)
                        if (k.Tracks(__instance.GetPrefab()) && service.engine.Active(k.Kind)) k.Remember(__instance);
                }
                catch (Exception error) { Plugin.Log?.Warning("Delve dungeon update: " + error.GetType().Name + ": " + error.Message); }
            }
        }

        /// <summary>One dungeon of one kind: its generator, entrance (the door at the surface where the kind has one), spots and last visit.</summary>
        private sealed class Site
        {
            public ZDOID Generator;
            public Vector3 Position;
            public Vector3 Entrance;
            public ZDOID Door = ZDOID.None;
            public readonly Dictionary<(int, int, int), RecordSpot> Spots = new Dictionary<(int, int, int), RecordSpot>();
            public Stamp? LastVisit;
            public bool SpotsDirty, VisitDirty;
            public string Id => Generator.ToString();
        }

        private sealed class DungeonKind
        {
            private readonly DelveDungeons owner;
            private readonly KindInfo info;
            private readonly int generator, spotsKey, visitDay, visitUtc, rrSpotsKey, rrVisitKey, boss, door;
            private readonly Dictionary<int, int> intactOf = new Dictionary<int, int>();
            private readonly HashSet<int> intact = new HashSet<int>();
            private readonly Dictionary<ZDOID, Site> sitesById = new Dictionary<ZDOID, Site>();
            private readonly List<Vector3> otherDungeons = new List<Vector3>();
            private readonly Dictionary<(int, int, int), RecordSpot> unassigned = new Dictionary<(int, int, int), RecordSpot>();
            private readonly WelcomeMemory welcomed = new WelcomeMemory();
            private long geometryRevision;
            private bool ready;

            public DungeonKind(DelveDungeons owner, KindInfo info)
            {
                this.owner = owner;
                this.info = info;
                generator = info.Generator!.GetStableHashCode();
                spotsKey = DelveKeys.Spots(info.Kind).GetStableHashCode();
                visitDay = DelveKeys.VisitDay(info.Kind).GetStableHashCode();
                visitUtc = DelveKeys.VisitUtc(info.Kind).GetStableHashCode();
                rrSpotsKey = (info.RestockRegenSpotsKey ?? "").GetStableHashCode();
                rrVisitKey = (info.RestockRegenVisitKey ?? "").GetStableHashCode();
                if (info.Kind == DelveKind.Queen)
                {
                    boss = KindCatalog.Citadel.Boss.GetStableHashCode();
                    door = KindCatalog.Citadel.Door.GetStableHashCode();
                }
            }

            public DelveKind Kind => info.Kind;
            private DelveEngine E => owner.Engine;
            private bool BossKind => boss != 0;

            public bool Tracks(int prefab) => ready && intactOf.ContainsKey(prefab);

            public IEnumerable<DungeonSiteReport> ReadSites(Stamp now)
            {
                if (!ready) yield break;
                var (clock, delay) = E.ClockOf(Kind);
                foreach (var site in sitesById.Values)
                {
                    var native = ZDOMan.instance.GetZDO(site.Generator);
                    if (native == null || native.GetPrefab() != generator || !ModeAllows(site)) continue;
                    yield return new DungeonSiteReport(site.Id, Kind, info.Generator!, V(site.Entrance), clock, delay,
                        site.LastVisit, now, E.RegenerateNow(Kind), E.DryRun(Kind));
                }
            }

            public void Clear()
            {
                geometryRevision++;
                ready = false;
                sitesById.Clear();
                otherDungeons.Clear();
                unassigned.Clear();
            }

            private static Vec3 V(Vector3 v) => DelveWorld.ToVec(v);

            private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

            /// <summary>Whole: the whole prefab, and not a pickable that was picked and left standing.</summary>
            private bool IsWhole(ZDO zdo) => intact.Contains(zdo.GetPrefab()) && !zdo.GetBool(ZDOVars.s_picked);

            public void Start()
            {
                Clear();
                intactOf.Clear();
                intact.Clear();
                foreach (string name in info.Whole)
                {
                    if (ZNetScene.instance.GetPrefab(name) == null)
                    {
                        E.Log.Warning("Delve " + Kind + ": prefab " + name + " not found, skipped.");
                        continue;
                    }
                    int whole = name.GetStableHashCode();
                    intact.Add(whole);
                    intactOf[whole] = whole;
                    foreach (string suffix in new[] { "_frac", "_destruction" })
                        if (ZNetScene.instance.GetPrefab(name + suffix) != null) intactOf[(name + suffix).GetStableHashCode()] = whole;
                }
                var things = new List<ZDO>();
                var doors = new List<ZDO>();
                foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
                {
                    int prefab = zdo.GetPrefab();
                    if (prefab == generator) AddSite(zdo);
                    else if (BossKind && prefab == door) doors.Add(zdo);
                    else if (DelveDungeonIndex.IsGenerator(prefab))
                    {
                        otherDungeons.Add(zdo.GetPosition());
                        geometryRevision++;
                    }
                    else if (intactOf.ContainsKey(prefab)) things.Add(zdo);
                }
                if (BossKind) FindDoors(doors);
                ready = true;
                int added = things.Count(t => Remember(t));
                E.Log.Verbose("Delve " + Kind + ": " + sitesById.Count + " dungeons, " + sitesById.Values.Sum(s => s.Spots.Count) + " spots remembered (" + added + " new this start).");
            }

            private Site AddSite(ZDO zdo)
            {
                if (sitesById.TryGetValue(zdo.m_uid, out var site)) return site;
                site = new Site { Generator = zdo.m_uid, Position = zdo.GetPosition(), Entrance = zdo.GetPosition() };
                site.LastVisit = DelveKeys.Decode(zdo.GetFloat(visitDay), zdo.GetLong(visitUtc));
                foreach (var spot in DungeonSpots.Decode(zdo.GetByteArray(spotsKey))) site.Spots[SpotIndex.Key(spot.Position)] = spot;
                // Converter carry-over: RestockRegen's records on the same generator, copied only when Delve has none.
                if (site.Spots.Count == 0 && info.RestockRegenSpotsKey != null)
                    foreach (var spot in DungeonSpots.Decode(zdo.GetByteArray(rrSpotsKey)))
                    {
                        site.Spots[SpotIndex.Key(spot.Position)] = spot;
                        site.SpotsDirty = true;
                    }
                if (!site.LastVisit.HasValue && info.RestockRegenVisitKey != null)
                {
                    int rr = zdo.GetInt(rrVisitKey);
                    if (rr > 0)
                    {
                        site.LastVisit = Stamp.DayOnly(rr - 1);
                        site.VisitDirty = true;
                    }
                }
                sitesById[zdo.m_uid] = site;
                geometryRevision++;
                return site;
            }

            private Site? SiteAt(Vector3 p)
            {
                if (p.y < DungeonSites.InteriorHeight) return null;
                var list = sitesById.Values.ToList();
                var index = DungeonSites.InsideOf(V(p), list.Select(s => V(s.Position)).ToList(), otherDungeons.Select(V).ToList(), owner.Radii(info).Inside);
                return index.HasValue ? list[index.Value] : null;
            }

            private Site? EntranceAt(Vector3 p)
            {
                if (p.y >= DungeonSites.InteriorHeight) return null;
                var list = sitesById.Values.ToList();
                var index = DungeonSites.EntranceOf(V(p), list.Select(s => V(s.Entrance)).ToList(), owner.Radii(info).Entrance);
                return index.HasValue ? list[index.Value] : null;
            }

            public bool Remember(ZDO thing)
            {
                var spot = new RecordSpot(intactOf[thing.GetPrefab()], V(thing.GetPosition()), V(thing.GetRotation().eulerAngles));
                return Remember(spot);
            }

            private bool Remember(RecordSpot spot)
            {
                var site = SiteAt(DelveWorld.ToVector(spot.Position));
                var key = SpotIndex.Key(spot.Position);
                if (site == null)
                {
                    unassigned[key] = spot; // its dungeon's generator is not known yet
                    return false;
                }
                if (site.Spots.ContainsKey(key)) return false;
                site.Spots[key] = spot;
                site.SpotsDirty = true;
                return true;
            }

            private bool ModeAllows(Site site) => DungeonModes.Allows(DelveDungeonIndex.ModeOf(info.Generator!), Kind);

            /// <summary>Every check: a player inside restarts the clock; players at an entrance or inside are told once per trip.</summary>
            public void Visit()
            {
                if (!ready) return;
                var present = new HashSet<long>();
                var entranceRadius = owner.Radii(info).Entrance;
                foreach (var player in E.Players)
                {
                    present.Add(player.Uid);
                    var inside = SiteAt(player.Position);
                    if (inside != null && ModeAllows(inside))
                    {
                        var last = inside.LastVisit;
                        if (!last.HasValue || ClockRules.Elapsed(ClockKind.InGameDays, last.Value.CompletedWith(E.Now), E.Now) >= 0.01
                            || ClockRules.Elapsed(ClockKind.RealHours, last.Value.CompletedWith(E.Now), E.Now) >= 0.1)
                        {
                            inside.LastVisit = E.Now;
                            inside.VisitDirty = true;
                        }
                    }
                    var here = inside ?? EntranceAt(player.Position);
                    if (here == null)
                    {
                        var told = welcomed.SiteOf(player.Uid);
                        if (told != null && (player.Position.y >= DungeonSites.InteriorHeight
                            || !sitesById.Values.Any(s => s.Id == told && Flat(player.Position, s.Entrance) < entranceRadius * 2f)))
                            welcomed.Leave(player.Uid);
                        continue;
                    }
                    if (!ModeAllows(here)) continue;
                    if (welcomed.Enter(player.Uid, here.Id, inside != null ? 2 : 1)) Welcome(player, here, inside != null);
                }
                foreach (var gone in welcomed.Players.Where(uid => !present.Contains(uid)).ToList()) welcomed.Leave(gone);
            }

            /// <summary>Tell a player at or in a dungeon that what was taken will come back, if anything was.</summary>
            private void Welcome(DelvePlayer player, Site site, bool inside)
            {
                if (!DelveNotices.On || (!BossKind && site.Spots.Count == 0)) return;
                var nearby = DelveWorld.ObjectsNear(site.Position, 4 * ZoneSystem.c_ZoneSize);
                var whole = new HashSet<(int, int, int)>(nearby.Where(IsWhole).Select(z => SpotIndex.Key(V(z.GetPosition()))));
                int taken = site.Spots.Keys.Count(k => !whole.Contains(k));
                if (BossKind ? nearby.Any(z => z.GetPrefab() == boss && SiteAt(z.GetPosition()) == site) : taken == 0) return;
                var (clock, delay) = E.ClockOf(Kind);
                int units = DungeonRegrowRules.UnitsLeft(site.LastVisit, E.Now, clock, delay, inside);
                var notice = inside ? SiteNotice.Inside : units > 0 ? SiteNotice.Entrance : SiteNotice.Due;
                var key = NoticeText.Site(Kind, notice);
                DelveNotices.Tell(player, notice == SiteNotice.Due ? DelveText.ServerText(key) : DelveText.ServerFormat(key, DelveNotices.Duration(clock, units)));
            }

            /// <summary>Writes pending records to each generator, never while a player holds it.</summary>
            public void Flush()
            {
                foreach (var site in sitesById.Values)
                {
                    if (!site.SpotsDirty && !site.VisitDirty) continue;
                    ZDO zdo = ZDOMan.instance?.GetZDO(site.Generator);
                    if (zdo == null || DelveWorld.HeldByPlayer(zdo)) continue;
                    if (site.VisitDirty)
                    {
                        zdo.Set(visitDay, DelveKeys.EncodeDay(site.LastVisit));
                        zdo.Set(visitUtc, DelveKeys.EncodeUtc(site.LastVisit));
                        site.VisitDirty = false;
                    }
                    if (site.SpotsDirty)
                    {
                        zdo.Set(spotsKey, DungeonSpots.Encode(site.Spots.Values.ToList()));
                        site.SpotsDirty = false;
                    }
                }
            }

            /// <summary>Where the game put the boss (<see cref="CitadelBossSpot"/>).</summary>
            private bool BossSpot(Site site, out RecordSpot spot)
            {
                spot = default;
                if (!CitadelBossSpot(ZDOMan.instance.GetZDO(site.Generator), out Vector3 pos, out Vector3 euler)) return false;
                spot = new RecordSpot(boss, V(pos), V(euler));
                return true;
            }

            /// <summary>At dawn (day clock), every check (real hours) or on regenerate now.</summary>
            public void Sweep()
            {
                if (!ready) return;
                bool dryRun = E.DryRun(Kind);
                bool now = E.RegenerateNow(Kind);
                var (clock, delay) = E.ClockOf(Kind);
                var standing = new Dictionary<(int, int, int), ZDO>();
                var doors = new List<ZDO>();
                var bosses = new List<ZDO>();
                long scanStart = DelveCheckTiming.Start(E.CheckTiming);
                foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
                {
                    int prefab = zdo.GetPrefab();
                    if (prefab == generator) AddSite(zdo);
                    else if (BossKind && prefab == door) doors.Add(zdo);
                    else if (BossKind && prefab == boss) bosses.Add(zdo);
                    else if (intactOf.ContainsKey(prefab))
                    {
                        var key = SpotIndex.Key(V(zdo.GetPosition()));
                        if (!standing.TryGetValue(key, out ZDO other) || !IsWhole(other)) standing[key] = zdo; // a whole one wins
                    }
                }
                E.CheckTiming?.Stage("dungeon_scan", scanStart, Kind, ZDOMan.instance.m_objectsByID.Count, standing.Count);
                foreach (ZDO thing in standing.Values) Remember(thing);
                foreach (var spot in unassigned.Values.ToList())
                {
                    unassigned.Remove(SpotIndex.Key(spot.Position));
                    Remember(spot);
                }
                if (BossKind) FindDoors(doors);
                var bossAlive = new HashSet<Site>(bosses.Select(b => SiteAt(b.GetPosition())).Where(s => s != null)!);
                int tracked = 0, waiting = 0, deferred = 0, acted = 0;
                var blocked = new Dictionary<GuardResult, int>();
                SweepOccupancy<Site, DelvePlayer>? playerSites = null;
                string? note = null;
                foreach (var site in sitesById.Values.ToList())
                {
                    if (!ModeAllows(site)) continue;
                    tracked++;
                    RecordSpot bossSpot = default;
                    bool place = !BossKind || BossSpot(site, out bossSpot);
                    var missing = site.Spots.Where(kv => !(standing.TryGetValue(kv.Key, out ZDO z) && IsWhole(z))).ToList();
                    playerSites ??= new SweepOccupancy<Site, DelvePlayer>(ProjectPlayers);
                    bool occupied = Occupied(site, standing, playerSites.Contains(site, E.Players, geometryRevision, owner.Radii(info).Inside));
                    var outcome = DungeonRegrowRules.Decide(site.LastVisit, E.Now, clock, delay, now, BossKind, bossAlive.Contains(site), place, missing.Count, occupied);
                    if (outcome == DungeonOutcome.Regenerate && E.Settings.Get(DelveSettings.General.PlayerBaseGuard) && DelveBases.Near(site.Entrance).Any(a => a.Contains(V(site.Entrance))))
                    {
                        blocked[GuardResult.PlayerBase] = blocked.TryGetValue(GuardResult.PlayerBase, out var n) ? n + 1 : 1;
                        continue;
                    }
                    switch (outcome)
                    {
                        case DungeonOutcome.FirstSight:
                            site.LastVisit = E.Now;
                            site.VisitDirty = true;
                            continue;
                        case DungeonOutcome.Waiting:
                            waiting++;
                            continue;
                        case DungeonOutcome.NoBossPlace:
                            note = "a citadel's boss room was not found";
                            continue;
                        case DungeonOutcome.Occupied:
                            blocked[GuardResult.PlayerNear] = blocked.TryGetValue(GuardResult.PlayerNear, out var o) ? o + 1 : 1;
                            if (now)
                            {
                                // Regenerate-now could not reach it: make it due so the first sweep that finds it free does it.
                                site.LastVisit = clock == ClockKind.RealHours ? Stamp.UtcOnly(E.Now.Utc.AddHours(-delay)) : Stamp.DayOnly(E.Now.Day - delay);
                                site.VisitDirty = true;
                            }
                            continue;
                        case DungeonOutcome.Regenerate:
                            break;
                        default:
                            continue;
                    }
                    if (E.Budget <= 0)
                    {
                        deferred++;
                        continue;
                    }
                    E.Budget--;
                    acted++;
                    int replaced = 0, rebuilt = 0, built = 0;
                    float clearance = E.Settings.Get(DelveSettings.DungeonResources.BuildClearance);
                    foreach (var kv in missing)
                    {
                        if (!DungeonRegrowRules.SpotClear(kv.Value.Position, clearance, DelveWorld.PlayerBuiltNear(DelveWorld.ToVector(kv.Value.Position), clearance)))
                        {
                            built++; // a player built here: this spot stays gone (user decision R-Q2)
                            continue;
                        }
                        if (standing.TryGetValue(kv.Key, out ZDO damaged))
                        {
                            replaced++;
                            if (intact.Contains(damaged.GetPrefab()))
                            {
                                // The whole prefab, picked and left standing: unpick it in place.
                                if (!dryRun)
                                {
                                    damaged.Set(ZDOVars.s_picked, false);
                                    damaged.Set(ZDOVars.s_pickedTime, 0L);
                                }
                                continue;
                            }
                            if (!dryRun) DelveWorld.Remove(damaged);
                        }
                        else rebuilt++;
                        if (!dryRun) DelveWorld.Create(kv.Value.Prefab, DelveWorld.ToVector(kv.Value.Position), DelveWorld.ToVector(kv.Value.Rotation));
                    }
                    if (BossKind)
                    {
                        rebuilt++;
                        if (!dryRun)
                        {
                            DelveWorld.Create(bossSpot.Prefab, DelveWorld.ToVector(bossSpot.Position), DelveWorld.ToVector(bossSpot.Rotation));
                            ZDO doorZdo = ZDOMan.instance.GetZDO(site.Door);
                            if (doorZdo != null && E.Settings.Get(DelveSettings.Bosses.CloseSealedDoor)) doorZdo.Set(ZDOVars.s_state, 0); // 0 is closed
                        }
                    }
                    site.LastVisit = E.Now; // the clock starts again
                    site.VisitDirty = true;
                    E.Record(Kind, "regenerated " + info.Generator + " at (" + site.Position.x.ToString("0") + ", " + site.Position.z.ToString("0") + ")"
                        + (now ? " (regenerate now)" : "") + ": " + replaced + " damaged replaced, " + rebuilt + " gone rebuilt"
                        + (built > 0 ? ", " + built + " held back by player buildings" : "") + (BossKind ? ", the Queen returned" : ""), dryRun);
                    if (built > 0) blocked[GuardResult.Built] = blocked.TryGetValue(GuardResult.Built, out var b) ? b + built : built;
                }
                E.Swept(Kind, deferred);
                E.Report(Kind, new KindReport(tracked, waiting, deferred, acted, blocked, true, dryRun, note));
            }

            /// <summary>Each citadel's door at the surface: the nearest within 100 m, flat. Its entrance is measured from the door.</summary>
            private void FindDoors(List<ZDO> doors)
            {
                var positions = doors.Select(d => V(d.GetPosition())).ToList();
                foreach (var site in sitesById.Values)
                {
                    var i = DungeonSites.NearestDoor(V(site.Position), positions);
                    if (!i.HasValue) continue;
                    site.Door = doors[i.Value].m_uid;
                    site.Entrance = doors[i.Value].GetPosition();
                }
            }

            private HashSet<Site> ProjectPlayers(IReadOnlyList<DelvePlayer> players)
            {
                var occupied = new HashSet<Site>();
                foreach (var player in players)
                {
                    var site = SiteAt(player.Position);
                    if (site != null) occupied.Add(site);
                }
                return occupied;
            }

            /// <summary>A player up in the dungeon, or holding its generator, its door or any of its objects.</summary>
            private bool Occupied(Site site, Dictionary<(int, int, int), ZDO> standing, bool playerInside)
            {
                if (playerInside) return true;
                if (DelveWorld.HeldByPlayer(ZDOMan.instance.GetZDO(site.Generator)) || DelveWorld.HeldByPlayer(ZDOMan.instance.GetZDO(site.Door))) return true;
                foreach (var key in site.Spots.Keys)
                    if (standing.TryGetValue(key, out ZDO zdo) && DelveWorld.HeldByPlayer(zdo)) return true;
                return false;
            }
        }
    }
}
