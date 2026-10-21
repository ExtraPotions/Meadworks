// Threshold - ExtraPotions Meadworks portals.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/Portals/PortalDiscovery.cs (EnsureDiscovered, Discover,
// Reclassify, WorldLoaded, IsPortalPrefab and the two world-load postfixes).
// Changed by expDARE, 2026: Threshold namespace and log; no ticker object (Threshold's plugin Update calls Tick); the
// private ZDOMan members through Harmony AccessTools (no publicizer); postfixes installed by Threshold's own Harmony
// instance only while Threshold has not stepped aside, and acting only while Threshold is enabled.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.Portals
{
    /// <summary>
    /// Widens the game's portal classification (<c>Game.PortalPrefabHash</c>, which puts a ZDO into the always-loaded
    /// portal list and its own save chunk) to every prefab carrying a <see cref="TeleportWorld"/>, so modded portals are
    /// destinations too (design A6, T11). Lazy and idempotent: nothing happens until <c>Game</c>, <c>ZNetScene</c> and
    /// <c>ZDOMan</c> exist; a new game session scans again. The one pass that moves already loaded ZDOs of the widened
    /// prefabs into the portal list runs right after a world loads, inside the game's own loading; otherwise on the next
    /// tick. Left unmoved, those ZDOs would be dropped from the next world save (the save writer skips portal prefabs in
    /// the sector lists).
    /// </summary>
    public static class ThresholdPortalDiscovery
    {
        const float IntervalSeconds = 5f;

        static readonly AccessTools.FieldRef<ZDOMan, Dictionary<ZDOID, ZDO>> ObjectsById =
            AccessTools.FieldRefAccess<ZDOMan, Dictionary<ZDOID, ZDO>>("m_objectsByID");
        static readonly Action<ZDOMan, ZDO, int> AddIfPortal = AccessTools.MethodDelegate<Action<ZDOMan, ZDO, int>>(
            AccessTools.Method(typeof(ZDOMan), "AddIfPortal", new[] { typeof(ZDO), typeof(int) }));

        // Not the Game instance last widened: a new game session creates a fresh Game and PortalPrefabHash.
        static Game? scannedFor;
        static bool loadFailed;
        static float nextTick;

        // The prefab hashes the scan for scannedFor added: a world loaded after the scan moves its ZDOs of these.
        static readonly HashSet<int> added = new HashSet<int>();

        /// <summary>Installs the world-load postfixes on Threshold's Harmony instance.</summary>
        public static void Patch(Harmony harmony)
        {
            var postfix = new HarmonyMethod(typeof(ThresholdPortalDiscovery), nameof(WorldLoaded));
            harmony.Patch(AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.LoadChunks)), postfix: postfix);
            harmony.Patch(AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.Load)), postfix: postfix);
        }

        /// <summary>From the plugin's Update, on every machine including a dedicated server.</summary>
        public static void Tick(bool enabled)
        {
            if (!enabled || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + IntervalSeconds;
            EnsureDiscovered();
        }

        public static void EnsureDiscovered()
        {
            var game = Game.instance;
            var scene = ZNetScene.instance;
            if (game == null || scene == null || ZDOMan.instance == null || scannedFor == game)
                return;
            Discover(game, scene);
            scannedFor = game;
            Reclassify(added);
        }

        /// <summary>After <c>ZDOMan.LoadChunks</c> or <c>Load</c>: ZDOs saved outside the portal chunk sit in the sector
        /// lists. Never throws into the game's world load.</summary>
        static void WorldLoaded()
        {
            try
            {
                if (!ThresholdModules.DiscoveryEnabled()) return;
                if (scannedFor != null && scannedFor == Game.instance && ZDOMan.instance != null)
                    Reclassify(added);
                else
                    EnsureDiscovered();
            }
            catch (Exception e)
            {
                if (!loadFailed)
                    Plugin.Log?.Error("Threshold: portal discovery after the world load failed (logged once): " + e);
                loadFailed = true;
            }
        }

        static void Discover(Game game, ZNetScene scene)
        {
            added.Clear();
            var known = new HashSet<int>(game.PortalPrefabHash);
            foreach (var prefab in scene.m_prefabs)
            {
                if (prefab == null || prefab.GetComponentInChildren<TeleportWorld>() == null)
                    continue;
                int hash = prefab.name.GetStableHashCode();
                if (known.Add(hash))
                {
                    game.PortalPrefabHash.Add(hash);
                    added.Add(hash);
                }
            }
            if (added.Count > 0) Plugin.Log?.Info("Threshold: " + added.Count + " modded portal prefab(s) join the portal list.");
        }

        /// <summary>ZDOs loaded or received before the scan were classified against the narrower hash list and sit in
        /// the ordinary sector lists; they move into the portal list now.</summary>
        static void Reclassify(HashSet<int> addedHashes)
        {
            if (addedHashes.Count == 0)
                return;
            var zdos = ZDOMan.instance;
            var toMove = new List<ZDO>();
            foreach (var pair in ObjectsById(zdos))
            {
                if (addedHashes.Contains(pair.Value.GetPrefab()))
                    toMove.Add(pair.Value);
            }
            foreach (var zdo in toMove)
                AddIfPortal(zdos, zdo, zdo.GetPrefab());
        }

        public static bool IsPortalPrefab(int prefabHash)
        {
            var game = Game.instance;
            return game != null && game.PortalPrefabHash.Contains(prefabHash);
        }

        public static void Reset()
        {
            scannedFor = null;
            added.Clear();
            loadFailed = false;
            nextTick = 0f;
        }
    }
}
