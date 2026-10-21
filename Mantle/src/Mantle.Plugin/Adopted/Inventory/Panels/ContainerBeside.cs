using HarmonyLib;
using Mantle.Core;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// An open chest's panel moves beside a tall inventory (user, 2026-10-07). The game hangs the container panel under
    /// the player panel (a child anchored to its bottom-left), so an inventory with many rows pushes the chest off the
    /// bottom of the screen. When the chest would not fit there, it goes to the right of the inventory, under the slot
    /// panel (Gear / Consumables), kept clear of the crafting panel on the right; when it fits again it goes back under the
    /// inventory, where the game had it. Worked out every frame a chest is open (after <see cref="ContainerPanelSize"/>),
    /// from the panels' corners in the player panel's own space; the position is written only when it changes. Only while
    /// Mantle lays the inventory out (the slot panel exists).
    /// Mantle, 2026-10-09: beside only when the whole chest fits on screen there (<see cref="ChestPlacement.BesideFits"/>).
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainer))]
    public static class ContainerBeside
    {
        private const float ScreenMargin = 8f;
        private static readonly Vector3[] corners = new Vector3[4];
        private static RectTransform placedPanel;
        private static Vector2 home;

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(InventoryGui __instance)
        {
            RectTransform panel = __instance.m_container;
            if (__instance.m_currentContainer == null || panel == null || __instance.m_player == null)
                return;
            if (panel != placedPanel)
            {
                placedPanel = panel;
                home = panel.anchoredPosition;
            }
            RectTransform slots = InventoryState.Active ? SlotPanel.Find(__instance) : null;
            Vector2 wanted = home;
            if (slots != null && !FitsUnder(__instance, panel) && Beside(__instance, panel, slots, out Vector2 beside))
                wanted = beside;
            if ((panel.anchoredPosition - wanted).sqrMagnitude > 0.01f)
                panel.anchoredPosition = wanted;
        }

        /// <summary>Whether the chest hanging at its home under the inventory ends above the screen's bottom.</summary>
        private static bool FitsUnder(InventoryGui gui, RectTransform panel)
        {
            RectTransform player = gui.m_player;
            float bottom = player.rect.yMin + home.y - panel.rect.height;
            return bottom >= ScreenEdges(player).y + ScreenMargin;
        }

        /// <summary>The chest's place right of the inventory, under the slot panel, left of the crafting panel; false when it does not fit on screen there.</summary>
        private static bool Beside(InventoryGui gui, RectTransform panel, RectTransform slots, out Vector2 position)
        {
            RectTransform player = gui.m_player;
            Corners(slots, player, out float slotLeft, out float slotBottom, out _);
            float left = Mathf.Max(slotLeft, player.rect.xMax + PanelDress.Gap);
            if (gui.m_crafting != null && gui.m_crafting.gameObject.activeInHierarchy)
            {
                Corners(gui.m_crafting, player, out float craftLeft, out _, out _);
                left = Mathf.Max(player.rect.xMax + PanelDress.Gap, Mathf.Min(left, craftLeft - PanelDress.Gap - panel.rect.width));
            }
            float top = slotBottom - PanelDress.Gap;
            // The panel is anchored to the player panel's bottom-left corner with its pivot at its own top-left.
            position = new Vector2(left - player.rect.xMin, top - player.rect.yMin);
            Vector2 screen = ScreenEdges(player);
            return ChestPlacement.BesideFits(left, top, panel.rect.width, panel.rect.height, screen.x, screen.y, ScreenMargin);
        }

        private static void Corners(RectTransform rect, RectTransform space, out float left, out float bottom, out float right)
        {
            rect.GetWorldCorners(corners);
            Vector3 low = space.InverseTransformPoint(corners[0]);
            Vector3 high = space.InverseTransformPoint(corners[2]);
            left = low.x;
            bottom = low.y;
            right = high.x;
        }

        /// <summary>The right and bottom edges of the inventory screen, in the player panel's space.</summary>
        private static Vector2 ScreenEdges(RectTransform player)
        {
            RectTransform screen = player.parent as RectTransform;
            if (screen == null)
                return new Vector2(float.MaxValue, float.MinValue);
            Corners(screen, player, out _, out float bottom, out float right);
            return new Vector2(right, bottom);
        }
    }
}
