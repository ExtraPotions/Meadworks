// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackRuntimeWeaponRebind.cs. Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace and existing snapshot boundaries; native algorithms retained.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries.Adopted;

internal static class SecondaryAttackRuntimeWeaponRebind
{
    private static readonly ConditionalWeakTable<ItemDrop.ItemData, RuntimeWeaponDefinitionState> WeaponStates = new();

    public static void Apply(ItemDrop.ItemData? weapon, SecondaryAttackAppliedWorldSnapshot appliedWorldSnapshot)
    {
        if (weapon?.m_dropPrefab == null)
        {
            return;
        }

        int currentApplyRevision = appliedWorldSnapshot.ApplyRevision;
        RuntimeWeaponDefinitionState state = WeaponStates.GetValue(weapon, _ => new RuntimeWeaponDefinitionState());
        if (state.AppliedWorldRevision == currentApplyRevision)
        {
            return;
        }

        ItemDrop? prefabItemDrop = weapon.m_dropPrefab.GetComponent<ItemDrop>();
        if (prefabItemDrop == null)
        {
            return;
        }

        if (appliedWorldSnapshot.DefinitionsByPrefabName.TryGetValue(weapon.m_dropPrefab.name, out SecondaryAttackDefinition definition) &&
            definition.AppliesSecondaryOverride &&
            ObjectDB.instance != null)
        {
            if (ProjectilePresetCooldownPolicy.UsesDynamicOriginalSecondary(definition))
            {
                weapon.m_shared.m_secondaryAttack = SecondaryAttackManager.CloneAttack(
                    definition.CooldownFallbackSecondaryAttack ?? prefabItemDrop.m_itemData?.m_shared?.m_secondaryAttack);
            }
            else if (definition.ConfiguredSecondaryAttack != null)
            {
                weapon.m_shared.m_secondaryAttack = SecondaryAttackManager.CloneAttack(definition.ConfiguredSecondaryAttack);
            }
            else
            {
                Attack sourceAttack = SecondaryAttackManager.ResolveSourceAttack(ObjectDB.instance, prefabItemDrop, definition);
                Attack configuredSecondaryAttack = SecondaryAttackManager.BuildSecondaryAttack(sourceAttack, definition);
                SecondaryAttackManager.NormalizeCopiedProjectileAim(configuredSecondaryAttack, definition);
                weapon.m_shared.m_secondaryAttack = configuredSecondaryAttack;
            }
        }
        else
        {
            weapon.m_shared.m_secondaryAttack = SecondaryAttackManager.CloneAttack(prefabItemDrop.m_itemData?.m_shared?.m_secondaryAttack);
        }

        state.AppliedWorldRevision = currentApplyRevision;
    }

    public static void RefreshLocalPlayerInventory(SecondaryAttackAppliedWorldSnapshot appliedWorldSnapshot)
    {
        Player? localPlayer = Player.m_localPlayer;
        Inventory? inventory = localPlayer?.GetInventory();
        if (inventory == null)
        {
            return;
        }

        foreach (ItemDrop.ItemData item in inventory.GetAllItems())
        {
            Apply(item, appliedWorldSnapshot);
        }
    }

    private sealed class RuntimeWeaponDefinitionState
    {
        internal int AppliedWorldRevision = -1;
    }
}
