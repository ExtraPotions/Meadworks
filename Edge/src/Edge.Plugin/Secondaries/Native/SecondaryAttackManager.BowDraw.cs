// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackManager.cs and SecondaryAttackManager.CommonRuntime.cs (bow draw subset). Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespaces, applied-definition gate and diagnostics;
// ranged-only source subset, unrelated melee/blood execution remains pending.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using UnityEngine;
namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Edge.Secondaries.Adopted;
internal static partial class SecondaryAttackManager
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Player, BowSecondaryState> BowSecondaryStates = new();
    public static void UpdateCustomBowDraw(
        Player player,
        ItemDrop.ItemData weapon,
        float dt,
        ref float attackDrawTime,
        bool blocking,
        bool attackHold,
        bool secondaryAttackHold,
        bool secondaryAttackPressed,
        ZSyncAnimation zanim,
        SEMan seman)
    {
        BowSecondaryState state = BowSecondaryStates.GetValue(player, _ => new BowSecondaryState());
        string currentPrefabName = weapon.m_dropPrefab != null ? weapon.m_dropPrefab.name : "";
        if (!string.Equals(state.PrefabName, currentPrefabName, StringComparison.Ordinal))
        {
            state.PrefabName = currentPrefabName;
            state.PendingSecondary = false;
        }

        if (!SecondaryAttackRuntimeFacade.TryGetDefinition(weapon, out SecondaryAttackDefinition definition))
        {
            state.PendingSecondary = false;
            return;
        }

        bool drawHeld = attackHold;
        float drawStaminaDrain = GetSkillAdjustedDrawCost(player, weapon, weapon.m_shared.m_attack.m_drawStaminaDrain);
        float drawEitrDrain = weapon.m_shared.m_attack.m_drawEitrDrain;
        bool hasStamina = drawStaminaDrain <= 0f || player.HaveStamina();
        bool hasEitr = drawEitrDrain <= 0f || player.HaveEitr();

        if (blocking || player.InMinorAction() || player.IsAttached())
        {
            attackDrawTime = -1f;
            state.PendingSecondary = false;
            if (!string.IsNullOrEmpty(weapon.m_shared.m_attack.m_drawAnimationState))
            {
                zanim.SetBool(weapon.m_shared.m_attack.m_drawAnimationState, value: false);
            }

            return;
        }

        if (drawHeld && attackDrawTime == 0f)
        {
            state.PendingSecondary = false;
        }
        else if (secondaryAttackPressed && drawHeld && attackDrawTime > 0f)
        {
            state.PendingSecondary = true;
            RangedSecondaryCooldownSystem.CanStart(player, weapon);
        }

        if (attackDrawTime < 0f)
        {
            if (!drawHeld)
            {
                attackDrawTime = 0f;
            }

            return;
        }

        if (drawHeld && hasStamina && hasEitr && attackDrawTime >= 0f)
        {
            if (attackDrawTime == 0f)
            {
                if (!weapon.m_shared.m_attack.StartDraw(player, weapon))
                {
                    attackDrawTime = -1f;
                    state.PendingSecondary = false;
                    return;
                }

                weapon.m_shared.m_holdStartEffect.Create(player.transform.position, Quaternion.identity, player.transform);
            }

            attackDrawTime += Time.fixedDeltaTime;
            if (!string.IsNullOrEmpty(weapon.m_shared.m_attack.m_drawAnimationState))
            {
                zanim.SetBool(weapon.m_shared.m_attack.m_drawAnimationState, value: true);
                zanim.SetFloat("drawpercent", player.GetAttackDrawPercentage());
            }

            player.UseStamina(drawStaminaDrain * dt);
            player.UseEitr(drawEitrDrain * dt);
            return;
        }

        if (attackDrawTime > 0f)
        {
            if (hasStamina && hasEitr)
            {
                bool pendingSecondary = state.PendingSecondary;
                float extraStaminaCost = 0f;
                if (!pendingSecondary || CanPayBowSecondaryReleaseExtraStamina(player, weapon, definition, drawStaminaDrain, attackDrawTime, out extraStaminaCost))
                {
                    bool started = player.StartAttack(null, pendingSecondary);
                    if (started && pendingSecondary && extraStaminaCost > 0f)
                    {
                        player.UseStamina(extraStaminaCost);
                    }
                }
                else
                {
                    Hud.instance?.StaminaBarEmptyFlash();
                }
            }

            if (!string.IsNullOrEmpty(weapon.m_shared.m_attack.m_drawAnimationState))
            {
                zanim.SetBool(weapon.m_shared.m_attack.m_drawAnimationState, value: false);
            }

            attackDrawTime = 0f;
            state.PendingSecondary = false;
        }
    }

    private static bool CanPayBowSecondaryReleaseExtraStamina(
        Player player,
        ItemDrop.ItemData weapon,
        SecondaryAttackDefinition definition,
        float drawStaminaDrain,
        float attackDrawTime,
        out float extraStaminaCost)
    {
        extraStaminaCost = 0f;
        if (weapon.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Bow || !weapon.m_shared.m_attack.m_bowDraw)
        {
            return true;
        }

        float resourceMultiplier = Mathf.Max(0f, definition.ResourceMultiplier);
        if (resourceMultiplier <= 1f || drawStaminaDrain <= 0f)
        {
            return true;
        }

        float fullChargeTime = GetSkillAdjustedFullDrawTime(player, weapon);
        float chargedTime = Mathf.Min(Mathf.Max(0f, attackDrawTime), fullChargeTime);
        extraStaminaCost = drawStaminaDrain * chargedTime * (resourceMultiplier - 1f);
        return extraStaminaCost <= 0f || player.HaveStamina(extraStaminaCost);
    }

    private static float GetSkillAdjustedFullDrawTime(Player player, ItemDrop.ItemData weapon)
    {
        float baseFullChargeTime = Mathf.Max(0f, weapon.m_shared.m_attack.m_drawDurationMin);
        if (baseFullChargeTime <= 0f)
        {
            return 0f;
        }

        float skillFactor = player.GetSkillFactor(weapon.m_shared.m_skillType);
        return Mathf.Lerp(baseFullChargeTime, baseFullChargeTime * 0.2f, skillFactor);
    }

    private static float GetSkillAdjustedDrawCost(Player player, ItemDrop.ItemData weapon, float rawDrawCost)
    {
        if (rawDrawCost <= 0f)
        {
            return 0f;
        }

        float skillFactor = player.GetSkillFactor(weapon.m_shared.m_skillType);
        return rawDrawCost - rawDrawCost * 0.33f * skillFactor;
    }

    private sealed class BowSecondaryState
    {
        public string PrefabName { get; set; } = "";

        public bool PendingSecondary { get; set; }
    }

}
