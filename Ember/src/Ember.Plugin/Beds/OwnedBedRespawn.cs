// OpenKeep. Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Adapted from OpenKeep at 684671f98de54db6ea6652092ec041f44016c37d, Homestead BedStore/BedList/BedRespawn and bed lifecycle patches.
// Changed by expDARE, 2026: Ember authority gates, scoped character data, confirmed native claims and death-only fallback.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Ember.Beds;
using ExtraPotions.Ember.Config;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Ember.Runtime
{
    internal static class OwnedBedRespawn
    {
        internal const string KeyPrefix = "meadworks.ember.beds.";
        struct Change { internal string Scope; internal BedPoint Point; internal bool Add; }
        static readonly List<Change> pending = new List<Change>();
        static readonly List<BedPoint> ordered = new List<BedPoint>();
        static readonly List<BedPoint> fallback = new List<BedPoint>();
        static readonly Dictionary<Bed, float> claims = new Dictionary<Bed, float>();
        static string? currentScope, fallbackScope;
        static bool seeded;
        static long generation = -1;

        internal static bool Active => Plugin.AllowsUi && EmberModules.ProductActive(out _)
            && Plugin.Settings?.Get(EmberSettings.Sleep.Enabled) == true
            && Plugin.Settings.Get(EmberSettings.Sleep.NearestOwnedBedRespawn) && SourceAllows();

        static bool SourceAllows()
        {
            if (!Chainloader.PluginInfos.TryGetValue("milkyteam.openkeep", out var source)) return true;
            var plugin = source.Instance as BaseUnityPlugin;
            return plugin != null && plugin.Config.TryGetEntry<bool>(
                new ConfigDefinition("8. Homestead", "Nearest Bed Respawn"), out var enabled) && !enabled.Value;
        }

        internal static string? Scope()
        {
            if (!ZNet.instance || ZNet.instance.GetWorld() == null || ZNet.instance.IsDedicated() || !Game.instance) return null;
            var world = ZNet.instance.GetWorldUID(); var profile = Game.instance.GetPlayerProfile();
            if (world == 0 || profile == null || profile.GetPlayerID() == 0
                || !CruciblePlugin.Session.AuthorityReady || CruciblePlugin.Session.WorldId != world) return null;
            return profile.GetPlayerID().ToString(CultureInfo.InvariantCulture) + "@" + world.ToString(CultureInfo.InvariantCulture);
        }
        static string Key => KeyPrefix + ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture);
        static bool Writable => Player.m_localPlayer && Game.instance && Game.instance.GetPlayerProfile() != null
            && Player.m_localPlayer.GetPlayerID() == Game.instance.GetPlayerProfile().GetPlayerID();
        static BedPoint Point(Vector3 v) => new BedPoint(v.x, v.y, v.z);
        static Vector3 Position(BedPoint p) => new Vector3(p.X, p.Y, p.Z);

        static bool BindScope()
        {
            var scope = Scope();
            if (!Active || scope == null) { Reset(); return false; }
            if (currentScope != scope || generation != CruciblePlugin.Session.Generation) { Reset(); currentScope = scope; generation = CruciblePlugin.Session.Generation; }
            return true;
        }
        internal static List<BedPoint> Known()
        {
            if (!Writable || currentScope == null || Scope() != currentScope) return new List<BedPoint>();
            return Player.m_localPlayer.m_customData.TryGetValue(Key, out var text) ? BedPoints.Decode(text) : new List<BedPoint>();
        }
        static void Record(BedPoint point, bool add)
        {
            if (!BindScope() || !BedPoints.Finite(point)) return;
            if (!Writable)
            {
                pending.RemoveAll(c => c.Scope == currentScope && BedPoints.Same(c.Point, point));
                pending.Add(new Change { Scope = currentScope!, Point = point, Add = add }); return;
            }
            var points = Known();
            if (add == BedPoints.Contains(points, point)) return;
            if (add) points.Add(point); else points.RemoveAll(p => BedPoints.Same(p, point));
            Player.m_localPlayer.m_customData[Key] = BedPoints.Encode(points);
        }
        static bool Live(Bed bed) => bed && bed.m_spawnPoint && bed.m_nview && bed.m_nview.IsValid() && bed.m_nview.GetZDO() != null;
        internal static void Observe(Bed bed)
        {
            if (!BindScope() || !Live(bed)) return;
            var owner = bed.m_nview.GetZDO().GetLong(ZDOVars.s_owner, 0);
            Record(Point(bed.GetSpawnPoint()), owner != 0 && owner == Game.instance.GetPlayerProfile().GetPlayerID());
        }
        internal static void ClaimMayHaveChanged(Bed bed)
        {
            if (!BindScope() || !Live(bed)) return;
            claims[bed] = Time.unscaledTime + 5f;
        }
        internal static void Tick()
        {
            if (!BindScope()) return;
            if (Writable && !seeded)
            {
                seeded = true;
                foreach (var bed in UnityEngine.Object.FindObjectsByType<Bed>(FindObjectsSortMode.None)) Observe(bed);
            }
            foreach (var pair in claims.ToArray())
            {
                var bed = pair.Key;
                if (!Live(bed)) { claims.Remove(bed); continue; }
                var mine = bed.m_nview.GetZDO().GetLong(ZDOVars.s_owner, 0) == Game.instance.GetPlayerProfile().GetPlayerID();
                if (mine || Time.unscaledTime >= pair.Value) { Observe(bed); claims.Remove(bed); }
            }
        }
        internal static void Died(Player player)
        {
            if (!BindScope() || !Writable || player != Player.m_localPlayer || !player.m_nview
                || !player.m_nview.IsValid() || !player.m_nview.IsOwner() || !player.IsDead()) return;
            ordered.Clear(); fallback.Clear(); fallbackScope = null;
            var points = Known(); var profile = Game.instance.GetPlayerProfile();
            if (profile.HaveCustomSpawnPoint())
            {
                var legacy = Point(profile.GetCustomSpawnPoint());
                if (BedPoints.Finite(legacy) && !BedPoints.Contains(points, legacy)) points.Add(legacy);
            }
            BedPoints.SortByDistance(points, Point(player.transform.position));
            if (points.Count == 0) return;
            fallbackScope = currentScope; ordered.AddRange(points);
            profile.SetCustomSpawnPoint(Position(points[0]));
            fallback.AddRange(points.Skip(1));
        }
        internal static IReadOnlyList<BedPoint> Choices() => fallbackScope != null && fallbackScope == Scope()
            ? ordered.ToArray() : Array.Empty<BedPoint>();
        internal static bool Prefer(BedPoint point)
        {
            if (!QuickRespawn.ChoiceCurrent || !BindScope() || !Writable || fallbackScope != currentScope) return false;
            var selected = ordered.FindIndex(p => BedPoints.Same(p, point));
            if (selected < 0) return false;
            var actual = ordered[selected];
            Game.instance.GetPlayerProfile().SetCustomSpawnPoint(Position(actual));
            fallback.Clear(); fallback.AddRange(ordered.Where(p => !BedPoints.Same(p, actual)));
            return true;
        }
        internal static void FailedSpawn(BedPoint gone)
        {
            if (!BindScope() || fallbackScope != currentScope || !Game.instance.m_respawnAfterDeath) return;
            Record(gone, false);
            ordered.RemoveAll(p => BedPoints.Same(p, gone));
            fallback.RemoveAll(p => BedPoints.Same(p, gone));
            if (fallback.Count == 0) return;
            var next = fallback[0]; fallback.RemoveAt(0);
            Game.instance.GetPlayerProfile().SetCustomSpawnPoint(Position(next));
        }
        internal static void Gone(Vector3 point) => Record(Point(point), false);
        internal static void Spawned(Player player)
        {
            if (player != Player.m_localPlayer || !BindScope()) return;
            ordered.Clear(); fallback.Clear(); fallbackScope = null;
            var changes = pending.ToArray(); pending.Clear();
            foreach (var change in changes) if (change.Scope == currentScope) Record(change.Point, change.Add);
        }
        internal static void Reset()
        {
            pending.Clear(); ordered.Clear(); fallback.Clear(); claims.Clear(); currentScope = null; fallbackScope = null; seeded = false; generation = -1;
        }
        internal static object Status() => new {
            active = Active, scope = Scope(), key = Scope() != null ? Key : null,
            known = Known().Select(Position).ToArray(), fallback = fallback.Select(Position).ToArray(),
            pendingChanges = pending.Count, pendingClaims = claims.Count
        };
    }
}
