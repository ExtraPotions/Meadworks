// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (user parity rows 26 and 27, 2026-10-10): the admin surface reset. Valheim_Admin_Tool's "reset
// resources" and "regenerate nature" are behavior references only (no code used). The game's own vegetation generator
// (ZoneSystem.PlaceVegetation in ghost mode, as the game itself runs it for zones near a player on a dedicated server)
// replays what each generated zone held; Delve.Core's SurfaceRules decide which missing spots come back.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Surface;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Delve.Runtime
{
    /// <summary>
    /// One surface reset at a time, run on the authority over several frames: a zone's terrain may need building first
    /// (on a dedicated server the zones around players are never loaded), then one zone is replayed per frame.
    /// </summary>
    public static class DelveSurface
    {
        private const float TerrainWait = 20f;

        private sealed class Job
        {
            public Vector3 Center;
            public SurfaceScope Scope;
            public long CallerUid;
            public string Caller = "";
            public Action<string>? Print;
            public readonly Queue<Vector2s> Zones = new Queue<Vector2s>();
            public SurfaceReport Report = null!;
            public float Deadline;
        }

        private sealed class PrefabInfo
        {
            public SurfaceClass Class;
            public int[] Same = Array.Empty<int>();
            public int Stub;
        }

        private static Job? job;
        private static readonly Dictionary<int, PrefabInfo> prefabs = new Dictionary<int, PrefabInfo>();
        private static readonly List<ZDO> gathered = new List<ZDO>();

        public static bool Running => job != null;

        /// <summary>The report of the last finished surface reset, and how many have finished since the world started.</summary>
        public static SurfaceReport? Last { get; private set; }
        public static int Finished { get; private set; }

        /// <summary>
        /// Starts a surface reset around <paramref name="center"/> for an admin. The lines answer at once; the result follows
        /// as a notice to the caller (and through <paramref name="print"/>, the host's console) when every zone is done.
        /// </summary>
        public static IReadOnlyList<string> Begin(Vector3 center, SurfaceScope scope, long callerUid, string caller, Action<string>? print = null)
        {
            var settings = Plugin.Settings;
            var zones = ZoneSystem.instance;
            if (!Plugin.Ready || settings == null || zones == null || ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || ZNetScene.instance == null)
                return new[] { "Delve: the surface reset needs a running world on the server." };
            if (!settings.Get(DelveSettings.Surface.AllowSurfaceReset))
                return new[] { "Delve: the surface reset is switched off (Surface / Allow Surface Reset)." };
            if (job != null)
                return new[] { "Delve: a surface reset is already running for " + job.Caller + "; try again when it has finished." };
            bool dryRun = settings.Get(DelveSettings.General.DryRunAll);
            var next = new Job
            {
                Center = center, Scope = scope, CallerUid = callerUid, Caller = caller, Print = print,
                Report = new SurfaceReport(scope, dryRun), Deadline = Time.realtimeSinceStartup + TerrainWait,
            };
            foreach (var (x, z) in SurfaceRules.ZonesWithin(DelveWorld.ToVec(center), settings.Get(DelveSettings.Surface.Radius)))
            {
                var id = new Vector2s(x, z);
                if (zones.IsZoneGenerated(id)) next.Zones.Enqueue(id);
                else next.Report.ZonesSkipped++;
            }
            if (next.Zones.Count == 0)
                return new[] { "Delve: no generated zones within " + Whole(settings.Get(DelveSettings.Surface.Radius)) + " m of you; nothing to reset." };
            job = next;
            Plugin.Log?.Info("Delve surface reset (" + scope + ") started by " + caller + " at " + Whole(center.x) + " " + Whole(center.z)
                + " over " + next.Zones.Count + " zones" + (dryRun ? ", dry run" : "") + ".");
            return new[] { "Delve: surface reset (" + What(scope) + ") started over " + next.Zones.Count + " zones around you" + (dryRun ? ", dry run" : "") + "; the result follows when it is done." };
        }

        /// <summary>Called every frame from the plugin: one zone per frame once its terrain is ready.</summary>
        public static void Tick()
        {
            var current = job;
            if (current == null) return;
            try
            {
                var zones = ZoneSystem.instance;
                if (zones == null || ZNet.instance == null || ZDOMan.instance == null || ZNetScene.instance == null) { Finish(current, "the world closed"); return; }
                if (current.Zones.Count == 0) { Finish(current, null); return; }
                var zone = current.Zones.Peek();
                var zonePos = ZoneSystem.GetZonePos(zone);
                GameObject? loaded = zones.m_zones.TryGetValue(zone, out var data) && data.m_root != null ? data.m_root : null;
                if (loaded == null)
                {
                    var template = zones.m_zonePrefab.GetComponentInChildren<Heightmap>();
                    if (!HeightmapBuilder.instance.IsTerrainReady(zonePos, template.m_width, template.m_scale, template.IsDistantLod, WorldGenerator.instance))
                    {
                        if (Time.realtimeSinceStartup < current.Deadline) return;
                        current.Zones.Dequeue();
                        current.Report.ZonesSkipped++;
                        return;
                    }
                }
                current.Zones.Dequeue();
                Zone(current, zones, zone, zonePos, loaded);
                current.Report.Zones++;
            }
            catch (Exception error)
            {
                Plugin.Log?.Error("Delve surface reset failed: " + error.GetType().Name + ": " + error.Message);
                Finish(current, "it failed, see the server log");
            }
        }

        public static void Reset()
        {
            job = null;
            Last = null;
            Finished = 0;
            prefabs.Clear();
            gathered.Clear();
        }

        private static void Finish(Job done, string? stopped)
        {
            job = null;
            Last = done.Report;
            Finished++;
            var lines = done.Report.Lines().ToList();
            if (stopped != null) lines.Insert(0, "Delve: the surface reset stopped early: " + stopped + ".");
            foreach (var line in lines)
            {
                Plugin.Log?.Info(line);
                if (done.CallerUid != 0L) DelveNotices.Tell(done.CallerUid, line, ServerNoticePlacement.TopLeft);
                done.Print?.Invoke(line);
            }
            if (done.Report.ByPrefab.Count > 0)
                Plugin.Log?.Info("Delve surface reset by prefab: " + string.Join(", ", done.Report.ByPrefab.Select(p => p.Key + " " + p.Value)));
        }

        /// <summary>One zone: what stands now is read first, then the generator replays the zone and each spot is decided.</summary>
        private static void Zone(Job current, ZoneSystem zones, Vector2s zone, Vector3 zonePos, GameObject? loaded)
        {
            var settings = Plugin.Settings!;
            float clearance = settings.Get(DelveSettings.Surface.BaseClearance);
            var report = current.Report;

            // What stands now, before the replay adds its own ghost objects to the same sectors.
            var standing = Gather(zonePos, clearance + ZoneSystem.c_ZoneSize * 0.5f);
            var built = standing.Where(z => z.GetLong(ZDOVars.s_creator) != 0L).Select(z => DelveWorld.ToVec(z.GetPosition())).ToList();
            var bases = DelveBases.Near(zonePos);
            var locations = Locations(zones, zone);
            var players = DelveWorld.Players().Select(p => DelveWorld.ToVec(p.Position)).ToList();
            var byPrefab = new Dictionary<int, List<ZDO>>();
            foreach (var zdo in standing)
            {
                if (!byPrefab.TryGetValue(zdo.GetPrefab(), out var list)) byPrefab[zdo.GetPrefab()] = list = new List<ZDO>();
                list.Add(zdo);
            }

            var ghosts = Replay(zones, zone, zonePos, loaded, standing);

            // Group the generated spots by prefab; the layout of a prefab is used only while it still matches the zone.
            var keep = new HashSet<ZDO>();
            foreach (var group in ghosts.GroupBy(g => g.GetPrefab()))
            {
                var info = Info(group.Key);
                if (!SurfaceRules.InScope(current.Scope, info.Class)) continue;
                var same = info.Same.SelectMany(h => byPrefab.TryGetValue(h, out var l) ? l : Enumerable.Empty<ZDO>()).ToList();
                var spots = group.Select(g => DelveWorld.ToVec(g.GetPosition())).ToList();
                var counted = same.Where(z => ZoneSystem.GetZone(z.GetPosition()) == zone && z.GetLong(ZDOVars.s_creator) == 0L
                    && !locations.Any(a => a.Contains(DelveWorld.ToVec(z.GetPosition())))).ToList();
                int matched = counted.Count(z => spots.Any(s => SurfaceRules.Matches(s, DelveWorld.ToVec(z.GetPosition()))));
                if (!SurfaceRules.LayoutMatches(counted.Count, matched))
                {
                    report.Diverge(Name(group.Key));
                    Plugin.Log?.Info("Delve surface reset: " + Name(group.Key) + " in zone " + zone.x + "," + zone.y + " left as it is (" + matched + " of " + counted.Count + " standing found in the generated layout).");
                    continue;
                }
                foreach (var ghost in group)
                {
                    var spot = DelveWorld.ToVec(ghost.GetPosition());
                    bool present = same.Any(z => SurfaceRules.Matches(spot, DelveWorld.ToVec(z.GetPosition())));
                    var decision = SurfaceRules.Decide(new SurfaceFacts(spot, present, clearance) { Built = built, Bases = bases, Locations = locations, Players = players });
                    if (decision == SurfaceSpot.Present) continue;
                    if (decision != SurfaceSpot.Restore)
                    {
                        report.Held[decision] = (report.Held.TryGetValue(decision, out var n) ? n : 0) + 1;
                        continue;
                    }
                    report.Restored++;
                    var name = Name(group.Key);
                    report.ByPrefab[name] = (report.ByPrefab.TryGetValue(name, out var c) ? c : 0) + 1;
                    if (info.Stub != 0 && byPrefab.TryGetValue(info.Stub, out var stubs))
                        foreach (var stub in stubs.Where(s => s.IsValid() && Vector3Flat(s.GetPosition(), ghost.GetPosition()) < SurfaceRules.StubRadius).ToList())
                        {
                            report.Stubs++;
                            if (!report.DryRun) DelveWorld.Remove(stub);
                        }
                    if (!report.DryRun) keep.Add(ghost);
                }
            }
            // Every replayed object not coming back is removed again; kept ones are ordinary generated objects now.
            foreach (var ghost in ghosts)
                if (!keep.Contains(ghost)) DelveWorld.Remove(ghost);
        }

        /// <summary>
        /// Runs the game's vegetation pass for the zone in ghost mode, as the game does when it generates a zone near a
        /// player on a dedicated server: objects get their data but are never loaded. Colliders of what stands now are off
        /// for the pass, so the generator's blocked-spot test sees the zone as it was when it was first generated.
        /// </summary>
        private static List<ZDO> Replay(ZoneSystem zones, Vector2s zone, Vector3 zonePos, GameObject? loaded, List<ZDO> standing)
        {
            var spawned = new List<GameObject>();
            var parent = new GameObject("DelveSurfaceReplay");
            GameObject? root = null;
            var disabled = new List<Collider>();
            var ghosts = new List<ZDO>();
            try
            {
                Heightmap hmap;
                if (loaded != null)
                {
                    hmap = loaded.GetComponentInChildren<Heightmap>();
                    foreach (var collider in loaded.GetComponentsInChildren<Collider>())
                        if (collider.enabled && collider.GetComponent<Heightmap>() == null) { collider.enabled = false; disabled.Add(collider); }
                }
                else
                {
                    root = UnityEngine.Object.Instantiate(zones.m_zonePrefab, zonePos, Quaternion.identity);
                    hmap = root.GetComponentInChildren<Heightmap>();
                }
                float reach = ZoneSystem.c_ZoneSize * 0.5f + 8f;
                foreach (var view in ZNetScene.instance.m_instances.Values)
                {
                    if (view == null) continue;
                    var p = view.transform.position;
                    if (Mathf.Abs(p.x - zonePos.x) > reach || Mathf.Abs(p.z - zonePos.z) > reach || Mathf.Abs(p.y - zonePos.y) > SurfaceRules.InteriorGap) continue;
                    foreach (var collider in view.GetComponentsInChildren<Collider>())
                        if (collider.enabled) { collider.enabled = false; disabled.Add(collider); }
                }
                var clear = new List<ZoneSystem.ClearArea>();
                if (zones.m_locationInstances.TryGetValue(zone, out var here) && here.m_placed && here.m_location != null && here.m_location.m_clearArea)
                    clear.Add(new ZoneSystem.ClearArea(here.m_position, here.m_location.m_exteriorRadius));
                try
                {
                    zones.PlaceVegetation(zone, zonePos, parent.transform, hmap, clear, ZoneSystem.SpawnMode.Ghost, spawned);
                }
                finally
                {
                    ZNetView.FinishGhostInit();
                }
                var before = new HashSet<ZDO>(standing);
                foreach (var go in spawned)
                {
                    var zdo = go != null ? go.GetComponent<ZNetView>()?.GetZDO() : null;
                    if (zdo != null && !before.Contains(zdo)) ghosts.Add(zdo);
                }
            }
            finally
            {
                foreach (var collider in disabled) if (collider != null) collider.enabled = true;
                foreach (var go in spawned) if (go != null) UnityEngine.Object.Destroy(go);
                UnityEngine.Object.Destroy(parent);
                if (root != null) UnityEngine.Object.Destroy(root);
            }
            return ghosts;
        }

        /// <summary>The objects in the zones around the spot, copied (the game reuses its search list).</summary>
        private static List<ZDO> Gather(Vector3 spot, float radius)
        {
            gathered.Clear();
            int reach = Mathf.Max(1, Mathf.CeilToInt(radius / ZoneSystem.c_ZoneSize));
            ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(spot), new SimulationDistance(reach, 0, true), gathered);
            var copy = new List<ZDO>(gathered);
            gathered.Clear();
            return copy;
        }

        /// <summary>Exterior circles of the placed world locations in and around the zone.</summary>
        private static List<Area> Locations(ZoneSystem zones, Vector2s zone)
        {
            var found = new List<Area>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (zones.m_locationInstances.TryGetValue(new Vector2s(zone.x + dx, zone.y + dz), out var instance) && instance.m_placed && instance.m_location != null)
                        found.Add(new Area(DelveWorld.ToVec(instance.m_position), Mathf.Max(instance.m_location.m_exteriorRadius, instance.m_location.m_interiorRadius)));
            return found;
        }

        /// <summary>What a generated prefab is, from its parts; cached per prefab.</summary>
        private static PrefabInfo Info(int hash)
        {
            if (prefabs.TryGetValue(hash, out var known)) return known;
            var info = new PrefabInfo { Same = new[] { hash } };
            var prefab = ZNetScene.instance.GetPrefab(hash);
            if (prefab != null && prefab.GetComponent<ZNetView>() != null)
            {
                var destructible = prefab.GetComponent<Destructible>();
                var broken = destructible != null ? destructible.m_spawnWhenDestroyed : null;
                bool mineable = prefab.GetComponent<MineRock>() != null || prefab.GetComponent<MineRock5>() != null
                    || (broken != null && (broken.GetComponent<MineRock5>() != null || broken.GetComponent<MineRock>() != null));
                bool excluded = prefab.GetComponentInChildren<Character>(true) != null || prefab.GetComponentInChildren<Container>(true) != null
                    || prefab.GetComponentInChildren<SpawnArea>(true) != null || prefab.GetComponentInChildren<CreatureSpawner>(true) != null
                    || prefab.GetComponentInChildren<Ship>(true) != null || prefab.GetComponentInChildren<Leviathan>(true) != null
                    || prefab.GetComponentInChildren<OfferingBowl>(true) != null;
                var tree = prefab.GetComponent<TreeBase>();
                info.Class = SurfaceRules.Classify(tree != null, mineable, prefab.GetComponent<Pickable>() != null, destructible != null, excluded);
                if (broken != null && broken.GetComponent<ZNetView>() != null)
                    info.Same = new[] { hash, broken.name.GetStableHashCode() };
                if (tree != null && tree.m_stubPrefab != null) info.Stub = tree.m_stubPrefab.name.GetStableHashCode();
            }
            prefabs[hash] = info;
            return info;
        }

        private static string Name(int hash)
        {
            var prefab = ZNetScene.instance?.GetPrefab(hash);
            return prefab != null ? prefab.name : hash.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static float Vector3Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        private static string Whole(float value) => Mathf.RoundToInt(value).ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static string What(SurfaceScope scope) => scope == SurfaceScope.Resources ? "resources" : scope == SurfaceScope.Nature ? "nature" : "resources and nature";
    }
}
