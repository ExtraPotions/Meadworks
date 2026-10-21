// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackDefinitionCompiler.cs (native definition types and compilation; game algorithms retained). Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace, portable config/canonical catalog and diagnostics boundaries;
// portable rotation values converted at the native boundary. No ServerSync or source UI/config ownership.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Edge.Secondaries.Adopted;

internal static class SecondaryAttackDefinitionCompiler
{
    internal static bool TryCreateDefinition(
        SecondaryAttackDefinitionBuildContext buildContext,
        string prefabName,
        ItemDrop itemDrop,
        NormalizedWeaponConfig weaponConfig,
        out SecondaryAttackDefinition? definition)
    {
        definition = null;
        ItemDrop.ItemData.SharedData? sharedData = itemDrop.m_itemData?.m_shared;
        if (sharedData == null)
        {
            return false;
        }

        if (!weaponConfig.Enabled)
        {
            return false;
        }

        if (IsPresetOptOut(weaponConfig))
        {
            return false;
        }

        bool hasMeleeFeatureConfig = weaponConfig.HasEnabledMeleeFeatureConfig;
        string secondaryType = weaponConfig.Secondary?.Type?.Trim() ?? "";
        DefinitionValidationResult validation = ValidateDefinitionRequest(
            prefabName, sharedData, weaponConfig, secondaryType, hasMeleeFeatureConfig);
        switch (validation.Disposition)
        {
            case DefinitionValidationDisposition.EffectOnly:
                definition = SecondaryAttackManager.CreateEffectOnlyDefinition(prefabName, weaponConfig);
                return true;
            case DefinitionValidationDisposition.Skip:
                return false;
            default:
                return TryCreateValidatedDefinition(
                    buildContext, prefabName, sharedData, validation.PrimaryAttack!, weaponConfig,
                    secondaryType, hasMeleeFeatureConfig, out definition);
        }
    }

