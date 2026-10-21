// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// MeleePresetCooldownSystem.cs. Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace, diagnostic and native boundary seams; source algorithms retained.
// Changed by expDARE, 2026: Inscribe inscriptions shorten the cooldown through meadworks.inscribe.secondary_cooldown.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries.Adopted;
using ExtraPotions.Edge.Runtime.Quickstep;

internal static class MeleePresetCooldownSystem
{
    internal static bool TryConsume(
        Character attacker,
        ItemDrop.ItemData? weapon,
        string presetName,
        MeleePresetCooldownDefinition cooldown)
    {
        if (attacker == null)
        {
            return true;
        }

        if (cooldown == null)
        {
            return true;
        }

        string cooldownGroup =
            SecondaryAttackCooldownGroupResolver.ResolveMeleeGroup(presetName, cooldown);
        weapon ??= ResolveCurrentWeapon(attacker);
        float finalCooldown = ReadDuration(attacker, weapon, cooldown);
        Sprite? icon = ResolveWeaponIcon(weapon) ?? ResolveRegisteredIcon(presetName);
        return SecondaryCooldownGroupSystem.TryConsume(
            attacker,
            cooldownGroup,
            finalCooldown,
            icon, presetName);
    }

    internal static float ReadDuration(Character attacker, ItemDrop.ItemData? weapon, MeleePresetCooldownDefinition cooldown)
    {
        float baseCooldown = Mathf.Max(0f, cooldown.Cooldown);
        float skillLevel = ResolveCooldownSkillLevel(attacker, weapon, cooldown.CooldownSkill);
        float reduction = ReadReduction(skillLevel, cooldown.CooldownReductionFactor);
        return InscribeCooldowns.Apply(attacker, weapon, Mathf.Max(0f, baseCooldown * (1f - reduction)));
    }

    internal static bool IsReady(
        Character attacker,
        string presetName,
        MeleePresetCooldownDefinition cooldown)
    {
        if (attacker == null)
        {
            return true;
        }

        if (cooldown == null)
        {
            return true;
        }

        string cooldownGroup =
            SecondaryAttackCooldownGroupResolver.ResolveMeleeGroup(presetName, cooldown);
        return SecondaryCooldownGroupSystem.IsReady(attacker, cooldownGroup);
    }

    internal static bool IsCooldownActive(
        Character attacker,
        string presetName,
        MeleePresetCooldownDefinition cooldown,
        out float remaining)
    {
        remaining = 0f;
        if (attacker == null)
        {
            return false;
        }

        if (cooldown == null)
        {
            return false;
        }

        string cooldownGroup =
            SecondaryAttackCooldownGroupResolver.ResolveMeleeGroup(presetName, cooldown);
        return SecondaryCooldownGroupSystem.IsCooldownActive(
            attacker,
            cooldownGroup,
            out remaining);
    }

    private static Sprite? ResolveWeaponIcon(ItemDrop.ItemData? weapon)
    {
        return weapon?.m_shared?.m_icons is { Length: > 0 } icons ? icons[0] : null;
    }

    private static Sprite? ResolveRegisteredIcon(string presetName)
    {
        return NativePresetPresentation.ResolveIcon(presetName);
    }

    private static ItemDrop.ItemData? ResolveCurrentWeapon(Character attacker)
    {
        return attacker is Humanoid humanoid ? humanoid.GetCurrentWeapon() : null;
    }

    private static float ResolveCooldownSkillLevel(
        Character attacker,
        ItemDrop.ItemData? weapon,
        string configuredSkill)
    {
        if (!TryResolveSkillType(configuredSkill, weapon, out Skills.SkillType skillType))
        {
            return 0f;
        }

        return Mathf.Clamp(attacker.GetSkillLevel(skillType), 0f, 100f);
    }

    internal static float ReadReduction(float level, float factor) => Mathf.Clamp01(level / 100f) * Mathf.Clamp01(factor);

    internal static bool TryResolveSkillType(
        string configuredSkill,
        ItemDrop.ItemData? weapon,
        out Skills.SkillType skillType)
    {
        string normalized = NormalizeSkillToken(configuredSkill);
        if (string.IsNullOrEmpty(normalized) ||
            normalized == "weapon" ||
            normalized == "current" ||
            normalized == "equipped")
        {
            if (weapon?.m_shared == null || weapon.m_shared.m_skillType == Skills.SkillType.None)
            {
                skillType = Skills.SkillType.None;
                return false;
            }

            skillType = weapon.m_shared.m_skillType;
            return true;
        }

        if (normalized is "none" or "off" or "disabled")
        {
            skillType = Skills.SkillType.None;
            return false;
        }

        string candidate = normalized switch
        {
            "sword" or "swords" => "Swords",
            "knife" or "knives" => "Knives",
            "spear" or "spears" => "Spears",
            "club" or "clubs" or "mace" or "maces" => "Clubs",
            "fist" or "fists" or "unarmed" => "Unarmed",
            "axe" or "axes" => "Axes",
            "polearm" or "polearms" or "atgeir" or "atgeirs" => "Polearms",
            "pickaxe" or "pickaxes" => "Pickaxes",
            "bow" or "bows" => "Bows",
            "crossbow" or "crossbows" => "Crossbows",
            "sneak" or "sneaking" => "Sneak",
            "block" or "blocking" => "Blocking",
            "bloodmagic" or "blood" => "BloodMagic",
            "elementalmagic" or "elemental" => "ElementalMagic",
            "woodcutting" or "woodcut" => "WoodCutting",
            "farming" => "Farming",
            "fishing" => "Fishing",
            _ => configuredSkill?.Trim() ?? ""
        };

        return Enum.TryParse(candidate, true, out skillType) &&
               skillType != Skills.SkillType.None;
    }

    private static string NormalizeSkillToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return value!.Trim()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .ToLowerInvariant();
    }
}
