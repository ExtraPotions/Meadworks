// Threshold - ExtraPotions Meadworks portals.
// Portions from ValheimModLibs AreaLoading (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: ValheimModLibs/AreaLoading/AreaLoader.cs (Busy),
// ValheimModLibs/AreaLoading/AreaSettle.cs (Settled, Look), ValheimModLibs/AreaLoading/LandLoad.cs (Bind, Tick),
// ValheimModLibs/AreaLoading/ObjectLoad.cs (Bind, Hurry, Collect) and ValheimModLibs/AreaLoading/LoadPatches.cs (the two
// postfixes), used by Wayfare 0.3.2 Quick Area Loading.
// Changed by expDARE, 2026: one class in Threshold's namespace; the only reason to hurry is a Threshold jump loading
// behind the teleport screen (ThresholdJumps.LoadingBehindScreen); no report log; patches installed by Threshold's own
// Harmony instance and reset on teardown.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.Travel
{
    /// <summary>
    /// Quick Area Loading and the client's landing check. While a Threshold jump loads behind the teleport screen, the land
    /// around the target loads as fast as the machine allows (the game's private <c>ZoneSystem.CreateLocalZones</c> again
    /// within <see cref="LandBudgetMs"/> of each frame) and the listed objects follow zone by zone (the game's private
    /// <c>ZNetScene.CreateObject</c> in its own <c>ZDOCompare</c> order, only where the zone's land is loaded). Never on a
    /// dedicated server or before the connection is up. <see cref="Settled"/>: on a client, the objects around a point
    /// count as arrived once the objects known in its 3x3 zones have not changed for <see cref="QuietSeconds"/>.
    /// </summary>
    public static class ThresholdAreaLoading
    {
        const double LandBudgetMs = 20.0;
        const double ObjectBudgetMs = 15.0;
        const float QuietSeconds = 0.5f;
        const float CheckSeconds = 0.1f;

        static bool patched;
        static Func<ZoneSystem, Vector3, bool>? createLocalZones;
        static FieldInfo? listed;
        static Func<ZNetScene, ZDO, GameObject>? create;
        static Comparison<ZDO>? order;
        static readonly Stopwatch clock = new Stopwatch();
        static readonly List<ZDO> pending = new List<ZDO>();

        static readonly List<ZDO> found = new List<ZDO>();
        static readonly SimulationDistance around = new SimulationDistance(1, 0);
        static Vector2s zone;
        static int count = -1;
        static long ids;
        static long revisions;
        static float quietSince;
        static float nextCheck;
        static bool settled;

        public static void Patch(Harmony harmony)
        {
            if (patched) return;
            patched = true;
            createLocalZones = AccessTools.MethodDelegate<Func<ZoneSystem, Vector3, bool>>(AccessTools.Method(typeof(ZoneSystem), "CreateLocalZones"));
            listed = AccessTools.Field(typeof(ZNetScene), "m_tempCurrentObjects");
            create = AccessTools.MethodDelegate<Func<ZNetScene, ZDO, GameObject>>(AccessTools.Method(typeof(ZNetScene), "CreateObject"));
            order = AccessTools.MethodDelegate<Comparison<ZDO>>(AccessTools.Method(typeof(ZNetScene), "ZDOCompare"));
            harmony.Patch(AccessTools.Method(typeof(ZoneSystem), "Update"), postfix: new HarmonyMethod(typeof(ThresholdAreaLoading), nameof(AfterZones)));
            harmony.Patch(AccessTools.Method(typeof(ZNetScene), "CreateDestroyObjects"), postfix: new HarmonyMethod(typeof(ThresholdAreaLoading), nameof(AfterObjects)));
        }

        static bool Busy
        {
            get
            {
                if (ZNet.instance == null || ZNet.instance.IsDedicated() || ZNet.GetConnectionStatus() != ZNet.ConnectionStatus.Connected) return false;
                try { return ThresholdJumps.LoadingBehindScreen(); }
                catch (Exception) { return false; }
            }
        }

        static void AfterZones(ZoneSystem __instance)
        {
            if (createLocalZones == null || !Busy || (ZNet.instance.IsServer() && !__instance.LocationsGenerated) || __instance.IsActiveAreaLoaded()) return;
            try
            {
                Vector3 at = ZNet.instance.GetReferencePosition();
                clock.Restart();
                while (clock.Elapsed.TotalMilliseconds < LandBudgetMs && createLocalZones(__instance, at)) { }
            }
            catch (Exception e) { Off(e); }
        }

        static void AfterObjects(ZNetScene __instance)
        {
            if (listed == null || create == null || order == null || !Busy || ZoneSystem.instance == null) return;
            try
            {
                if (!(listed.GetValue(__instance) is List<ZDO> near)) return;
                Collect(near, ZNet.instance.GetReferencePosition());
                pending.Sort(order);
                clock.Restart();
                foreach (ZDO zdo in pending)
                {
                    if (clock.Elapsed.TotalMilliseconds >= ObjectBudgetMs) break;
                    create(__instance, zdo);
                }
            }
            catch (Exception e) { Off(e); }
            finally { pending.Clear(); }
        }

        /// <summary>The listed objects not made yet whose zone has its land and is ready for their type, with the game's sort key.</summary>
        static void Collect(List<ZDO> near, Vector3 at)
        {
            pending.Clear();
            ZoneSystem zones = ZoneSystem.instance;
            foreach (ZDO zdo in near)
            {
                if (zdo.Created) continue;
                Vector2s sector = zdo.GetSector();
                if (!zones.IsZoneLoaded(sector) || !zones.IsZoneReadyForType(sector, zdo.Type)) continue;
                zdo.m_tempSortValue = Utils.DistanceSqr(at, zdo.GetPosition());
                pending.Add(zdo);
            }
        }

        /// <summary>Whether the objects around <paramref name="point"/> have all arrived; always true on the server.</summary>
        public static bool Settled(Vector3 point)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer() || ZDOMan.instance == null) return true;
            Vector2s at = ZoneSystem.GetZone(point);
            if (at == zone && Time.time < nextCheck) return settled;
            nextCheck = Time.time + CheckSeconds;
            settled = Look(at);
            return settled;
        }

        /// <summary>Folds the zones' objects (count, XOR of ids, sum of data revisions) and compares them with the last look.</summary>
        static bool Look(Vector2s at)
        {
            found.Clear();
            ZDOMan.instance.FindSectorObjects(at, around, found);
            long idFold = 0L;
            long revisionSum = 0L;
            foreach (ZDO zdo in found)
            {
                idFold ^= zdo.m_uid.UserID * 31L + zdo.m_uid.ID;
                revisionSum += zdo.DataRevision;
            }
            int seen = found.Count;
            found.Clear();
            bool same = at == zone && seen == count && idFold == ids && revisionSum == revisions;
            if (!same)
            {
                zone = at;
                count = seen;
                ids = idFold;
                revisions = revisionSum;
                quietSince = Time.time;
                return false;
            }
            return Time.time - quietSince >= QuietSeconds;
        }

        static void Off(Exception e)
        {
            Plugin.Log?.Error("Threshold: Quick Area Loading is off (the game's loading members changed): " + e.GetType().Name + ": " + e.Message);
            createLocalZones = null;
            create = null;
        }

        public static void Reset()
        {
            patched = false;
            count = -1;
            settled = false;
            nextCheck = 0f;
        }
    }
}
