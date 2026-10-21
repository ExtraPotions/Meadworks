// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackManager.CommonRuntime.cs (native durability and effects subset). Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace, diagnostic and native boundary seams; source algorithms retained.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
// Asks the wear-exempt capability (meadworks.items.wear_exempt) before the durability debit.
using System;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries.Adopted;
using ExtraPotions.Edge.Runtime.Quickstep;
internal static partial class SecondaryAttackManager
{
    internal static void PlayTriggeredAttackEffects(Attack attack, float durabilityFactor)
    {
        DrainAttackDurability(attack, durabilityFactor);

        Transform origin = attack.m_character.transform;
        attack.m_weapon.m_shared.m_triggerEffect.Create(origin.position, attack.m_character.transform.rotation, origin);
        attack.m_triggerEffect.Create(origin.position, attack.m_character.transform.rotation, origin);
        attack.m_character.AddNoise(attack.m_attackHitNoise);
    }

    internal static void DrainAttackDurability(Attack attack, float durabilityFactor)
    {
        if (attack?.m_weapon == null || attack.m_character == null)
        {
            return;
        }

        DrainItemDurability(attack.m_character, attack.m_weapon, durabilityFactor);
    }

    internal static void DrainItemDurability(Character character, ItemDrop.ItemData weapon, float durabilityFactor)
    {
        if (character == null ||
            weapon?.m_shared == null ||
            !weapon.m_shared.m_useDurability ||
            !character.IsPlayer() ||
            WearExempt(character, weapon))
        {
            return;
        }

        float drain = GetItemDurabilityDrain(weapon) * Mathf.Max(0f, durabilityFactor) * Game.m_durabilityRate;
        if (drain <= 0f)
        {
            return;
        }

        weapon.m_durability = Mathf.Max(0f, weapon.m_durability - drain);
    }

    private static readonly CapabilityLink<Func<object, object, bool>> wearExempt =
        new(WearExemptContract.Id, WearExemptContract.ContractVersion);

    // Another product (Hex's "disable durability loss") may exempt this character's item from wear; none installed, none exempt.
    private static bool WearExempt(Character character, ItemDrop.ItemData weapon) =>
        CruciblePlugin.IsReady && wearExempt.TryGet(CruciblePlugin.Services.Capabilities, out var provider)
        && WearExemptContract.Exempt(provider, character, weapon);

    internal static float GetItemDurabilityDrain(ItemDrop.ItemData weapon)
    {
        float drain = weapon?.m_shared?.m_useDurabilityDrain ?? 0f;
        return drain > 0f ? drain : 1f;
    }

    internal static SecondaryAttackDurabilityAdjustmentState BeginSecondaryAttackDurabilityAdjustment(Attack attack)
    {
        if (attack?.m_weapon?.m_shared == null ||
            attack.m_character == null ||
            !attack.m_weapon.m_shared.m_useDurability ||
            !attack.m_character.IsPlayer() ||
            !SecondaryAttackRuntimeContext.TryGetActiveAttack(attack, out ActiveSecondaryAttack? activeAttack) ||
            activeAttack == null)
        {
            return SecondaryAttackDurabilityAdjustmentState.Empty;
        }

        float factor = Mathf.Max(0f, activeAttack.Definition.DurabilityFactor);
        if (Mathf.Approximately(factor, 1f))
        {
            return SecondaryAttackDurabilityAdjustmentState.Empty;
        }

        return new SecondaryAttackDurabilityAdjustmentState(attack.m_weapon, attack.m_weapon.m_durability, factor);
    }

    internal static void EndSecondaryAttackDurabilityAdjustment(SecondaryAttackDurabilityAdjustmentState state)
    {
        if (!state.Applies || state.Weapon?.m_shared == null)
        {
            return;
        }

        float actualDrain = state.BeforeDurability - state.Weapon.m_durability;
        if (actualDrain <= 0.001f)
        {
            return;
        }

        float targetDrain = actualDrain * state.Factor;
        state.Weapon.m_durability = Mathf.Clamp(
            state.BeforeDurability - targetDrain,
            0f,
            Mathf.Max(state.Weapon.m_shared.m_maxDurability, state.BeforeDurability));
    }

    internal static void EndSecondaryAttackDurabilityAdjustment(ref SecondaryAttackDurabilityAdjustmentState state)
    {
        EndSecondaryAttackDurabilityAdjustment(state);
        state = SecondaryAttackDurabilityAdjustmentState.Empty;
    }

    internal readonly struct SecondaryAttackDurabilityAdjustmentState
    {
        internal static readonly SecondaryAttackDurabilityAdjustmentState Empty = new(null, 0f, 1f);

        internal SecondaryAttackDurabilityAdjustmentState(ItemDrop.ItemData? weapon, float beforeDurability, float factor)
        {
            Weapon = weapon;
            BeforeDurability = beforeDurability;
            Factor = factor;
        }

        internal ItemDrop.ItemData? Weapon { get; }

        internal float BeforeDurability { get; }

        internal float Factor { get; }

        internal bool Applies => Weapon != null;
    }

    internal static void EnsureRuntimeWeaponDefinitionApplied(ItemDrop.ItemData? weapon) => SecondaryAttackRuntimeWeaponRebind.Apply(weapon, EdgeNativeDefinitions.AppliedSnapshot);
    internal static void RefreshLocalPlayerRuntimeWeaponDefinitions() => SecondaryAttackRuntimeWeaponRebind.RefreshLocalPlayerInventory(EdgeNativeDefinitions.AppliedSnapshot);
    internal static bool TryGetShieldRemaining(
        Character character,
        int preferredStatusEffectHash,
        out SE_Shield? shield,
        out float remaining,
        out float remainingTime)
    {
        shield = null;
        remaining = 0f;
        remainingTime = 0f;
        SEMan? seMan = character?.GetSEMan();
        if (seMan == null)
        {
            return false;
        }

        if (preferredStatusEffectHash != 0 && seMan.GetStatusEffect(preferredStatusEffectHash) is SE_Shield preferredShield &&
            ShieldAccess.TryReadRemaining(preferredShield, out remaining, out remainingTime))
        {
            shield = preferredShield;
            return true;
        }

        foreach (StatusEffect statusEffect in seMan.GetStatusEffects())
        {
            if (statusEffect is not SE_Shield candidateShield)
            {
                continue;
            }

            if (!ShieldAccess.TryReadRemaining(candidateShield, out remaining, out remainingTime))
            {
                continue;
            }

            shield = candidateShield;
            return true;
        }

        return false;
    }

}
