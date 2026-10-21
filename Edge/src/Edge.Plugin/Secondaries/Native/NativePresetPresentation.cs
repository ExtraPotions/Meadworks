// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackPresetCatalog.cs (native icon lookup subset). Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace and catalog boundary; source algorithms retained.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using UnityEngine;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Edge.Secondaries.Adopted;
namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
internal static class NativePresetPresentation
{
    internal static Sprite? ResolveIcon(string presetKey)
    {
        if (ObjectDB.instance == null || !PresetCatalog.TryGet(presetKey, out var info))
        {
            return null;
        }

        ItemDrop? itemDrop = ObjectDB.instance.GetItemPrefab(info.IconPrefabName)?.GetComponent<ItemDrop>();
        return itemDrop?.m_itemData?.m_shared?.m_icons is { Length: > 0 } icons ? icons[0] : null;
    }

    internal static string? GetKey(SecondaryAttackBehavior behavior)
    {
        return behavior switch
        {
            ProjectileSecondaryBehavior projectile => ProjectileRuntimeSystem.GetPresetName(projectile.Preset),
            SummonEmpowerSecondaryBehavior => "summonEmpower",
            ShieldConvertSecondaryBehavior => "shieldConvert",
            _ => null
        };
    }

    internal static string? GetKey(MeleeSpecialPreset preset) => PresetCatalog.TryGet(preset.ToString(), out var info) ? info.Key : null;
    internal static MeleePresetCooldownDefinition? CooldownFor(SecondaryAttackDefinition definition, string key) => key switch
    {
        "sneakAmbush" => definition.SneakAmbush?.PresetCooldown,
        "cleavingThrust" => definition.CleavingThrust?.PresetCooldown,
        "launchSlam" => definition.LaunchSlam?.PresetCooldown,
        "knockbackChain" => definition.KnockbackChain?.PresetCooldown,
        "aftershock" => definition.Aftershock?.PresetCooldown,
        "riftTrail" => definition.RiftTrail?.PresetCooldown,
        "fractureLine" => definition.FractureLine?.PresetCooldown,
        "harvestSweep" => definition.HarvestSweep?.PresetCooldown,
        "spinningSweep" => definition.SpinningSweep?.PresetCooldown,
        "boomerang" => definition.Boomerang?.PresetCooldown,
        "summonEmpower" => (definition.Behavior as SummonEmpowerSecondaryBehavior)?.PresetCooldown,
        "shieldConvert" => (definition.Behavior as ShieldConvertSecondaryBehavior)?.PresetCooldown,
        _ => definition.OnProjectileHit?.PresetCooldown
    };
}
