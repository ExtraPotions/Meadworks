// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Runtime;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Ammunition of the local player's shot (Attack.UseAmmo, on the shooter's client, once per shot): Volley takes up
    /// to two more of the same stack and fires one volley arrow per arrow it got; Thrifty Quiver gives the whole
    /// shot's ammunition back on its chance. Consumable ammunition (the game eats it instead) is left alone.
    /// Writes are checked (playtest 2026-10-20): a volley fires only the arrows that actually left the stack, and a
    /// refund the inventory refuses (another mod holding it) is dropped at the shooter's feet instead of lost.
    /// </summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.UseAmmo))]
    internal static class AmmoPatch
    {
        private static void Postfix(Attack __instance, bool __result, ref ItemDrop.ItemData ammoItem)
        {
            if (!__result || !AttackClones.Is(__instance))
            {
                return;
            }
            if (ammoItem == null || ammoItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable)
            {
                // No second and third arrow to take: the shot flies alone rather than tripled for one.
                __instance.m_projectiles = AttackClones.Multi ? AttackClones.BaseProjectiles : __instance.m_projectiles;
                return;
            }
            Inventory inventory = __instance.m_character.GetInventory();
            int used = 1 + TakeVolley(__instance, inventory, ammoItem);
            ItemLocalSums? sums = ItemLocalCache.Get(__instance.m_weapon);
            if (sums != null && Random.value < sums.Get(EffectKind.AmmoSave))
            {
                Refund(inventory, ammoItem, used, SafeInventory.AtFeet(__instance.m_character));
            }
        }

        private static int TakeVolley(Attack attack, Inventory inventory, ItemDrop.ItemData ammo)
        {
            if (!AttackClones.Multi)
            {
                return 0;
            }
            int extra = SafeInventory.RemoveFrom(inventory, ammo, Mathf.Min(2, ammo.m_stack));
            attack.m_projectiles = AttackClones.BaseProjectiles * (1 + extra);
            return extra;
        }

        private static void Refund(Inventory inventory, ItemDrop.ItemData ammo, int count, Vector3 dropAt)
        {
            if (inventory.ContainsItem(ammo))
            {
                ammo.m_stack += count;
                inventory.Changed();
                return;
            }
            ammo.m_stack = count;
            SafeInventory.AddOrDrop(inventory, ammo, dropAt);
        }
    }

    /// <summary>
    /// True Flight: projectiles from this weapon leave X% faster. Projectile.Setup receives the final launch velocity
    /// and the weapon; it runs where the projectile is fired (the shooter's client).
    /// </summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
    internal static class ProjectileVelocityPatch
    {
        private static void Prefix(Character owner, ref Vector3 velocity, ItemDrop.ItemData item)
        {
            if (item == null || !ReferenceEquals(owner, Player.m_localPlayer))
            {
                return;
            }
            ItemLocalSums? sums = ItemLocalCache.Get(item);
            if (sums != null)
            {
                velocity *= 1f + sums.Get(EffectKind.ProjectileVelocity);
            }
        }
    }

    /// <summary>
    /// Swift String: this bow draws X% faster. The game's draw fraction is held time over the draw duration, clamped
    /// to 1; dividing the duration by 1 + X is the same as multiplying the fraction, clamped again. Read by the
    /// drawing player's own client (draw stamina, the draw bar, the shot's power).
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GetAttackDrawPercentage))]
    internal static class DrawSpeedPatch
    {
        private static void Postfix(Humanoid __instance, ref float __result)
        {
            if (__result <= 0f || __result >= 1f)
            {
                return;
            }
            ItemLocalSums? sums = ItemLocalCache.Get(__instance.GetCurrentWeapon());
            if (sums != null)
            {
                __result = Mathf.Min(1f, __result * (1f + sums.Get(EffectKind.DrawSpeed)));
            }
        }
    }

    /// <summary>
    /// Skirmisher: the movement slowdown of this weapon's attack (the game multiplies movement by the attack's speed
    /// factor while it plays: the bow's shot, the crossbow's aim and fire) is X% smaller. Local player's movement,
    /// its own client.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GetAttackSpeedFactorMovement))]
    internal static class DrawMovePatch
    {
        private static void Postfix(Humanoid __instance, ref float __result)
        {
            if (__result >= 1f || !ReferenceEquals(__instance, Player.m_localPlayer))
            {
                return;
            }
            ItemLocalSums? sums = ItemLocalCache.Get(__instance.GetCurrentWeapon());
            if (sums != null)
            {
                __result = 1f - (1f - __result) * Mathf.Max(0f, 1f - sums.Get(EffectKind.DrawMovePenalty));
            }
        }
    }
}
