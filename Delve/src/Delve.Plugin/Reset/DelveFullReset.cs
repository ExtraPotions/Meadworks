// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the full dungeon reset runtime (design C1, D4, DV09). Behavior from MoreDungeonResets' README and
// config only (behavior reference; nothing of it is read or copied). Dungeons whose type is Full in Delve.Dungeons.yml
// are tracked: a player inside restarts the clock, players at the entrance and inside are warned, and once due with every
// guard clear a replacement is generated first. The old interior is removed only after that generation returns a ZDO.
// If it does not, the pieces created by that attempt are removed and the old dungeon stays. Generation uses ghost spawning,
// the way the game generates a zone it has not loaded, so the new rooms use the current room counts. The DV01b probe is accepted (user
// decision 2026-10-10), so resets run live unless Full Reset Dry Run or Dry Run All is on.
// A citadel's reset also places the Queen in her room when the new rooms did not bring her, and locks its sealed door
// again (Close Sealed Door), as the Queen's return does.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Localization;
using ExtraPotions.Delve.Regrow;
using ExtraPotions.Delve.Reset;
using UnityEngine;

namespace ExtraPotions.Delve.Runtime
{
    public sealed class DelveFullReset : IDelveService
    {
        private const float DefaultEntrance = 30f;
        private static readonly int visitDay = DelveKeys.VisitDay(DelveKind.FullReset).GetStableHashCode();
        private static readonly int visitUtc = DelveKeys.VisitUtc(DelveKind.FullReset).GetStableHashCode();
        private static readonly int forcedRetry = DelveKeys.FullResetRetry.GetStableHashCode();
        private static readonly int tombstone = "Player_tombstone".GetStableHashCode();
        private static readonly int player = "Player".GetStableHashCode();
        private static readonly int queen = KindCatalog.Citadel.Boss.GetStableHashCode();
        private static readonly int queenDoor = KindCatalog.Citadel.Door.GetStableHashCode();

        private readonly Dictionary<ZDOID, Stamp> pendingVisits = new Dictionary<ZDOID, Stamp>();
        private readonly WelcomeMemory welcomed = new WelcomeMemory();
        private DelveEngine? engine;
        private readonly Dictionary<ZDOID, SiteRequest> requests = new Dictionary<ZDOID, SiteRequest>();

        private sealed class SiteRequest
        {
            internal ZDO Native = null!;
            internal int Prefab;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal bool SkipDelay;
            internal Func<bool> Allowed = null!;
        }

        internal bool Request(DelveEngine e, string id, bool skipDelay, Func<bool> allowed)
        {
            if (!ReferenceEquals(engine, e) || string.IsNullOrWhiteSpace(id) || id.Length > 128 || requests.Count >= 16) return false;
            foreach (var site in DelveDungeonIndex.KnownGenerators)
            {
                var native = site.Zdo;
                if (native.m_uid.ToString() != id) continue;
                if (!ReferenceEquals(ZDOMan.instance.GetZDO(native.m_uid), native) || requests.ContainsKey(native.m_uid)
                    || DelveDungeonIndex.NameOf(native.GetPrefab()) != site.Name || DelveDungeonIndex.ModeOf(site.Name) != DungeonMode.Full) return false;
                requests.Add(native.m_uid, new SiteRequest { Native = native, Prefab = native.GetPrefab(), Position = native.GetPosition(),
                    Rotation = native.GetRotation(), SkipDelay = skipDelay, Allowed = allowed });
                return true;
            }
            return false;
        }

        private Dictionary<ZDOID, SiteRequest> TakeRequests(DelveEngine e)
        {
            var accepted = new Dictionary<ZDOID, SiteRequest>();
            var pending = requests.ToArray(); requests.Clear();
            foreach (var request in pending)
            {
                var r = request.Value;
                if (!RequestCurrent(request.Key, r))
                {
                    e.Record(DelveKind.FullReset, "site request " + request.Key + " refused: authority, permission or native generator changed", false);
                    continue;
                }
                accepted.Add(request.Key, r);
            }
            return accepted;
        }

        private static bool RequestCurrent(ZDOID id, SiteRequest r)
        {
            var current = ZDOMan.instance.GetZDO(id);
            if (!ReferenceEquals(current, r.Native) || current.GetPrefab() != r.Prefab
                || current.GetPosition() != r.Position || current.GetRotation() != r.Rotation
                || DelveDungeonIndex.NameOf(r.Prefab) is not string name || DelveDungeonIndex.ModeOf(name) != DungeonMode.Full) return false;
            try { return r.Allowed(); } catch { return false; }
        }

