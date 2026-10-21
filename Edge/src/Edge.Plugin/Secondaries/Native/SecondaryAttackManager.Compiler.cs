// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackManager.cs and SecondaryAttackManager.CommonRuntime.cs (attack construction, resource scaling and native source resolution; Hunter compat omitted). Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace, portable config/canonical catalog and diagnostics boundaries;
// portable rotation values converted at the native boundary. No ServerSync or source UI/config ownership.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using UnityEngine;
namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Edge.Secondaries.Adopted;
internal static partial class SecondaryAttackManager
{
    private const string StaffRapidFireAnimation = "staff_rapidfire";
    internal static Attack BuildSecondaryAttack(Attack sourceAttack, SecondaryAttackDefinition definition)
    {
        Attack secondaryAttack = CloneAttack(sourceAttack);
        if (definition.BehaviorType == SecondaryAttackBehaviorType.SummonEmpower ||
            definition.BehaviorType == SecondaryAttackBehaviorType.ShieldConvert)
        {
            secondaryAttack.m_attackType = Attack.AttackType.None;
            secondaryAttack.m_bowDraw = false;
            secondaryAttack.m_requiresReload = false;
            secondaryAttack.m_projectiles = 1;
            secondaryAttack.m_projectileBursts = 1;
            secondaryAttack.m_attackChainLevels = 1;
            secondaryAttack.m_attackRandomAnimations = 0;
        }
        else if (definition.BehaviorType == SecondaryAttackBehaviorType.Projectile)
        {
            secondaryAttack.m_attackType = Attack.AttackType.Projectile;
            secondaryAttack.m_bowDraw = sourceAttack.m_bowDraw;
            secondaryAttack.m_requiresReload = sourceAttack.m_requiresReload;
            if (definition.Behavior is not ProjectileSecondaryBehavior { Preset: SecondaryAttackPreset.Piercing or SecondaryAttackPreset.Burst })
            {
                secondaryAttack.m_projectiles = 1;
                secondaryAttack.m_projectileBursts = 1;
            }
        }
        else if (definition.BehaviorType == SecondaryAttackBehaviorType.Aftershock)
        {
            secondaryAttack.m_attackType = Attack.AttackType.Area;
            secondaryAttack.m_bowDraw = false;
            secondaryAttack.m_requiresReload = false;
            secondaryAttack.m_projectiles = 1;
            secondaryAttack.m_projectileBursts = 1;
        }

        secondaryAttack.m_attackAnimation = definition.AttackAnimation;
        if (definition.BehaviorType == SecondaryAttackBehaviorType.Projectile &&
            string.Equals(secondaryAttack.m_attackAnimation, StaffRapidFireAnimation, StringComparison.Ordinal))
        {
            secondaryAttack.m_loopingAttack = true;
        }

        secondaryAttack.m_attackHealth = definition.RawAttackHealth;
        secondaryAttack.m_attackHealthPercentage = definition.RawAttackHealthPercentage;
        secondaryAttack.m_attackStamina = definition.RawAttackStamina;
        secondaryAttack.m_attackEitr = definition.RawAttackEitr;
        secondaryAttack.m_drawStaminaDrain = definition.RawDrawStamina;
        secondaryAttack.m_drawEitrDrain = definition.RawDrawEitr;
        secondaryAttack.m_reloadStaminaDrain = definition.RawReloadStamina;
        secondaryAttack.m_reloadEitrDrain = definition.RawReloadEitr;
        secondaryAttack.m_damageMultiplier *= definition.OutputMultiplier;
        secondaryAttack.m_forceMultiplier *= definition.OutputMultiplier;
        secondaryAttack.m_staggerMultiplier *= definition.OutputMultiplier;
        if (definition.HasCustomAttackAnimation)
        {
            secondaryAttack.m_attackChainLevels = 1;
            secondaryAttack.m_attackRandomAnimations = 0;
        }

        return secondaryAttack;
    }

    private static bool TryParsePreset(string presetText, out SecondaryAttackPreset preset)
    {
        string configuredPreset = presetText.Trim();
        preset = default;
        return PresetCatalog.TryGet(
                   configuredPreset,
                   out PresetInfo presetInfo) &&
               presetInfo.Group == PresetGroup.Ranged &&
               Enum.TryParse(presetInfo.Key, ignoreCase: true, out preset);
    }

