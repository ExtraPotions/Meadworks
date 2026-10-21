// SPDX-License-Identifier: GPL-3.0-only
// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0
// 47e45a6e8c3b389a51e42fc7387d1a4fb90af473
// Changed by expDARE, 2026: Wildblood namespace and selected native dependency closure.
using UnityEngine;
namespace ExtraPotions.Wildblood.AdoptedCm;
internal static class CreaturePrefabRegistry
{
    internal static bool IsPlayerPrefab(GameObject prefab)
    {
        return prefab.GetComponent<Player>() != null;
    }
}