        public IReadOnlyList<DelveKind> Kinds { get; } = new[] { DelveKind.FullReset };

        public void Start(DelveEngine e)
        {
            engine = e;
            pendingVisits.Clear();
            requests.Clear();
        }

        public void Stop(DelveEngine e)
        {
            Flush();
            pendingVisits.Clear();
            requests.Clear();
            engine = null;
        }

        private static Vec3 V(Vector3 v) => DelveWorld.ToVec(v);

        /// <summary>The generator's interior volume, from its prefab's zone size around its zone center.</summary>
        private static Box InteriorOf(ZDO generator)
        {
            var prefab = ZNetScene.instance.GetPrefab(generator.GetPrefab());
            var dg = prefab != null ? prefab.GetComponent<DungeonGenerator>() : null;
            Vector3 size = dg != null ? dg.m_zoneSize : new Vector3(64f, 64f, 64f);
            Vector3 center = ZoneSystem.GetZonePos(ZoneSystem.GetZone(generator.GetPosition()));
            return FullResetRules.Interior(V(generator.GetPosition()), V(size), V(center));
        }

        private static IEnumerable<(ZDO Zdo, string Name)> FullSites() =>
            DelveDungeonIndex.Generators().Where(g => DelveDungeonIndex.ModeOf(g.Name) == DungeonMode.Full);

        private Stamp? LastVisit(ZDO generator) =>
            pendingVisits.TryGetValue(generator.m_uid, out var s) ? s : DelveKeys.Decode(generator.GetFloat(visitDay), generator.GetLong(visitUtc));

        private void SetVisit(ZDO generator, Stamp stamp) => pendingVisits[generator.m_uid] = stamp;

        internal IEnumerable<DungeonSiteReport> ReadSites(DelveEngine e, Stamp now)
        {
            if (!ReferenceEquals(engine, e) || !e.Active(DelveKind.FullReset)) yield break;
            var (clock, delay) = e.ClockOf(DelveKind.FullReset);
            // Reuse the inventory populated by normal owner checks; a display request never scans all native objects.
            foreach (var known in DelveDungeonIndex.KnownGenerators)
            {
                var native = known.Zdo; var name = known.Name;
                if (!ReferenceEquals(ZDOMan.instance.GetZDO(native.m_uid), native)
                    || DelveDungeonIndex.NameOf(native.GetPrefab()) != name || DelveDungeonIndex.ModeOf(name) != DungeonMode.Full) continue;
                bool pending = requests.TryGetValue(native.m_uid, out var request);
                yield return new DungeonSiteReport(native.m_uid.ToString(), DelveKind.FullReset, name, V(native.GetPosition()),
                    clock, delay, LastVisit(native), now, e.RegenerateNow(DelveKind.FullReset) || native.GetBool(forcedRetry) || pending && request!.SkipDelay,
                    FullResetRules.EffectiveDryRun(e.DryRun(DelveKind.FullReset)), pending);
            }
        }

        public void Check(DelveEngine e, ISet<DelveKind> sweeping)
        {
            if (!e.Active(DelveKind.FullReset))
            {
                requests.Clear();
                Flush();
                return;
            }
            var exact = TakeRequests(e);
            var sites = FullSites().ToList();
            Visit(e, sites);
            bool scheduled = sweeping.Contains(DelveKind.FullReset);
            if (scheduled || exact.Count > 0 || sites.Any(site => site.Zdo.GetBool(forcedRetry))) Sweep(e, sites, exact, scheduled);
            Flush();
        }

