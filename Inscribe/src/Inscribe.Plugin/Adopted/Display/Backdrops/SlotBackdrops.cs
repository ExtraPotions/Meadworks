// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Display.Backdrops
{
    /// <summary>
    /// Backdrops in the item cells: every inventory grid (the player's with its slots, a container's, any other mod's)
    /// and the hotbar. Both run after the game's own per-frame icon update, while they are shown: the cells holding a
    /// magic item get their backdrop, emptied cells lose it.
    /// </summary>
    internal static class SlotBackdrops
    {
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
        internal static class GridPatch
        {
            [HarmonyPostfix]
            private static void Postfix(InventoryGrid __instance)
            {
                Inventory? inventory = __instance.GetInventory();
                if (inventory == null)
                {
                    foreach (InventoryElement element in __instance.m_elements)
                        if (element != null) IconBackdrop.Hide(element.m_icon);
                    return;
                }
                int width = inventory.GetWidth();
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    InventoryElement? element = __instance.GetElement(item.m_gridPos.x, item.m_gridPos.y, width);
                    if (element != null)
                    {
                        IconBackdrop.Set(element.m_icon, item);
                        IconEquipped.Apply(element.m_icon, element.m_equiped, item.m_equipped, true);
                    }
                }
                foreach (InventoryElement element in __instance.m_elements)
                {
                    if (element != null && !element.m_used)
                    {
                        IconBackdrop.Hide(element.m_icon);
                        IconEquipped.Apply(element.m_icon, element.m_equiped, false, true);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(HotkeyBar), nameof(HotkeyBar.UpdateIcons))]
        internal static class HotbarPatch
        {
            [HarmonyPostfix]
            private static void Postfix(HotkeyBar __instance)
            {
                List<HotkeyBar.ElementData> elements = __instance.m_elements;
                foreach (ItemDrop.ItemData item in __instance.m_items)
                {
                    int slot = item.m_gridPos.x;
                    if (slot >= 0 && slot < elements.Count)
                    {
                        IconBackdrop.Set(elements[slot].m_icon, item);
                        IconEquipped.Apply(elements[slot].m_icon, Fill(elements[slot]), item.m_equipped, false);
                    }
                }
                foreach (HotkeyBar.ElementData element in elements)
                {
                    if (!element.m_used)
                    {
                        IconBackdrop.Hide(element.m_icon);
                        IconEquipped.Apply(element.m_icon, Fill(element), false, false);
                    }
                }
            }

            // The hotbar's equipped fill is a GameObject the game switches on and off; its Image is what the marker hides.
            private static UnityEngine.UI.Image? Fill(HotkeyBar.ElementData element) =>
                element.m_equiped != null ? element.m_equiped.GetComponent<UnityEngine.UI.Image>() : null;
        }
    }
}
