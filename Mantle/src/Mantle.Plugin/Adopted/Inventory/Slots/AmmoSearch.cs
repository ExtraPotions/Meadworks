using System.Collections.Generic;
using HarmonyLib;
using Mantle.Core;
using Mantle.Tackle;

using ItemType = ItemDrop.ItemData.ItemType;

namespace Mantle.Slots
{
    /// <summary>
    /// Which ammo the game takes when it has to choose. It searches only when no ammo is equipped, or the equipped stack
    /// is gone or does not fit the weapon (<c>Attack.StartDraw</c> and <c>Start</c> through <c>HaveAmmo</c> and
    /// <c>EquipAmmoItem</c>, which equips what it finds; <c>UseAmmo</c>), so ammo the player equipped is used until it runs
    /// out. The game takes the first match in cell order, which puts any stray stack in the grid ahead of the slots under
    /// it. For the player's own inventory the answer is the first match in the tacklebox's cells (the rod's bait), then in
    /// the Ammo slots left to right (the user asked for arrows to follow the slots, 2026-09-28), and only then the game's
    /// own search.
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetAmmoItem))]
    public static class AmmoSearch
    {
        [HarmonyPrefix]
        public static bool Prefix(Inventory __instance, string ammoName, string matchPrefabName, ref ItemDrop.ItemData __result)
        {
            if (!InventoryState.Manages(__instance))
                return true;
            ItemDrop.ItemData found = Tacklebox.Active ? First(__instance, InventoryState.CellsOf(SlotKind.Tackle), ammoName, matchPrefabName) : null;
            found = found ?? First(__instance, InventoryState.CellsOf(SlotKind.Ammo), ammoName, matchPrefabName);
            if (found == null)
                return true;
            __result = found;
            return false;
        }

        private static ItemDrop.ItemData First(Inventory inventory, IReadOnlyList<Vector2i> cells, string ammoName, string prefab)
        {
            foreach (Vector2i cell in cells)
            {
                ItemDrop.ItemData item = inventory.GetItemAt(cell.x, cell.y);
                if (item != null && Fits(item, ammoName, prefab))
                    return item;
            }
            return null;
        }

        /// <summary>The game's own test in GetAmmoItem.</summary>
        private static bool Fits(ItemDrop.ItemData item, string ammoName, string prefab)
        {
            ItemType type = item.m_shared.m_itemType;
            bool ammo = type == ItemType.Ammo || type == ItemType.AmmoNonEquipable || type == ItemType.Consumable;
            return ammo && item.m_shared.m_ammoType == ammoName && (prefab == null || (item.m_dropPrefab != null && item.m_dropPrefab.name == prefab));
        }
    }
}
