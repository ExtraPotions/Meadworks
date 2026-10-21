// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Read the native facts required by the existing portable family resolver; no attack or prefab mutation.
using System;
using System.Linq;
using ExtraPotions.Edge.Secondaries;
namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
internal static class NativeWeaponFacts
{
    internal static WeaponFacts Read(ItemDrop drop)
    {
        var shared = drop.m_itemData.m_shared;
        var primary = shared.m_attack;
        var secondary = shared.m_secondaryAttack;
        var payload = primary?.m_attackProjectile;
        var spawn = payload != null ? payload.GetComponent<SpawnAbility>() : null;
        return new WeaponFacts
        {
            PrefabName = drop.gameObject.name,
            Skill = Enum.TryParse<WeaponSkill>(shared.m_skillType.ToString(), out var skill) ? skill : WeaponSkill.Other,
            ItemType = Enum.TryParse<WeaponItemType>(shared.m_itemType.ToString(), out var type) ? type : WeaponItemType.Other,
            HasPrimary = primary != null, PrimaryAnimation = primary?.m_attackAnimation ?? "",
            PrimaryAttackKind = Enum.TryParse<AttackKind>(primary?.m_attackType.ToString(), out var kind) ? kind : AttackKind.None,
            PrimaryHasProjectile = payload != null && payload.GetComponent<Projectile>() != null,
            PrimaryProjectileIsIProjectile = payload != null && payload.GetComponent<IProjectile>() != null,
            PrimaryProjectileHasAoe = payload != null && payload.GetComponentInChildren<Aoe>(true) != null,
            PrimaryProjectileHasSpawnAbility = payload != null && payload.GetComponentInChildren<SpawnAbility>(true) != null,
            PrimarySpawnsSummon = spawn != null && spawn.m_spawnPrefab != null && spawn.m_spawnPrefab.Any(p => p != null),
            PrimaryRequiresReload = primary?.m_requiresReload == true,
            AmmoType = shared.m_ammoType ?? "",
            HasSecondary = secondary != null && !string.IsNullOrWhiteSpace(secondary.m_attackAnimation),
            SecondaryAnimation = secondary?.m_attackAnimation ?? "",
            SecondaryAttackKind = Enum.TryParse<AttackKind>(secondary?.m_attackType.ToString(), out var secondaryKind) ? secondaryKind : AttackKind.None,
            BombHasArea = NativeBombFacts.BombProjectileSpawnsAoe(payload),
        };
    }
}