        private void Visit(DelveEngine e, List<(ZDO Zdo, string Name)> sites)
        {
            var present = new HashSet<long>();
            bool warn = e.Settings.Get(DelveSettings.Dungeons.WarnAtEntrance);
            foreach (var p in e.Players)
            {
                present.Add(p.Uid);
                (ZDO Zdo, string Name)? here = null;
                bool inside = false;
                foreach (var site in sites)
                {
                    if (InteriorOf(site.Zdo).Contains(V(p.Position)))
                    {
                        here = site;
                        inside = true;
                        var last = LastVisit(site.Zdo);
                        if (!last.HasValue || ClockRules.Elapsed(ClockKind.InGameDays, last.Value.CompletedWith(e.Now), e.Now) >= 0.01
                            || ClockRules.Elapsed(ClockKind.RealHours, last.Value.CompletedWith(e.Now), e.Now) >= 0.1)
                            SetVisit(site.Zdo, e.Now);
                        break;
                    }
                    Vector3 g = site.Zdo.GetPosition();
                    if (p.Position.y < DungeonSites.InteriorHeight && new Vector2(p.Position.x - g.x, p.Position.z - g.z).magnitude < DefaultEntrance)
                        here = site;
                }
                if (here == null)
                {
                    welcomed.Leave(p.Uid);
                    continue;
                }
                if (warn && welcomed.Enter(p.Uid, here.Value.Zdo.m_uid.ToString(), inside ? 2 : 1))
                {
                    var (clock, delay) = e.ClockOf(DelveKind.FullReset);
                    int units = DungeonRegrowRules.UnitsLeft(LastVisit(here.Value.Zdo), e.Now, clock, delay, inside);
                    var notice = inside ? SiteNotice.Inside : units > 0 ? SiteNotice.Entrance : SiteNotice.Due;
                    var key = NoticeText.Site(DelveKind.FullReset, notice);
                    DelveNotices.Tell(p, notice == SiteNotice.Due ? DelveText.ServerText(key) : DelveText.ServerFormat(key, DelveNotices.Duration(clock, units)));
                }
            }
            foreach (var gone in welcomed.Players.Where(uid => !present.Contains(uid)).ToList()) welcomed.Leave(gone);
        }