    private static void ApplyAttackResourceScaling(SecondaryAttackDefinition definition, Attack sourceAttack, float resourceMultiplier)
    {
        float multiplier = Mathf.Max(0f, resourceMultiplier);
        definition.ResourceMultiplier = multiplier;
        definition.RawAttackHealth = Mathf.Max(0f, sourceAttack.m_attackHealth * multiplier);
        definition.RawAttackHealthPercentage = Mathf.Max(0f, sourceAttack.m_attackHealthPercentage * multiplier);
        definition.RawAttackStamina = Mathf.Max(0f, sourceAttack.m_attackStamina * multiplier);
        definition.RawAttackEitr = Mathf.Max(0f, sourceAttack.m_attackEitr * multiplier);
        definition.RawDrawStamina = Mathf.Max(0f, sourceAttack.m_drawStaminaDrain * multiplier);
        definition.RawDrawEitr = Mathf.Max(0f, sourceAttack.m_drawEitrDrain * multiplier);
        definition.RawReloadStamina = Mathf.Max(0f, sourceAttack.m_reloadStaminaDrain * multiplier);
        definition.RawReloadEitr = Mathf.Max(0f, sourceAttack.m_reloadEitrDrain * multiplier);
    }

    internal static Attack ResolveSourceAttack(ObjectDB objectDb, ItemDrop itemDrop, SecondaryAttackDefinition definition)
    {
        if (definition.BehaviorType == SecondaryAttackBehaviorType.Projectile)
        {
            return itemDrop.m_itemData.m_shared.m_attack;
        }

        if (definition.BehaviorType == SecondaryAttackBehaviorType.SummonEmpower ||
            definition.BehaviorType == SecondaryAttackBehaviorType.ShieldConvert)
        {
            return itemDrop.m_itemData.m_shared.m_attack;
        }

        if (definition.BehaviorType == SecondaryAttackBehaviorType.Aftershock)
        {
            AftershockSecondaryBehavior? aftershockBehavior = definition.Behavior as AftershockSecondaryBehavior;
            string aftershockSourcePrefab = string.IsNullOrWhiteSpace(aftershockBehavior?.SourcePrefabName)
                ? itemDrop.m_itemData.m_dropPrefab?.name ?? itemDrop.name
                : aftershockBehavior!.SourcePrefabName;
            if (TryResolveAftershockSourceAttack(objectDb, aftershockSourcePrefab, out Attack? aftershockSourceAttack, out _))
            {
                return aftershockSourceAttack!;
            }

            return ResolveOriginalSecondaryAttack(
                       objectDb,
                       itemDrop.m_itemData.m_dropPrefab?.name ?? itemDrop.name,
                       itemDrop) ??
                   itemDrop.m_itemData.m_shared.m_attack;
        }

        if (definition.BehaviorType == SecondaryAttackBehaviorType.FractureLine)
        {
            FractureLineSecondaryBehavior? fractureLineBehavior = definition.Behavior as FractureLineSecondaryBehavior;
            string fractureLineSourcePrefab = string.IsNullOrWhiteSpace(fractureLineBehavior?.SourcePrefabName)
                ? itemDrop.m_itemData.m_dropPrefab?.name ?? itemDrop.name
                : fractureLineBehavior!.SourcePrefabName;
            if (TryResolveFractureLineSourceAttack(objectDb, fractureLineSourcePrefab, out Attack? fractureLineSourceAttack, out _))
            {
                return fractureLineSourceAttack!;
            }

            return ResolveOriginalSecondaryAttack(
                       objectDb,
                       itemDrop.m_itemData.m_dropPrefab?.name ?? itemDrop.name,
                       itemDrop) ??
                   itemDrop.m_itemData.m_shared.m_attack;
        }

        CopiedSecondaryBehavior? copiedBehavior = definition.Behavior as CopiedSecondaryBehavior;
        string sourcePrefabName = string.IsNullOrWhiteSpace(copiedBehavior?.SourcePrefabName)
            ? itemDrop.m_itemData.m_dropPrefab?.name ?? itemDrop.name
            : copiedBehavior!.SourcePrefabName;
        if (TryResolveSecondarySourceAttack(objectDb, sourcePrefabName, out Attack? sourceAttack, out _))
        {
            return sourceAttack!;
        }

        return ResolveOriginalSecondaryAttack(
                   objectDb,
                   itemDrop.m_itemData.m_dropPrefab?.name ?? itemDrop.name,
                   itemDrop) ??
               itemDrop.m_itemData.m_shared.m_attack;
    }

