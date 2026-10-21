using HarmonyLib;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The cell under the pointer is a cell that is shown. The game takes the first element whose rectangle holds the
    /// pointer (<c>InventoryGrid.GetHoveredElement</c>), hidden or not, and the slot panel's two tabs lay their cells on
    /// the same spots: on the Consumables tab the hidden Gear cells come first in the list, so the item tooltip was
    /// written into the hidden cell's tooltip (an arrow in an Ammo slot showed none) and the game's equip-hovered and
    /// touch drop went to the hidden item. The key ring's and tacklebox's pop-ups hide cells the same way.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.GetHoveredElement))]
    public static class HoveredCell
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryGrid __instance, ref InventoryElement __result)
        {
            if (__result == null || __result.gameObject.activeInHierarchy)
                return;
            __result = null;
            foreach (InventoryElement element in __instance.m_elements)
            {
                if (element == null || !element.gameObject.activeInHierarchy)
                    continue;
                RectTransform rect = element.GetElementRectTransform();
                if (rect.rect.Contains(rect.InverseTransformPoint(ZInput.pointerPosition)))
                {
                    __result = element;
                    return;
                }
            }
        }
    }
}