        private void Sweep(DelveEngine e, List<(ZDO Zdo, string Name)> sites, IReadOnlyDictionary<ZDOID, SiteRequest> exact, bool scheduled)
        {
            bool dryRun = FullResetRules.EffectiveDryRun(e.DryRun(DelveKind.FullReset));
            var (clock, delay) = e.ClockOf(DelveKind.FullReset);
            bool globalNow = e.RegenerateNow(DelveKind.FullReset);
            bool clearance = e.Settings.Get(DelveSettings.Dungeons.BuildClearance);
            bool baseGuard = e.Settings.Get(DelveSettings.General.PlayerBaseGuard);
            int tracked = 0, waiting = 0, deferred = 0, acted = 0;
            var blocked = new Dictionary<GuardResult, int>();
            int tombstones = 0;
            foreach (var site in sites)
            {
                var generator = site.Zdo;
                bool retry = generator.GetBool(forcedRetry);
                bool requested = exact.TryGetValue(generator.m_uid, out var request);
                if (!scheduled && !requested && !retry) continue;
                if (requested && !RequestCurrent(generator.m_uid, request!))
                {
                    e.Record(DelveKind.FullReset, "site request " + generator.m_uid + " refused before guard evaluation; no retry queued", false);
                    continue;
                }
                tracked++;
                bool now = globalNow || retry || requested && request!.SkipDelay;
                var box = InteriorOf(generator);
                var objects = DelveWorld.ObjectsNear(generator.GetPosition(), Math.Max(box.Max.X - box.Min.X, box.Max.Z - box.Min.Z) / 2f)
                    .Where(z => box.Contains(V(z.GetPosition()))).ToList();
                bool playerInside = e.Players.Any(p => box.Contains(V(p.Position))) || objects.Any(z => z.GetPrefab() == player);
                bool tomb = objects.Any(z => z.GetPrefab() == tombstone);
                bool built = objects.Any(z => z.GetPrefab() != tombstone && z.GetLong(ZDOVars.s_creator) != 0L);
                bool inBase = baseGuard && DelveBases.Near(generator.GetPosition()).Any(a => a.Contains(V(generator.GetPosition())));
                var outcome = FullResetRules.Decide(LastVisit(generator), e.Now, clock, delay, now, playerInside, tomb, built, clearance, inBase);
                if (retry && outcome != FullResetOutcome.Reset) deferred++;
                if (requested && outcome != FullResetOutcome.Reset)
                    e.Record(DelveKind.FullReset, "site request " + generator.m_uid + " not reset: " + outcome + "; no retry queued", false);
                switch (outcome)
                {
                    case FullResetOutcome.FirstSight:
                        SetVisit(generator, e.Now);
                        continue;
                    case FullResetOutcome.Waiting:
                        waiting++;
                        continue;
                    case FullResetOutcome.PlayerInside: Count(blocked, GuardResult.PlayerNear); continue;
                    case FullResetOutcome.Built: Count(blocked, GuardResult.Built); continue;
                    case FullResetOutcome.PlayerBase: Count(blocked, GuardResult.PlayerBase); continue;
                    case FullResetOutcome.Tombstone:
                        tombstones++;
                        continue;
                }
                if (e.Budget <= 0)
                {
                    deferred++;
                    if (scheduled && globalNow && !dryRun)
                        generator.Set(forcedRetry, true);
                    if (requested) e.Record(DelveKind.FullReset, "site request " + generator.m_uid + " not reset: action budget; no retry queued", false);
                    continue;
                }
                e.Budget--;
                acted++;
                var wipe = objects.Where(z => FullResetRules.Wipe(z.m_uid == generator.m_uid, z.GetPrefab() == player, z.GetPrefab() == tombstone) == WipeAction.Remove).ToList();
                Vector3 at = generator.GetPosition();
                if (dryRun)
                {
                    if (retry) deferred++;
                    else SetVisit(generator, e.Now);
                    e.Record(DelveKind.FullReset, "wiped " + wipe.Count + " objects in " + site.Name + " at (" + at.x.ToString("0") + ", " + at.z.ToString("0") + ") and generated it again"
                        + (now ? " (regenerate now)" : ""), true);
                    continue;
                }
                var fresh = Regenerate(generator, out var created);
                var commit = FullResetRules.ResetCommit.For(fresh != null);
                if (commit.RemoveCreated)
                {
                    RemoveIds(created);
                    e.Record(DelveKind.FullReset, "left " + site.Name + " at (" + at.x.ToString("0") + ", " + at.z.ToString("0")
                        + ") in place; generation failed (see the log)" + (now ? " (regenerate now)" : ""), false);
                    if (scheduled)
                    {
                        deferred++;
                        if (globalNow)
                            generator.Set(forcedRetry, true);
                        e.NoteClaimFailure(DelveKind.FullReset);
                    }
                    continue;
                }
                if (commit.RemoveOld)
                {
                    foreach (var z in wipe)
                    {
                        if (z == null || created.Contains(z.m_uid) || z.m_uid == fresh!.m_uid) continue;
                        RemoveOne(z);
                    }
                    var previous = ZDOMan.instance.GetZDO(generator.m_uid);
                    if (previous != null && previous.m_uid != fresh!.m_uid) RemoveOne(previous);
                }
                fresh!.Set(forcedRetry, false);
                pendingVisits[fresh.m_uid] = e.Now;
                string citadel = Citadel(e, site.Name, fresh, box, new HashSet<ZDOID>(wipe.Select(z => z.m_uid)));
                e.Record(DelveKind.FullReset, "wiped " + wipe.Count + " objects in " + site.Name + " at (" + at.x.ToString("0") + ", " + at.z.ToString("0") + ") and "
                    + "generated it again" + citadel + (now ? " (regenerate now)" : ""), false);
            }
            if (scheduled) e.Swept(DelveKind.FullReset, deferred);
            e.Report(DelveKind.FullReset, new KindReport(tracked, waiting, deferred, acted, blocked, true, dryRun,
                tombstones > 0 ? tombstones + " postponed by a tombstone" : null));
        }

