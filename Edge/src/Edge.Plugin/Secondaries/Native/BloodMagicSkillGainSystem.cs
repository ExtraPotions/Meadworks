// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8.
// Changed by expDARE, 2026: product settings, namespace and shared presentation boundaries; source algorithms retained.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Integration;
using ExtraPotions.Edge.Secondaries.Adopted;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;

internal static class BloodMagicSkillGainSystem
{
    private static float GetHealthCostSkillRaiseFactor()
    {
        return Mathf.Max(0f, EdgeModules.Active(EdgeModule.Secondaries) ? (Plugin.Settings?.Get(EdgeSettings.BloodMagic.HealthCostSkillRaiseFactor) ?? 0f) : 0f);
    }

    internal static bool IsHealthCostSkillGainEnabled()
    {
        return GetHealthCostSkillRaiseFactor() > 0f;
    }

    internal static bool ShouldUseMaxHealthForPercentageCost()
    {
        return EdgeModules.Active(EdgeModule.Secondaries) && (Plugin.Settings?.Get(EdgeSettings.BloodMagic.HealthCostUsesMaxHealth) ?? false);
    }

    internal static void ApplyMaxHealthPercentageCost(Attack attack, ref float healthCost)
    {
        if (!ShouldUseMaxHealthForPercentageCost() ||
            attack == null ||
            attack.m_character == null ||
            attack.m_weapon?.m_shared.m_skillType != Skills.SkillType.BloodMagic ||
            attack.m_attackHealthPercentage <= 0f)
        {
            return;
        }

        float baseCost = Mathf.Max(0f, attack.m_attackHealth) +
                         Mathf.Max(0f, attack.m_character.GetMaxHealth()) * Mathf.Max(0f, attack.m_attackHealthPercentage) / 100f;
        if (baseCost <= 0f)
        {
            healthCost = 0f;
            return;
        }

        float skillFactor = Mathf.Clamp01(attack.m_character.GetSkillFactor(Skills.SkillType.BloodMagic));
        healthCost = baseCost - baseCost * 0.33f * skillFactor;
    }

    internal static void TryGrantForHealthUse(Character character, float previousHealth)
    {
        if (!IsHealthCostSkillGainEnabled() || character is not Player player || character is not Humanoid humanoid)
        {
            return;
        }

        float consumedHealth = previousHealth - character.GetHealth();
        if (consumedHealth <= 0.001f)
        {
            return;
        }

        Attack? attack = humanoid.m_currentAttack;
        if (attack == null || attack.m_character != humanoid || attack.m_weapon?.m_shared.m_skillType != Skills.SkillType.BloodMagic)
        {
            return;
        }

        float factor = GetHealthCostSkillRaiseFactor();
        float raiseAmount = consumedHealth * factor;
        if (raiseAmount <= 0f)
        {
            return;
        }

        player.RaiseSkill(Skills.SkillType.BloodMagic, raiseAmount);
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.UseHealth))]
internal static class CharacterUseHealthBloodMagicSkillGainPatch
{
    private static void Prefix(Character __instance, out float __state)
    {
        __state = __instance != null ? __instance.GetHealth() : 0f;
    }

    private static void Postfix(Character __instance, float __state)
    {
        if (__instance != null)
        {
            BloodMagicSkillGainSystem.TryGrantForHealthUse(__instance, __state);
        }
    }
}

[HarmonyPatch(typeof(Attack), nameof(Attack.GetAttackHealth))]
internal static class AttackGetAttackHealthBloodMagicCostPatch
{
    private static void Postfix(Attack __instance, ref float __result)
    {
        BloodMagicSkillGainSystem.ApplyMaxHealthPercentageCost(__instance, ref __result);
    }
}
