// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// RangedSecondaryCooldownSystem.cs. Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespaces, canonical catalogs, Crucible settings and diagnostics;
// native source algorithms retained. Candidate stage, dispatch and prefab application are a separate unit.
// Changed by expDARE, 2026: Inscribe inscriptions shorten the cooldown through meadworks.inscribe.secondary_cooldown.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Edge.Secondaries.Adopted;
using ExtraPotions.Edge.Config;

internal static class RangedSecondaryCooldownSystem
{
    private const string FallbackIconPrefabName = "Bow";

    internal static bool CanStart(Humanoid humanoid, ItemDrop.ItemData? weapon)
    {
        if (humanoid == null ||
            weapon == null ||
            !SecondaryAttackRuntimeFacade.TryGetDefinition(weapon, out SecondaryAttackDefinition definition) ||
            definition.Behavior is not ProjectileSecondaryBehavior projectileBehavior)
        {
            return true;
        }

        return CanUse(humanoid, projectileBehavior);
    }

    internal static bool CanUse(Attack attack, ProjectileSecondaryBehavior behavior)
    {
        if (attack?.m_character == null)
        {
            return true;
        }

        return CanUse(attack.m_character, behavior);
    }

    internal static bool StartCooldown(Attack attack, ProjectileSecondaryBehavior behavior)
    {
        if (attack?.m_character == null)
        {
            return true;
        }

        return StartCooldown(attack.m_character, attack.m_weapon, behavior);
    }

    private static bool CanUse(
        Character attacker,
        ProjectileSecondaryBehavior behavior)
    {
        if (attacker == null)
        {
            return true;
        }

        if (behavior == null)
        {
            return true;
        }

        string cooldownGroup =
            SecondaryAttackCooldownGroupResolver.ResolveRangedGroup(behavior);
        return SecondaryCooldownGroupSystem.IsReady(attacker, cooldownGroup);
    }

    private static bool StartCooldown(
        Character attacker,
        ItemDrop.ItemData? weapon,
        ProjectileSecondaryBehavior behavior)
    {
        if (attacker == null)
        {
            return true;
        }

        if (behavior == null)
        {
            return true;
        }

        string cooldownGroup =
            SecondaryAttackCooldownGroupResolver.ResolveRangedGroup(behavior);
        float finalCooldown = ReadDuration(attacker, weapon, behavior);
        Sprite? icon = ResolveWeaponIcon(weapon) ?? ResolveRegisteredIcon();
        return SecondaryCooldownGroupSystem.TryConsume(
            attacker,
            cooldownGroup,
            finalCooldown,
            icon, ProjectileRuntimeSystem.GetPresetName(behavior.Preset));
    }

    internal static float ReadDuration(Character attacker, ItemDrop.ItemData? weapon, ProjectileSecondaryBehavior behavior)
    {
        float baseCooldown = Mathf.Max(0f, behavior.Cooldown);
        float skillLevel = ResolveCooldownSkillLevel(attacker, weapon);
        float reduction = MeleePresetCooldownSystem.ReadReduction(skillLevel, behavior.CooldownReductionFactor);
        return InscribeCooldowns.Apply(attacker, weapon, Mathf.Max(0f, baseCooldown * (1f - reduction)));
    }

    private static float ResolveCooldownSkillLevel(
        Character attacker,
        ItemDrop.ItemData? weapon)
    {
        if (weapon?.m_shared == null ||
            weapon.m_shared.m_skillType == Skills.SkillType.None)
        {
            return 0f;
        }

        return Mathf.Clamp(
            attacker.GetSkillLevel(weapon.m_shared.m_skillType),
            0f,
            100f);
    }

    private static Sprite? ResolveWeaponIcon(ItemDrop.ItemData? weapon)
    {
        return weapon?.m_shared?.m_icons is { Length: > 0 } icons ? icons[0] : null;
    }

    private static Sprite? ResolveIcon(ObjectDB objectDb, string itemPrefabName)
    {
        ItemDrop? itemDrop =
            objectDb.GetItemPrefab(itemPrefabName)?.GetComponent<ItemDrop>();
        return itemDrop?.m_itemData?.m_shared?.m_icons is { Length: > 0 } icons
            ? icons[0]
            : null;
    }

    private static Sprite? ResolveRegisteredIcon()
    {
        return ObjectDB.instance != null
            ? ResolveIcon(ObjectDB.instance, FallbackIconPrefabName)
            : null;
    }
}
