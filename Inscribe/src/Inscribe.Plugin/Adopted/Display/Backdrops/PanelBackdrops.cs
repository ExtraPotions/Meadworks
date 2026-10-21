// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Valheim.UI;

namespace ExtraPotions.Inscribe.Adopted.Display.Backdrops
{
    /// <summary>
    /// Backdrops on the icons outside the item cells: the item following the cursor while dragged and the radial menu's
    /// items. The crafting panel's (an upgrade entry in the recipe list and the selected upgrade's large icon, both
    /// showing the player's own item) are set by <see cref="CraftingPanel"/>, from its own two patches. The split dialog
    /// needs none: a magic item never stacks.
    /// </summary>
    internal static class PanelBackdrops
    {
        private static GameObject? _ghost;
        private static Image? _ghostIcon;

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateItemDrag))]
        internal static class DragPatch
        {
            [HarmonyPostfix]
            private static void Postfix(InventoryGui __instance)
            {
                GameObject ghost = __instance.m_dragGo;
                if (ghost == null)
                {
                    return;
                }
                if (!ReferenceEquals(ghost, _ghost))
                {
                    _ghost = ghost;
                    Transform icon = ghost.transform.Find("icon");
                    _ghostIcon = icon == null ? null : icon.GetComponent<Image>();
                }
                IconBackdrop.Set(_ghostIcon, __instance.m_dragItem);
            }
        }

        [HarmonyPatch(typeof(ItemElement), nameof(ItemElement.Init))]
        internal static class RadialPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ItemElement __instance, ItemDrop.ItemData item) => IconBackdrop.Set(__instance.m_icon, item);
        }
    }
}
