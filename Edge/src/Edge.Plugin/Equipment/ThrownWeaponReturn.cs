// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Edge.Config;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Equipment;

/// <summary>
/// A thrown weapon that is not used up comes back to the thrower's inventory, in the slot it left. A throw carries the
/// very item that left the inventory (<c>Projectile.m_spawnItem</c>, set on the thrower's client only); the projectile's
/// owner calls SpawnOnHit when it lands, and the game would drop that item there. Taking it back before the drop keeps
/// its data and never makes a second copy. Bombs and other consumed throws carry no item; stackable items are left to
/// the game. With no room the item falls where it landed, as before.
/// </summary>
internal static class ThrownWeaponReturn
{
    /// <summary>Edge setting <c>5 - Equipment / Return Thrown Weapons</c>, the whole-product switch and the session gate.</summary>
    internal static bool Enabled => Plugin.Ready && Plugin.Settings != null
        && Plugin.Settings.Get(EdgeSettings.General.Enabled) && Plugin.Settings.Get(EdgeSettings.Equipment.ReturnThrownWeapons)
        && EdgeModules.ProductActive(out _);

    internal static bool Returnable(ItemDrop.ItemData? item) =>
        item?.m_shared != null && item.m_shared.m_maxStackSize <= 1
        && item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable
        && item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo;

    /// <summary>Puts <paramref name="item"/> back in its previous slot, else the first free one; false when there is no room.</summary>
    internal static bool PutBack(Inventory inventory, ItemDrop.ItemData item)
    {
        if (inventory.ContainsItem(item)) return true;
        item.m_equipped = false;
        Vector2i slot = item.m_gridPos;
        bool inside = slot.x >= 0 && slot.y >= 0 && slot.x < inventory.GetWidth() && slot.y < inventory.GetHeight();
        if (inside && inventory.GetItemAt(slot.x, slot.y) == null && inventory.AddItem(item, slot)) return true;
        return inventory.HaveEmptySlot() && inventory.AddItem(item);
    }

    internal static Player? LocalThrower(Projectile projectile) =>
        projectile.m_owner is Player player && ReferenceEquals(player, Player.m_localPlayer) && !player.IsDead() ? player : null;
}

[HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
internal static class ThrownWeaponReturnSetupPatch
{
    // A throw that never hits anything still lands (and returns) when its time runs out instead of vanishing.
    private static void Postfix(Projectile __instance)
    {
        if (__instance.m_spawnItem != null && ThrownWeaponReturn.Returnable(__instance.m_spawnItem)
            && ThrownWeaponReturn.LocalThrower(__instance) != null && ThrownWeaponReturn.Enabled)
            __instance.m_spawnOnTtl = true;
    }
}

/// <summary>Runs after other prefixes (Inscribe's Returning and Bifrost Step) so their effects keep priority.</summary>
[HarmonyPatch(typeof(Projectile), nameof(Projectile.SpawnOnHit))]
internal static class ThrownWeaponReturnLandingPatch
{
    [HarmonyPriority(Priority.Low)]
    private static void Prefix(Projectile __instance)
    {
        var item = __instance.m_spawnItem;
        if (item == null || !ThrownWeaponReturn.Returnable(item)) return;
        var thrower = ThrownWeaponReturn.LocalThrower(__instance);
        if (thrower == null || !ThrownWeaponReturn.Enabled) return;
        var inventory = thrower.GetInventory();
        if (inventory != null && ThrownWeaponReturn.PutBack(inventory, item)) __instance.m_spawnItem = null;
    }
}
