// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackManager.CommonRuntime.cs (reload, authority and safe hit support subset). Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespaces, canonical catalogs, Crucible settings and diagnostics;
// native source algorithms retained. Candidate stage, dispatch and prefab application are a separate unit.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Edge.Secondaries.Adopted;
using ExtraPotions.Edge.Config;
internal static partial class SecondaryAttackManager
{
    private const string ReloadLoadedCustomDataKey = "SecondaryAttacks.ReloadLoaded";

    private static readonly Dictionary<Humanoid, ItemDrop.ItemData> ReloadConsumptionWeapons = new();

    internal static void TryRestorePersistedReloadedWeaponState(Player player, ItemDrop.ItemData weapon)
    {
        if (player == null || weapon == null || !IsReloadableWeapon(weapon))
        {
            return;
        }

        if (player.m_weaponLoaded == weapon || !weapon.m_customData.TryGetValue(ReloadLoadedCustomDataKey, out string value) || value != "1")
        {
            return;
        }

        player.SetWeaponLoaded(weapon);
    }

    internal static void OnWeaponLoadedStateChanged(Player player, ItemDrop.ItemData? previousWeapon, ItemDrop.ItemData? newWeapon)
    {
        if (player == null)
        {
            return;
        }

        if (newWeapon != null)
        {
            SetPersistedReloadState(player, newWeapon, loaded: true);
            return;
        }

        if (previousWeapon == null)
        {
            return;
        }

        bool consumed = IsReloadStateConsumptionActive(player, previousWeapon);
        if (consumed)
        {
            SetPersistedReloadState(player, previousWeapon, loaded: false);
        }
        else
        {
            SetPersistedReloadState(player, previousWeapon, loaded: true);
        }
    }

    internal static void ConsumePersistedReloadedWeaponState(Player player, ItemDrop.ItemData weapon)
    {
        SetPersistedReloadState(player, weapon, loaded: false);
    }

    internal static bool TryPrepareReloadSecondaryResourceCost(
        Humanoid humanoid,
        ItemDrop.ItemData weapon,
        out ReloadSecondaryResourceCostContext context)
    {
        context = ReloadSecondaryResourceCostContext.Empty;
        if (weapon == null || !SecondaryAttackRuntimeFacade.TryGetDefinition(weapon, out SecondaryAttackDefinition definition))
        {
            return true;
        }

        if (definition.BehaviorType != SecondaryAttackBehaviorType.Projectile)
        {
            return true;
        }

        Attack primaryAttack = weapon.m_shared.m_attack;
        if (!primaryAttack.m_requiresReload || !IsCurrentReloadWeaponLoaded(humanoid, weapon))
        {
            return true;
        }

        float multiplierDelta = Mathf.Max(0f, definition.ResourceMultiplier) - 1f;
        if (Mathf.Approximately(multiplierDelta, 0f))
        {
            return true;
        }

        float reloadTime = Mathf.Max(0f, weapon.GetWeaponLoadingTime());
        if (reloadTime <= 0f)
        {
            return true;
        }

        float staminaDelta = Mathf.Max(0f, primaryAttack.m_reloadStaminaDrain) * reloadTime * multiplierDelta;
        float eitrDelta = Mathf.Max(0f, primaryAttack.m_reloadEitrDrain) * reloadTime * multiplierDelta;
        if (staminaDelta > 0f && !humanoid.HaveStamina(staminaDelta))
        {
            if (humanoid.IsPlayer())
            {
                Hud.instance?.StaminaBarEmptyFlash();
            }

            return false;
        }

        if (eitrDelta > 0f && !humanoid.TryUseEitr(eitrDelta))
        {
            return false;
        }

        context = new ReloadSecondaryResourceCostContext(staminaDelta, eitrDelta);
        return true;
    }

    private static bool IsCurrentReloadWeaponLoaded(Humanoid humanoid, ItemDrop.ItemData weapon)
    {
        if (humanoid is Player player)
        {
            return player.m_weaponLoaded == weapon;
        }

        return humanoid.IsWeaponLoaded();
    }

    internal static void ApplyReloadSecondaryResourceCost(Humanoid humanoid, ReloadSecondaryResourceCostContext? context)
    {
        if (context == null || !context.HasDelta)
        {
            return;
        }

        if (context.StaminaDelta > 0f)
        {
            humanoid.UseStamina(context.StaminaDelta);
        }
        else if (context.StaminaDelta < 0f)
        {
            humanoid.AddStamina(-context.StaminaDelta);
        }

        if (context.EitrDelta > 0f)
        {
            humanoid.UseEitr(context.EitrDelta);
        }
        else if (context.EitrDelta < 0f)
        {
            humanoid.AddEitr(-context.EitrDelta);
        }
    }