        /// <summary>
        /// A citadel generated again: the Queen is placed in her room when the new rooms did not bring her, and the sealed
        /// door at the surface locks again (Close Sealed Door) unless a player holds it. Nobody is inside: the reset waited
        /// for every guard. A Queen wiped by this reset still stands in the object list until the game sends its removal, so
        /// she does not count. Returns the words the action record adds.
        /// </summary>
        private static string Citadel(DelveEngine e, string name, ZDO generator, Box box, HashSet<ZDOID> wiped)
        {
            if (name != FullResetRules.Citadel) return "";
            var words = new List<string>();
            float reach = Math.Max(box.Max.X - box.Min.X, box.Max.Z - box.Min.Z) / 2f;
            bool queenInside = DelveWorld.ObjectsNear(generator.GetPosition(), reach).Any(z => z.GetPrefab() == queen && !wiped.Contains(z.m_uid) && box.Contains(V(z.GetPosition())));
            bool roomFound = DelveDungeons.CitadelBossSpot(generator, out Vector3 spot, out Vector3 euler);
            if (FullResetRules.RestoreQueen(name, queenInside, roomFound))
                words.Add(DelveWorld.Create(queen, spot, euler) != null ? "the Queen returned" : "the Queen could not be placed");
            else if (!queenInside) words.Add("the Queen's room was not found");
            var doors = DelveWorld.ObjectsNear(generator.GetPosition(), DungeonSites.DoorSearch + 8f).Where(z => z.GetPrefab() == queenDoor).ToList();
            int? nearest = DungeonSites.NearestDoor(V(generator.GetPosition()), doors.Select(d => V(d.GetPosition())).ToList());
            if (FullResetRules.RelockDoor(name, e.Settings.Get(DelveSettings.Bosses.CloseSealedDoor), nearest.HasValue))
            {
                ZDO door = doors[nearest!.Value];
                if (DelveWorld.HeldByPlayer(door)) words.Add("its sealed door is held by a player and stays as it is");
                else
                {
                    door.Set(ZDOVars.s_state, 0); // 0 is closed
                    words.Add("its sealed door locked again");
                }
            }
            return words.Count > 0 ? ", " + string.Join(", ", words) : "";
        }

        private static void Count(Dictionary<GuardResult, int> map, GuardResult g) => map[g] = map.TryGetValue(g, out var n) ? n + 1 : 1;

        /// <summary>
        /// Builds a fresh generator in ghost mode, as ZoneSystem does for a zone the authority has not loaded. The old
        /// generator and interior are not removed here. <paramref name="created"/> is every ZDO this attempt added.
        /// The result is null unless that new generator ZDO is one of them. Room counts apply through the GenerateRooms hook.
        /// </summary>
        private ZDO? Regenerate(ZDO generator, out HashSet<ZDOID> created)
        {
            created = new HashSet<ZDOID>();
            GameObject prefab = ZNetScene.instance.GetPrefab(generator.GetPrefab());
            if (prefab == null || prefab.GetComponent<DungeonGenerator>() == null || ZDOMan.instance == null) return null;
            Vector3 position = generator.GetPosition();
            Quaternion rotation = generator.GetRotation();
            var before = new HashSet<ZDOID>(ZDOMan.instance.m_objectsByID.Keys);
            GameObject? instance = null;
            ZDO? fresh = null;
            ZNetView.StartGhostInit();
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
                instance.GetComponent<DungeonGenerator>().Generate(ZoneSystem.SpawnMode.Ghost);
                fresh = instance.GetComponent<ZNetView>()?.GetZDO();
            }
            catch (Exception error)
            {
                engine?.Log.Error("Delve full reset generation failed: " + error.GetType().Name + ": " + error.Message);
                fresh = null;
            }
            finally
            {
                ZNetView.FinishGhostInit();
                if (instance != null) UnityEngine.Object.Destroy(instance);
            }
            if (ZDOMan.instance == null) return null;
            foreach (var id in ZDOMan.instance.m_objectsByID.Keys)
                if (!before.Contains(id)) created.Add(id);
            if (fresh == null || !created.Contains(fresh.m_uid) || ZDOMan.instance.GetZDO(fresh.m_uid) == null) return null;
            return fresh;
        }

        private void RemoveIds(IEnumerable<ZDOID> ids)
        {
            foreach (var id in ids) RemoveOne(ZDOMan.instance?.GetZDO(id));
        }

        private void RemoveOne(ZDO? zdo)
        {
            if (zdo == null) return;
            try { DelveWorld.Remove(zdo); }
            catch (Exception error) { engine?.Log.Error("Delve full reset remove failed: " + error.GetType().Name + ": " + error.Message); }
        }

        private void Flush()
        {
            foreach (var id in pendingVisits.Keys.ToList())
            {
                ZDO zdo = ZDOMan.instance?.GetZDO(id);
                if (zdo == null)
                {
                    pendingVisits.Remove(id);
                    continue;
                }
                if (DelveWorld.HeldByPlayer(zdo)) continue;
                var s = pendingVisits[id];
                zdo.Set(visitDay, DelveKeys.EncodeDay(s));
                zdo.Set(visitUtc, DelveKeys.EncodeUtc(s));
                pendingVisits.Remove(id);
            }
        }
    }
}