    internal static bool TryResolveSecondarySourceAttack(ObjectDB objectDb, string sourcePrefabName, out Attack? sourceAttack, out string reason)
    {
        sourceAttack = null;
        reason = "";
        ItemDrop? sourceItemDrop = FindItemDropByPrefabName(objectDb, sourcePrefabName);
        if (sourceItemDrop == null)
        {
            reason = $"source weapon '{sourcePrefabName}' was not found in ObjectDB.";
            return false;
        }

        sourceAttack = ResolveOriginalSecondaryAttack(objectDb, sourcePrefabName, sourceItemDrop);
        if (sourceAttack == null || string.IsNullOrWhiteSpace(sourceAttack.m_attackAnimation))
        {
            reason = $"source weapon '{sourcePrefabName}' does not have a valid secondary attack.";
            return false;
        }

        return true;
    }

    private static bool TryResolveAftershockSourceAttack(ObjectDB objectDb, string sourcePrefabName, out Attack? sourceAttack, out string reason)
    {
        sourceAttack = null;
        reason = "";
        ItemDrop? sourceItemDrop = FindItemDropByPrefabName(objectDb, sourcePrefabName);
        if (sourceItemDrop == null)
        {
            reason = $"aftershock source weapon '{sourcePrefabName}' was not found in ObjectDB.";
            return false;
        }

        Attack? primaryAttack = sourceItemDrop.m_itemData.m_shared.m_attack;
        if (IsValidAftershockSourceAttack(primaryAttack))
        {
            sourceAttack = primaryAttack;
            return true;
        }

        Attack? secondaryAttack = ResolveOriginalSecondaryAttack(objectDb, sourcePrefabName, sourceItemDrop);
        if (IsValidAftershockSourceAttack(secondaryAttack))
        {
            sourceAttack = secondaryAttack;
            return true;
        }

        reason = $"aftershock source weapon '{sourcePrefabName}' does not have a valid Area primary or secondary attack.";
        return false;
    }

    private static bool IsValidAftershockSourceAttack(Attack? attack)
    {
        return attack != null &&
               attack.m_attackType == Attack.AttackType.Area &&
               !string.IsNullOrWhiteSpace(attack.m_attackAnimation) &&
               attack.m_attackRayWidth > 0f;
    }

    private static bool TryResolveFractureLineSourceAttack(ObjectDB objectDb, string sourcePrefabName, out Attack? sourceAttack, out string reason)
    {
        sourceAttack = null;
        reason = "";
        ItemDrop? sourceItemDrop = FindItemDropByPrefabName(objectDb, sourcePrefabName);
        if (sourceItemDrop == null)
        {
            reason = $"fractureLine source weapon '{sourcePrefabName}' was not found in ObjectDB.";
            return false;
        }

        Attack? secondaryAttack = ResolveOriginalSecondaryAttack(objectDb, sourcePrefabName, sourceItemDrop);
        if (IsValidFractureLineSourceAttack(secondaryAttack))
        {
            sourceAttack = secondaryAttack;
            return true;
        }

        Attack? primaryAttack = sourceItemDrop.m_itemData.m_shared.m_attack;
        if (IsValidFractureLineSourceAttack(primaryAttack))
        {
            sourceAttack = primaryAttack;
            return true;
        }

        reason = $"fractureLine source weapon '{sourcePrefabName}' does not have a valid melee primary or secondary attack.";
        return false;
    }

    private static bool IsValidFractureLineSourceAttack(Attack? attack)
    {
        return attack != null &&
               (attack.m_attackType == Attack.AttackType.Horizontal || attack.m_attackType == Attack.AttackType.Vertical) &&
               !string.IsNullOrWhiteSpace(attack.m_attackAnimation);
    }

    private static Attack? ResolveOriginalSecondaryAttack(
        ObjectDB objectDb,
        string sourcePrefabName,
        ItemDrop sourceItemDrop)
    {
        return SecondaryAttackObjectDbStateStore.TryGetOriginalSecondaryAttack(
            objectDb,
            sourcePrefabName,
            out Attack? originalSecondaryAttack)
            ? originalSecondaryAttack
            : sourceItemDrop.m_itemData.m_shared.m_secondaryAttack;
    }

    private static ItemDrop? FindItemDropByPrefabName(ObjectDB objectDb, string prefabName)
    {
        foreach (GameObject itemPrefab in objectDb.m_items)
        {
            if (itemPrefab == null || !string.Equals(itemPrefab.name, prefabName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return itemPrefab.GetComponent<ItemDrop>();
        }

        return null;
    }

    internal static Attack CloneAttack(Attack? sourceAttack)
    {
        if (sourceAttack == null)
        {
            return new Attack();
        }

        Attack clone = sourceAttack.Clone();
        return clone;
    }

}