    internal static bool IsPresetOptOut(NormalizedWeaponConfig weaponConfig)
    {
        NormalizedSecondaryModeConfig? secondary = weaponConfig.Secondary;
        if (secondary == null)
        {
            return false;
        }

        if (string.Equals(secondary.Type, "none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(secondary.Type, "projectile", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(secondary.Projectile?.Preset, "none", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreateValidatedDefinition(
        SecondaryAttackDefinitionBuildContext buildContext,
        string prefabName,
        ItemDrop.ItemData.SharedData sharedData,
        Attack primaryAttack,
        NormalizedWeaponConfig weaponConfig,
        string secondaryType,
        bool hasMeleeFeatureConfig,
        out SecondaryAttackDefinition? definition)
    {
        definition = null;

        if (secondaryType == "summonEmpower")
        {
            return SecondaryAttackManager.TryCreateSummonEmpowerDefinition(prefabName, sharedData, primaryAttack, weaponConfig, out definition);
        }

        if (secondaryType == "shieldConvert")
        {
            return SecondaryAttackManager.TryCreateShieldConvertDefinition(prefabName, sharedData, primaryAttack, weaponConfig, out definition);
        }

        if (secondaryType == "projectile")
        {
            if (primaryAttack.m_attackType != Attack.AttackType.Projectile)
            {
                RuleDiagnostics.Warning($"Skipping {prefabName}: primary attack is not projectile-based.");
                if (hasMeleeFeatureConfig)
                {
                    definition = SecondaryAttackManager.CreateEffectOnlyDefinition(prefabName, weaponConfig);
                    return true;
                }

                return false;
            }

            return SecondaryAttackManager.TryCreateCustomPayloadDefinition(prefabName, sharedData, primaryAttack, weaponConfig, out definition);
        }

        if (secondaryType == "aftershock")
        {
            return SecondaryAttackManager.TryCreateAftershockDefinition(buildContext, prefabName, sharedData, primaryAttack, weaponConfig, out definition);
        }

        if (secondaryType == "fractureLine")
        {
            return SecondaryAttackManager.TryCreateFractureLineDefinition(buildContext, prefabName, primaryAttack, weaponConfig, out definition);
        }

        string sourcePrefabName = string.IsNullOrWhiteSpace(weaponConfig.Secondary?.CopyFrom)
            ? prefabName
            : weaponConfig.Secondary!.CopyFrom.Trim();
        if (secondaryType == "copy")
        {
            if (!SecondaryAttackManager.TryResolveSecondarySourceAttack(buildContext.ObjectDb, sourcePrefabName, out Attack? sourceSecondaryAttack, out string reason))
            {
                if (buildContext.EmitMissingWarnings)
                {
                    RuleDiagnostics.Warning($"Skipping {prefabName}: {reason}");
                }

                if (hasMeleeFeatureConfig)
                {
                    definition = SecondaryAttackManager.CreateEffectOnlyDefinition(prefabName, weaponConfig);
                    return true;
                }

                return false;
            }

            return SecondaryAttackManager.TryCreateSecondaryOverrideDefinition(prefabName, sourcePrefabName, primaryAttack, sourceSecondaryAttack!, weaponConfig, out definition);
        }

        if (hasMeleeFeatureConfig)
        {
            definition = SecondaryAttackManager.CreateEffectOnlyDefinition(prefabName, weaponConfig);
            return true;
        }

        RuleDiagnostics.Warning($"Skipping {prefabName}: unsupported secondary.type '{secondaryType}'.");
        return false;
    }

    private enum DefinitionValidationDisposition
    {
        Continue,
        Skip,
        EffectOnly
    }

    private readonly struct DefinitionValidationResult
    {
        public DefinitionValidationResult(DefinitionValidationDisposition disposition, Attack? primaryAttack = null)
        {
            Disposition = disposition;
            PrimaryAttack = primaryAttack;
        }

        public DefinitionValidationDisposition Disposition { get; }

        public Attack? PrimaryAttack { get; }
    }

    private static DefinitionValidationResult ValidateDefinitionRequest(
        string prefabName,
        ItemDrop.ItemData.SharedData sharedData,
        NormalizedWeaponConfig weaponConfig,
        string secondaryType,
        bool hasMeleeFeatureConfig)
    {
        if (weaponConfig.Secondary == null)
        {
            return !hasMeleeFeatureConfig
                ? new DefinitionValidationResult(DefinitionValidationDisposition.Skip)
                : new DefinitionValidationResult(DefinitionValidationDisposition.EffectOnly);
        }

        if (string.IsNullOrWhiteSpace(secondaryType))
        {
            RuleDiagnostics.Warning($"Skipping {prefabName}: a secondary behavior preset is required.");
            return hasMeleeFeatureConfig
                ? new DefinitionValidationResult(DefinitionValidationDisposition.EffectOnly)
                : new DefinitionValidationResult(DefinitionValidationDisposition.Skip);
        }

        if (secondaryType == "projectile" &&
            (weaponConfig.Secondary?.Projectile == null || string.IsNullOrWhiteSpace(weaponConfig.Secondary.Projectile.Preset)))
        {
            RuleDiagnostics.Warning($"Skipping {prefabName}: ranged secondary requires preset.");
            return hasMeleeFeatureConfig
                ? new DefinitionValidationResult(DefinitionValidationDisposition.EffectOnly)
                : new DefinitionValidationResult(DefinitionValidationDisposition.Skip);
        }

        Attack primaryAttack = sharedData.m_attack;
        if (primaryAttack == null || string.IsNullOrWhiteSpace(primaryAttack.m_attackAnimation))
        {
            RuleDiagnostics.Warning($"Skipping {prefabName}: primary attack is missing.");
            return hasMeleeFeatureConfig
                ? new DefinitionValidationResult(DefinitionValidationDisposition.EffectOnly)
                : new DefinitionValidationResult(DefinitionValidationDisposition.Skip);
        }

        return new DefinitionValidationResult(DefinitionValidationDisposition.Continue, primaryAttack);
    }
}

internal readonly struct SecondaryAttackDefinitionBuildContext
{
    public SecondaryAttackDefinitionBuildContext(
        ObjectDB objectDb,
        bool emitMissingWarnings)
    {
        ObjectDb = objectDb;
        EmitMissingWarnings = emitMissingWarnings;
    }

    public ObjectDB ObjectDb { get; }

    public bool EmitMissingWarnings { get; }
}
