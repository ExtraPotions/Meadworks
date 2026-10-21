// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGateScan.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Threshold.Portals;
using HarmonyLib;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>Server: every sea gate pillar ZDO in the world, kept up to date as pillars come rather than searched for
    /// on request. A pillar reaches the server in one of three ways: the world loads it, this machine creates it (a host
    /// building), or a client sends it. The last two both pass <c>ZDOMan.AddIfPortal</c> with the prefab known
    /// (<c>CreateNewZDO</c>, and <c>RPC_ZDOData</c> after the data is read), where <see cref="PillarIndexPatch"/> adds it.
    /// For the first, one pass over the loaded world runs once, right after the world has loaded, spread over frames
    /// (<c>ZDOMan.GetAllZDOsWithPrefabIterative</c> walks up to 400 occupied sectors a call); after it nothing walks the
    /// world again, and no request ever waits for or starts a walk. The index is kept as ZDOIDs, not ZDOs: the game pools
    /// ZDOs and reuses a destroyed one for another object, while a destroyed object's id simply resolves to null and is
    /// dropped the next time the index is read.</summary>
    internal static class SeaGateScan
    {
        private static readonly HashSet<ZDOID> found = new HashSet<ZDOID>();
        private static readonly List<ZDO> buffer = new List<ZDO>();
        private static readonly List<ZDOID> dead = new List<ZDOID>();
        private static readonly List<ZDO> live = new List<ZDO>();
        private static ZDOMan indexedOn;
        private static bool seeded;
        private static int index;

        /// <summary>The pass over the loaded world is done: the index holds every pillar of this world.</summary>
        internal static bool Seeded => seeded && indexedOn != null && indexedOn == ZDOMan.instance;

        /// <summary>Every frame on the server: one step of the pass over the loaded world, until it is done.</summary>
        internal static void Step()
        {
            ZDOMan man = ZDOMan.instance;
            if (man == null)
                return;
            Follow(man);
            if (seeded)
                return;
            if (!man.GetAllZDOsWithPrefabIterative(SeaGateFields.PillarPrefab, buffer, ref index))
                return;
            foreach (ZDO zdo in buffer)
            {
                if (zdo != null && zdo.IsValid() && SeaGateFields.IsPillar(zdo))
                    found.Add(zdo.m_uid);
            }
            buffer.Clear();
            seeded = true;
        }

        /// <summary>A pillar ZDO this server just created or received.</summary>
        internal static void Add(ZDOMan man, ZDO zdo)
        {
            Follow(man);
            found.Add(zdo.m_uid);
        }

        /// <summary>A new world (another ZDOMan) starts a new index and a new pass.</summary>
        private static void Follow(ZDOMan man)
        {
            if (indexedOn == man)
                return;
            indexedOn = man;
            found.Clear();
            buffer.Clear();
            seeded = false;
            index = 0;
        }

        /// <summary>The last <see cref="Pillars"/> folded into one number: every live pillar's id and data revision and
        /// their count. It changes whenever a pillar comes, goes or any of its data changes (name, partner, mode).</summary>
        internal static long Fold { get; private set; }

        /// <summary>The indexed pillars that still exist, in one list kept and refilled by every call (read it before the
        /// next); ids that no longer resolve to a pillar are dropped. Sets <see cref="Fold"/>.</summary>
        internal static List<ZDO> Pillars()
        {
            live.Clear();
            ZDOMan man = ZDOMan.instance;
            ListHash hash = ListHash.Start(0);
            if (man != null && indexedOn == man)
                Collect(man, ref hash);
            hash.Add(live.Count);
            Fold = hash.Value;
            return live;
        }

        private static void Collect(ZDOMan man, ref ListHash hash)
        {
            dead.Clear();
            foreach (ZDOID id in found)
            {
                ZDO zdo = man.GetZDO(id);
                if (zdo == null || !zdo.IsValid() || !SeaGateFields.IsPillar(zdo))
                {
                    dead.Add(id);
                    continue;
                }
                live.Add(zdo);
                hash.Add(id.UserID);
                hash.Add(id.ID);
                hash.Add(zdo.DataRevision);
            }
            foreach (ZDOID id in dead)
                found.Remove(id);
        }
    }

    /// <summary>Every ZDO the game creates (<c>CreateNewZDO</c>) or receives (<c>RPC_ZDOData</c>, after reading its data)
    /// passes <c>ZDOMan.AddIfPortal</c> with its prefab; a sea gate pillar's joins the server's index there. This runs
    /// for every ZDO update a server receives, so anything but a pillar leaves after one comparison. Not gated on the
    /// settings: the index must be complete when sea gates are switched on.</summary>
    [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.AddIfPortal))]
    public static class PillarIndexPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ZDOMan __instance, ZDO zdo, int prefabHash)
        {
            if (prefabHash != SeaGateFields.PillarHash || zdo == null || ZNet.instance == null || !ZNet.instance.IsServer())
                return;
            SeaGateScan.Add(__instance, zdo);
        }
    }
}
