// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later (Elite Creatures Reborn 3.20.0, ValheimMods at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: EliteCreaturesReborn/EliteCreaturesReborn/Patches/SpawnerRespawnPatch.cs
// SpawnerRespawn.Arm, InDungeon and the CreatureSpawner Awake and Spawn postfixes). The notice is in
// THIRD_PARTY_NOTICES/EliteCreaturesReborn-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: one Delve engine service. Arming runs only on the authority, for spawners it has loaded and
// owns, on the day clock, with Delve's Creature Respawn settings; every other cleared one-shot spawner (the usual case on
// a dedicated server, where clients own spawners) is reset by the server (user decision D2): after the delay since its
// creature was last alive, with nobody within Creature Respawn / Respawn Player Radius (user decision R-Q5), its object is replaced by a fresh one with no spawned state, which every
// client is told about, so the owner's unmodded game spawns it again. The reset is Meadworks-original.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Respawn;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Delve.Runtime
{
    public sealed class DelveSpawners : IDelveService
    {
        private static DelveSpawners? current;

        /// <summary>Prefab hashes of one-shot spawners (the prefab's own respawn time is 0: once cleared, cleared for good).</summary>
        private readonly HashSet<int> oneShot = new HashSet<int>();
        private DelveEngine? engine;

        public IReadOnlyList<DelveKind> Kinds { get; } = new[] { DelveKind.CampSpawners, DelveKind.DungeonSpawners };

        public void Start(DelveEngine e)
        {
            engine = e;
            current = this;
            oneShot.Clear();
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null) continue;
                var spawner = prefab.GetComponent<CreatureSpawner>();
                if (spawner != null && spawner.m_respawnTimeMinuts <= 0f) oneShot.Add(prefab.name.GetStableHashCode());
            }
            e.Log.Verbose("Delve spawners: " + oneShot.Count + " one-shot spawner prefabs.");
        }

        public void Stop(DelveEngine e)
        {
            if (current == this) current = null;
            engine = null;
            oneShot.Clear();
        }

        private static DelveKind KindAt(Vector3 p) => SpawnerRules.InDungeon(DelveWorld.ToVec(p)) ? DelveKind.DungeonSpawners : DelveKind.CampSpawners;

        public void Check(DelveEngine e, ISet<DelveKind> sweeping)
        {
            var due = Kinds.Where(k => e.Active(k) && sweeping.Contains(k)).ToList();
            if (due.Count == 0) return;
            var counts = due.ToDictionary(k => k, k => new int[4]); // tracked, waiting, deferred, acted
            var blocked = due.ToDictionary(k => k, k => new Dictionary<GuardResult, int>());
            foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values.ToList())
            {
                if (!oneShot.Contains(zdo.GetPrefab())) continue;
                Vector3 p = zdo.GetPosition();
                var kind = KindAt(p);
                if (!counts.TryGetValue(kind, out var c)) continue;
                c[0]++;
                if (ArmedLocally(zdo)) continue; // the game's own timer brings it back
                var connection = zdo.GetConnection();
                bool hasSpawned = connection != null && connection.m_type == ZDOExtraData.ConnectionType.Spawned;
                bool alive = hasSpawned && !connection!.m_target.IsNone() && ZDOMan.instance.GetZDO(connection.m_target) != null;
                var (clock, delay) = e.ClockOf(kind);
                bool now = e.RegenerateNow(kind);
                var lastAlive = SpawnerRules.LastAlive(zdo.GetLong(ZDOVars.s_aliveTime), e.DayLengthSeconds);
                if (now && lastAlive.HasDay) lastAlive = Stamp.DayOnly(e.Now.Day - 3650); // regenerate now: the delay is spent
                var guard = e.Guard(p, e.Settings.Get(DelveSettings.CreatureRespawn.PlayerRadius), 0f, DelveWorld.HeldByPlayer(zdo));
                var decision = SpawnerRules.ServerReset(true, hasSpawned, alive, lastAlive, e.Now, clock, delay, guard, out var blockedBy);
                if (decision == SpawnerReset.Waiting) c[1]++;
                if (decision == SpawnerReset.Blocked)
                {
                    var b = blocked[kind]; // a player within the radius, or a client that owns and has it loaded (Held)
                    b[blockedBy] = b.TryGetValue(blockedBy, out var n) ? n + 1 : 1;
                }
                if (decision != SpawnerReset.Reset) continue;
                if (e.Budget <= 0)
                {
                    c[2]++;
                    continue;
                }
                e.Budget--;
                c[3]++;
                bool dryRun = e.DryRun(kind);
                string name = ZNetScene.instance.GetPrefab(zdo.GetPrefab())?.name ?? "a spawner";
                if (!dryRun)
                {
                    int prefab = zdo.GetPrefab();
                    Vector3 rotation = zdo.GetRotation().eulerAngles;
                    DelveWorld.Remove(zdo);
                    DelveWorld.Create(prefab, p, rotation);
                }
                e.Record(kind, "reset " + name + " at (" + p.x.ToString("0") + ", " + p.z.ToString("0") + ")" + (now ? " (regenerate now)" : ""), dryRun);
            }
            foreach (var kind in due)
            {
                var c = counts[kind];
                e.Swept(kind, c[2]);
                e.Report(kind, new KindReport(c[0], c[1], c[2], c[3], blocked[kind], true, e.DryRun(kind)));
            }
        }

        /// <summary>The authority has this spawner loaded and gave it a timer (ArmLocal).</summary>
        private static bool ArmedLocally(ZDO zdo)
        {
            var view = ZNetScene.instance.FindInstance(zdo);
            var spawner = view != null ? view.GetComponent<CreatureSpawner>() : null;
            return spawner != null && view!.IsOwner() && spawner.m_respawnTimeMinuts > 0f;
        }

        /// <summary>
        /// Gives a one-shot spawner the authority has loaded and owns the respawn timer it was built without, so the game's
        /// own spawner brings its creature back. Only one that has spawned before: a fresh one keeps zero, so the first
        /// population is the game's own. A spawner whose timer the game already set is never touched.
        /// </summary>
        internal static void Arm(CreatureSpawner spawner)
        {
            var service = current;
            var e = service?.engine;
            if (e == null || !e.Running || spawner == null || spawner.m_nview == null || !spawner.m_nview.IsValid() || !spawner.m_nview.IsOwner()) return;
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            var kind = KindAt(spawner.transform.position);
            if (!e.Active(kind) || e.DryRun(kind)) return;
            var (clock, delay) = e.ClockOf(kind);
            if (SpawnerRules.PathFor(true, clock) != RespawnApproach.ArmLocal) return;
            var minutes = SpawnerRules.Arm(spawner.m_respawnTimeMinuts, spawner.HasSpawned(), SpawnerRules.ArmMinutes(delay, e.DayLengthSeconds));
            if (minutes.HasValue) spawner.m_respawnTimeMinuts = minutes.Value;
        }

        /// <summary>A spawner loading with a creature already behind it (alive or killed) is armed before its first update.</summary>
        [HarmonyPatch(typeof(CreatureSpawner), "Awake")]
        private static class ArmOnAwake
        {
            private static bool Prepare() => !DelveModules.StepAside.SteppedAside(Integration.DelveModule.Respawn);

            private static void Postfix(CreatureSpawner __instance)
            {
                try { Arm(__instance); }
                catch (Exception error) { Plugin.Log?.Warning("Delve spawner arm: " + error.GetType().Name + ": " + error.Message); }
            }
        }

        /// <summary>A spawner that has just spawned its first creature is armed for the next one.</summary>
        [HarmonyPatch(typeof(CreatureSpawner), "Spawn")]
        private static class ArmOnSpawn
        {
            private static bool Prepare() => !DelveModules.StepAside.SteppedAside(Integration.DelveModule.Respawn);

            private static void Postfix(CreatureSpawner __instance)
            {
                try { Arm(__instance); }
                catch (Exception error) { Plugin.Log?.Warning("Delve spawner arm: " + error.GetType().Name + ": " + error.Message); }
            }
        }
    }
}