    internal static ReloadStateConsumptionScope BeginReloadStateConsumption(Attack attack)
    {
        if (attack?.m_character == null || attack.m_weapon == null || !attack.m_requiresReload)
        {
            return default;
        }

        Humanoid character = attack.m_character;
        bool hadPreviousWeapon = ReloadConsumptionWeapons.TryGetValue(
            character,
            out ItemDrop.ItemData? previousWeapon);
        ReloadConsumptionWeapons[character] = attack.m_weapon;
        return new ReloadStateConsumptionScope(character, previousWeapon, hadPreviousWeapon);
    }

    internal static void EndReloadStateConsumption(ref ReloadStateConsumptionScope scope)
    {
        if (!scope.Active)
        {
            scope = default;
            return;
        }

        Humanoid? character = scope.Character;
        if (!ReferenceEquals(character, null))
        {
            if (scope.HadPreviousWeapon && scope.PreviousWeapon != null)
            {
                ReloadConsumptionWeapons[character] = scope.PreviousWeapon;
            }
            else
            {
                ReloadConsumptionWeapons.Remove(character);
            }
        }

        scope = default;
    }

    private static bool IsReloadableWeapon(ItemDrop.ItemData weapon)
    {
        return weapon?.m_shared?.m_attack?.m_requiresReload == true;
    }

    private static bool IsReloadStateConsumptionActive(Humanoid humanoid, ItemDrop.ItemData weapon)
    {
        return ReloadConsumptionWeapons.TryGetValue(humanoid, out ItemDrop.ItemData consumedWeapon) && consumedWeapon == weapon;
    }

    internal readonly struct ReloadStateConsumptionScope
    {
        internal ReloadStateConsumptionScope(
            Humanoid character,
            ItemDrop.ItemData? previousWeapon,
            bool hadPreviousWeapon)
        {
            Character = character;
            PreviousWeapon = previousWeapon;
            HadPreviousWeapon = hadPreviousWeapon;
            Active = true;
        }

        internal Humanoid? Character { get; }

        internal ItemDrop.ItemData? PreviousWeapon { get; }

        internal bool HadPreviousWeapon { get; }

        internal bool Active { get; }
    }

    private static void SetPersistedReloadState(Player player, ItemDrop.ItemData weapon, bool loaded)
    {
        if (!IsReloadableWeapon(weapon))
        {
            return;
        }

        bool changed;
        if (loaded)
        {
            changed = !weapon.m_customData.TryGetValue(ReloadLoadedCustomDataKey, out string value) || value != "1";
            weapon.m_customData[ReloadLoadedCustomDataKey] = "1";
        }
        else
        {
            changed = weapon.m_customData.Remove(ReloadLoadedCustomDataKey);
        }

        if (changed)
        {
            player.GetInventory()?.Changed();
        }
    }

    internal static void NormalizeCopiedProjectileAim(Attack? attack, SecondaryAttackDefinition? definition)
    {
        if (attack == null ||
            definition == null ||
            definition.Behavior is not CopiedSecondaryBehavior ||
            definition.OnProjectileHit == null && definition.Boomerang == null ||
            attack.m_attackType != Attack.AttackType.Projectile && attack.m_attackProjectile == null)
        {
            return;
        }

        attack.m_useCharacterFacing = false;
        attack.m_useCharacterFacingYAim = false;
    }

    internal static bool HasCharacterAuthority(Character? character)
    {
        return TryGetCharacterZdo(character, out ZNetView? nview, out _) && nview!.IsOwner();
    }

    internal static bool TryGetCharacterZdo(Character? character, out ZNetView? nview, out ZDO? zdo)
    {
        nview = character != null ? character.GetComponent<ZNetView>() : null;
        zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
        return nview != null && nview.IsValid() && zdo != null;
    }

    internal static double GetNetworkTimeSeconds()
    {
        return ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : Time.timeAsDouble;
    }

    internal static Vector3 ResolveSafeClosestPoint(Collider collider, Vector3 origin)
    {
        if (collider is MeshCollider meshCollider && !meshCollider.convex)
        {
#pragma warning disable CS0618
            return meshCollider.ClosestPointOnBounds(origin);
#pragma warning restore CS0618
        }

        return collider.ClosestPoint(origin);
    }

    internal static bool IsEnemyOrAggravatableTarget(Character attacker, Character target)
    {
        return BaseAI.IsEnemy(attacker, target) ||
               (target.GetBaseAI() != null && target.GetBaseAI().IsAggravatable() && attacker.IsPlayer());
    }

    internal sealed class ReloadSecondaryResourceCostContext
    {
        public static readonly ReloadSecondaryResourceCostContext Empty = new(0f, 0f);

        public ReloadSecondaryResourceCostContext(float staminaDelta, float eitrDelta)
        {
            StaminaDelta = staminaDelta;
            EitrDelta = eitrDelta;
        }

        public float StaminaDelta { get; }

        public float EitrDelta { get; }

        public bool HasDelta => Math.Abs(StaminaDelta) > 0.001f || Math.Abs(EitrDelta) > 0.001f;
    }

}
