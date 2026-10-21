// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackCooldownGroupResolver.cs. Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespaces, canonical catalogs, Crucible settings and diagnostics;
// native source algorithms retained. Candidate stage, dispatch and prefab application are a separate unit.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Edge.Secondaries.Adopted;
using ExtraPotions.Edge.Config;

internal static class SecondaryAttackCooldownGroupResolver
{
    internal static void Apply(
        string prefabName,
        ItemDrop itemDrop,
        SecondaryAttackDefinition definition)
    {
        string automaticFamilyGroup =
            WeaponFamilies.CooldownGroup(NativeWeaponFacts.Read(itemDrop));

        Assign(definition.SneakAmbush?.PresetCooldown, automaticFamilyGroup, "sneakAmbush");
        Assign(definition.CleavingThrust?.PresetCooldown, automaticFamilyGroup, "cleavingThrust");
        Assign(definition.LaunchSlam?.PresetCooldown, automaticFamilyGroup, "launchSlam");
        Assign(definition.KnockbackChain?.PresetCooldown, automaticFamilyGroup, "knockbackChain");
        Assign(definition.Aftershock?.PresetCooldown, automaticFamilyGroup, "aftershock");
        Assign(definition.RiftTrail?.PresetCooldown, automaticFamilyGroup, "riftTrail");
        Assign(definition.FractureLine?.PresetCooldown, automaticFamilyGroup, "fractureLine");
        Assign(definition.HarvestSweep?.PresetCooldown, automaticFamilyGroup, "harvestSweep");
        Assign(
            definition.OnProjectileHit?.PresetCooldown,
            automaticFamilyGroup,
            definition.OnProjectileHit == null
                ? null
                : PresetCatalog.Canonicalize(definition.OnProjectileHit.Preset.ToString()));
        Assign(definition.Boomerang?.PresetCooldown, automaticFamilyGroup, "boomerang");
        Assign(definition.SpinningSweep?.PresetCooldown, automaticFamilyGroup, "spinningSweep");

        switch (definition.Behavior)
        {
            case ProjectileSecondaryBehavior projectile:
                projectile.CooldownGroup = !string.IsNullOrWhiteSpace(automaticFamilyGroup)
                    ? automaticFamilyGroup
                    : CreatePresetFallback("ranged", ProjectileRuntimeSystem.GetPresetName(projectile.Preset));
                break;
            case SummonEmpowerSecondaryBehavior summonEmpower:
                summonEmpower.PresetCooldown.CooldownGroup =
                    WeaponFamilies.BloodSummonCooldownGroup;
                break;
            case ShieldConvertSecondaryBehavior shieldConvert:
                shieldConvert.PresetCooldown.CooldownGroup =
                    WeaponFamilies.BloodShieldCooldownGroup;
                break;
        }
    }

    internal static string ResolveMeleeGroup(
        string presetName,
        MeleePresetCooldownDefinition? cooldown)
    {
        return !string.IsNullOrWhiteSpace(cooldown?.CooldownGroup)
            ? cooldown!.CooldownGroup.Trim()
            : CreatePresetFallback("melee", presetName);
    }

    internal static string ResolveRangedGroup(ProjectileSecondaryBehavior? behavior)
    {
        if (behavior == null)
        {
            return "";
        }

        return !string.IsNullOrWhiteSpace(behavior.CooldownGroup)
            ? behavior.CooldownGroup.Trim()
            : CreatePresetFallback("ranged", ProjectileRuntimeSystem.GetPresetName(behavior.Preset));
    }

    private static void Assign(
        MeleePresetCooldownDefinition? cooldown,
        string automaticFamilyGroup,
        string? presetName)
    {
        if (cooldown == null)
        {
            return;
        }

        cooldown.CooldownGroup = !string.IsNullOrWhiteSpace(automaticFamilyGroup)
            ? automaticFamilyGroup
            : CreatePresetFallback("melee", presetName);
    }

    private static string CreatePresetFallback(string domain, string? presetName)
    {
        string normalizedDomain = NormalizeToken(domain);
        string normalizedPreset = NormalizeToken(presetName);
        if (string.IsNullOrEmpty(normalizedDomain) || string.IsNullOrEmpty(normalizedPreset))
        {
            return "";
        }

        return $"preset:{normalizedDomain}:{normalizedPreset}";
    }

    private static string NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value!.Trim().ToLowerInvariant();
    }
}
