// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackWorldApplySystem.cs (native definition types and compilation; game algorithms retained). Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace, portable config/canonical catalog and diagnostics boundaries;
// portable rotation values converted at the native boundary. No ServerSync or source UI/config ownership.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
internal static class NativeBombFacts
{
    internal static bool BombProjectileSpawnsAoe(GameObject? projectilePrefab)
    {
        if (projectilePrefab == null)
        {
            return false;
        }

        Projectile? projectile = projectilePrefab.GetComponent<Projectile>();
        if (projectile == null)
        {
            return false;
        }

        if (projectile.m_aoe > 0f)
        {
            return true;
        }

        if (PrefabContainsAoe(projectile.m_spawnOnHit))
        {
            return true;
        }

        List<GameObject>? randomSpawnOnHit = projectile.m_randomSpawnOnHit;
        if (randomSpawnOnHit == null)
        {
            return false;
        }

        foreach (GameObject spawnPrefab in randomSpawnOnHit)
        {
            if (PrefabContainsAoe(spawnPrefab))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PrefabContainsAoe(GameObject? prefab)
    {
        return PrefabContainsAoe(prefab, new HashSet<GameObject>());
    }

    private static bool PrefabContainsAoe(GameObject? prefab, HashSet<GameObject> visitedPrefabs)
    {
        if (prefab == null || !visitedPrefabs.Add(prefab))
        {
            return false;
        }

        if (prefab.GetComponentInChildren<Aoe>(true) != null)
        {
            return true;
        }

        foreach (SpawnAbility spawnAbility in prefab.GetComponentsInChildren<SpawnAbility>(true))
        {
            foreach (GameObject spawnPrefab in spawnAbility.m_spawnPrefab ?? Array.Empty<GameObject>())
            {
                if (PrefabContainsAoe(spawnPrefab, visitedPrefabs))
                {
                    return true;
                }
            }
        }

        return false;
    }

}
