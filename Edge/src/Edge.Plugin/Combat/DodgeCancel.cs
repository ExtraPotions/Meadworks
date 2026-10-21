// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original clean room implementation from the approved feature description and Valheim's native API/lifecycle inspection.
// No DodgeCancel source, assembly or assets were accessed. Native game code is not included.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Runtime.Quickstep;
using ExtraPotions.Edge.Runtime.Secondaries.Native;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Combat;

internal static class DodgeCancel
{
    internal static bool Enabled => Plugin.Ready && Plugin.Settings != null
        && Plugin.Settings.Get(EdgeSettings.General.Enabled) && Plugin.Settings.Get(EdgeSettings.General.DodgeCancel)
        && EdgeModules.ProductActive(out _);

    // Attacks whose native damage frame (OnAttackTrigger) has run. Each StartAttack clones its Attack, so identity is
    // per swing; entries go away with the abandoned attack.
    private static readonly ConditionalWeakTable<Attack, object> HitLanded = new();

    private static bool OnlyBeforeHit => Plugin.Settings!.Get(EdgeSettings.General.DodgeCancelOnlyBeforeHit);

    private static float ExtraStaminaCost
    {
        get
        {
            float cost = Plugin.Settings!.Get(EdgeSettings.General.DodgeCancelExtraStaminaCost);
            return float.IsNaN(cost) || cost <= 0f ? 0f : Mathf.Min(cost, 100f);
        }
    }

    internal static void OnAttackTrigger(Attack attack)
    {
        if (attack != null && !HitLanded.TryGetValue(attack, out _)) HitLanded.Add(attack, HitLanded);
    }

    private static bool CanCancel(Player player) => Enabled && player != null && player == Player.m_localPlayer
        && player.m_nview != null && player.m_nview.IsValid() && player.m_nview.IsOwner()
        && !player.IsDead() && !player.IsTeleporting() && !player.IsStaggering() && !player.IsEncumbered()
        && !player.InDodge() && !player.InMinorAction() && !player.InPlaceMode() && !player.IsAttached()
        && player.m_currentAttack != null && !player.m_currentAttack.IsDone() && player.InAttack()
        && !(OnlyBeforeHit && HitLanded.TryGetValue(player.m_currentAttack, out _));

    internal static void OnBlockInput(Player player, bool pressed)
    {
        // Read the native result, including Toggle Block. Releasing a toggle or holding an existing block is not a
        // new cancellation. A native queued dodge takes the separate affordability/ground path below.
        if (!pressed || player == null || !player.m_blocking || player.m_queuedDodgeTimer > 0f) return;
        float extra = Enabled ? ExtraStaminaCost : 0f;
        if (extra > 0f && !player.HaveStamina(extra)) return;
        if (TryCancel(player) && extra > 0f) player.UseStamina(extra);
    }

    internal static void BeforeDodge(Player player, float dt)
    {
        if (!CanCancel(player) || player.m_queuedDodgeTimer <= Mathf.Max(0f, dt) || !player.IsOnGround()) return;
        float extra = ExtraStaminaCost;
        if (!player.HaveStamina(QuickstepSystem.PendingDodgeCost(player) + extra)) return;
        if (TryCancel(player) && extra > 0f) player.UseStamina(extra);
        // The original Quickstep prefix or native UpdateDodge now consumes the original queue/direction and cost.
        // No injected dodge trigger, invulnerability or replacement input path; the only Edge debit is the optional
        // Extra Stamina Cost, paid once and only after the attack was actually cancelled.
    }

    private static bool TryCancel(Player player)
    {
        if (!CanCancel(player)) return false;
        var animator = player.m_animator;
        if (animator == null || player.m_zanim == null) return false;
        var exits = new List<(int Layer, int State)>();
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            var current = animator.GetCurrentAnimatorStateInfo(layer);
            var next = animator.GetNextAnimatorStateInfo(layer);
            if (current.tagHash != Humanoid.s_animatorTagAttack
                && (!animator.IsInTransition(layer) || next.tagHash != Humanoid.s_animatorTagAttack)) continue;
            // Never reset an unfamiliar animation controller. Only existing native locomotion/idle states can be
            // selected; unrelated layers and their weights are left alone.
            int state = Animator.StringToHash("Movement");
            if (!animator.HasState(layer, state)) state = Animator.StringToHash("Idle");
            if (!animator.HasState(layer, state)) return false;
            exits.Add((layer, state));
        }
        if (exits.Count == 0) return false;

        var attack = player.m_currentAttack;
        EdgeNativeOwnership.CancelAttack(attack);
        attack.Stop(); // source/native attachment, trail and already-paid resource cleanup; never refund costs
        player.m_currentAttack = null; // late native animation events cannot fire the abandoned attack
        player.m_previousAttack = null;
        player.m_currentAttackIsSecondary = false;
        player.m_queuedAttackTimer = 0f;
        player.m_queuedSecondAttackTimer = 0f;
        player.m_attack = player.m_attackHold = player.m_secondaryAttack = player.m_secondaryAttackHold = false;
        player.m_zanim.SetTrigger("attack_abort");
        foreach (var exit in exits) animator.Play(exit.State, exit.Layer, 0f);
        animator.Update(0f);
        player.m_cachedAnimHashFrame = -1;
        player.m_cachedFrame = -1; // Player caches InAttack within a native update; re-read the new animation state
        return true;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
internal static class DodgeCancelBlockInputPatch
{
    private static void Postfix(Player __instance, bool block)
    {
        try { DodgeCancel.OnBlockInput(__instance, block); }
        catch (Exception error) { Plugin.Log?.Debug("block cancellation refused: " + error.Message); }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.UpdateDodge))]
internal static class DodgeCancelQueuedDodgePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Player __instance, float dt)
    {
        try { DodgeCancel.BeforeDodge(__instance, dt); }
        catch (Exception error) { Plugin.Log?.Debug("dodge cancellation refused: " + error.Message); }
    }
}

[HarmonyPatch(typeof(Attack), nameof(Attack.OnAttackTrigger))]
internal static class DodgeCancelHitFramePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Attack __instance)
    {
        try { DodgeCancel.OnAttackTrigger(__instance); }
        catch (Exception error) { Plugin.Log?.Debug("dodge cancel hit frame not recorded: " + error.Message); }
    }
}
