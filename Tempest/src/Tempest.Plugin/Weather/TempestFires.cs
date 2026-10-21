// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Strike Starts Fires ignites the strike point with the game's own fire objects.
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Tempest.Weather;
using UnityEngine;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>
    /// Ignition works like a landing ember of the game (<see cref="Cinder"/>.OnHit): the game's own burn check
    /// (<see cref="Cinder.CanBurn"/>: not under water, a tree or log, dry grass outside the Mountains and Deep North, no rain)
    /// decides, then the cinder's fire object is spawned and set up with the cinder's spread. The spawned fire is an ordinary
    /// networked object owned by the striking client; the game burns it out, puts it out in rain and spreads it only where it
    /// allows cinders (Ashlands or the Fire world modifier). Tempest does not track or delete it. Main thread only.
    /// </summary>
    internal static class TempestFires
    {
        static readonly Collider[] Hits = new Collider[32];
        static Cinder? source;
        static bool looked;
        static int mask = -1;
        static MeadworksLog? log;

        internal static void Initialize(MeadworksLog logger) => log = logger;

        internal static void Shutdown()
        {
            source = null;
            looked = false;
            log = null;
        }

        /// <summary>Sets one burnable thing at the strike alight: a tree within the strike's area first, otherwise the ground under it.</summary>
        internal static void Ignite(Vector3 at, float radius, StrikeEffects effects)
        {
            try
            {
                var cinder = Source();
                if (cinder == null) return;
                if (mask < 0) mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain");
                if (radius > 0)
                {
                    var count = Physics.OverlapSphereNonAlloc(at, radius, Hits, mask);
                    for (var i = 0; i < count; i++)
                    {
                        var collider = Hits[i];
                        if (collider == null || Classify(collider) != StrikeTarget.Tree) continue;
                        var point = collider.ClosestPointOnBounds(at);
                        var normal = (at - point).sqrMagnitude > 0.0001f ? (at - point).normalized : Vector3.up;
                        if (Try(cinder, collider, point, normal, effects)) { System.Array.Clear(Hits, 0, count); return; }
                    }
                    System.Array.Clear(Hits, 0, count);
                }
                if (Physics.Raycast(at + Vector3.up * 5, Vector3.down, out var hit, 10, mask))
                    Try(cinder, hit.collider, hit.point, hit.normal, effects);
            }
            catch (System.Exception error)
            {
                log?.Warning("Strike fire failed: " + error.GetType().Name);
            }
        }

        static bool Try(Cinder cinder, Collider collider, Vector3 point, Vector3 normal, StrikeEffects effects)
        {
            if (!StrikeTargets.CanIgnite(effects, Classify(collider), InsideWard(point), ShieldGenerator.IsInsideShield(point))) return false;
            if (!Cinder.CanBurn(collider, point, out var terrain, cinder.m_chanceToIgniteGrass)) return false;
            var prefab = terrain ? cinder.m_firePrefab : cinder.m_houseFirePrefab;
            if (prefab == null) return false;
            var fire = Object.Instantiate(prefab, point + normal * 0.1f, Quaternion.identity);
            var spawner = fire.GetComponent<CinderSpawner>();
            if (spawner != null) spawner.Setup(cinder.m_spread, collider.gameObject);
            return true;
        }

        static StrikeTarget Classify(Collider collider)
        {
            if (collider.GetComponentInParent<Piece>() != null) return StrikeTarget.Piece;
            var character = collider.GetComponentInParent<Character>();
            if (character != null) return character.IsPlayer() ? StrikeTarget.Player : StrikeTarget.Creature;
            if (collider.GetComponentInParent<TreeBase>() != null || collider.GetComponentInParent<TreeLog>() != null) return StrikeTarget.Tree;
            return StrikeTarget.Other;
        }

        /// <summary>The game's fire does not check wards; Tempest never ignites inside an active one.</summary>
        static bool InsideWard(Vector3 point)
        {
            foreach (var area in PrivateArea.m_allAreas)
                if (area != null && area.IsEnabled() && area.IsInside(point, 0)) return true;
            return false;
        }

        /// <summary>The game's ember object, found by its component so no prefab name is assumed.</summary>
        static Cinder? Source()
        {
            if (looked) return source;
            if (ZNetScene.instance == null) return null;
            looked = true;
            foreach (var prefab in ZNetScene.instance.m_prefabs)
            {
                var cinder = prefab != null ? prefab.GetComponent<Cinder>() : null;
                if (cinder != null && cinder.m_firePrefab != null && cinder.m_houseFirePrefab != null) { source = cinder; break; }
            }
            if (source == null) log?.Warning("The game's ember object was not found; Strike Starts Fires only adds fire damage.");
            return source;
        }
    }
}
