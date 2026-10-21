using HarmonyLib;
using Mantle.Core;
using Mantle.Layout;
using Mantle.Look;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The player panel's size. The game grows it by one grid row per inventory row (<c>InventoryGui.SetInventorySize</c>)
    /// and never widens it; here it gets the main grid's rows (the slot rows are drawn in the slot panel, not the grid),
    /// however few (with fewer than the game's 4 OpenKeep's buttons sit right under the grid; the side panels keep their
    /// height, <see cref="BackpackPanelHeight"/>), and one column step per column beyond 8, plus a few pixels at the top with the brown frame so the first row
    /// clears it, plus, with OpenKeep installed, a strip at the bottom that holds OpenKeep's button row inside the panel
    /// (the user asked for the panel to hold the buttons rather than have them hang below it; <see cref="ButtonStrip"/>).
    /// The grid centres itself in the panel, so the cells follow. A container whose inventory is wider than 8 (a wide
    /// chest, the grave of a wide inventory) widens the container panel the same way, its scrollbar kept at the right edge.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetInventorySize))]
    public static class PanelSize
    {
        private static float playerWidth = -1f;

        /// <summary>
        /// True while the player panel is laid out here and OpenKeep's or Burrow's button row goes inside it.
        /// Changed for Mantle, 2026-10-07: Burrow (<see cref="BurrowRow"/>) keeps the strip as OpenKeep does.
        /// </summary>
        public static bool ButtonsInside => InventoryState.Active && (InventoryGui.instance == null || !InventoryGui.instance.IsContainerOpen()) && (OpenKeepLink.Present || BurrowRow.Present);

        [HarmonyPrefix]
        public static bool Prefix(InventoryGui __instance, int rows)
        {
            RectTransform panel = __instance.m_player;
            if (playerWidth < 0f)
                playerWidth = panel.sizeDelta.x;
            InventoryLayout layout = InventoryState.Active ? InventoryState.Layout : null;
            int columns = layout != null ? layout.Width : InventorySettings.GameWidth;
            int mainRows = layout != null ? layout.MainRows : rows;
            float pad = GridSkin.TopPad;
            float step = __instance.m_playerGrid.m_elementSpace;
            float strip = ButtonsInside ? ButtonStrip.Height : 0f;
            float height = __instance.m_playerHeight + (mainRows - InventorySettings.GameRows) * __instance.m_invGridHeight + pad + strip;
            panel.sizeDelta = new Vector2(playerWidth + (columns - InventorySettings.GameWidth) * step, height);
            __instance.m_playerGrid.m_gridRoot.anchoredPosition = new Vector2(0f, -pad);
            ButtonStrip.Mark(panel, ButtonsInside);
            SlotPanel.Refresh(__instance);
            SlotElements.Invalidate();
            return false;
        }

        /// <summary>Applies the current layout's size again (a setting that changes the frame or the panel).</summary>
        public static void Refresh()
        {
            InventoryGui gui = InventoryGui.instance;
            Player player = Player.m_localPlayer;
            if (gui != null && player != null)
                gui.SetInventorySize(LayoutBuilder.GameRows(player));
        }
    }

    /// <summary>The container panel's width for containers wider than the game's 8 columns.</summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainer))]
    public static class ContainerPanelSize
    {
        private static RectTransform sizedPanel;
        private static float baseWidth;
        private static float scrollX;
        private static int shownColumns;

        [HarmonyPostfix]
        public static void Postfix(InventoryGui __instance)
        {
            Transform strip = __instance.m_player.Find(ButtonStrip.Name);
            if (strip != null && strip.gameObject.activeSelf != PanelSize.ButtonsInside)
                PanelSize.Refresh();
            Container container = __instance.m_currentContainer;
            if (container == null || container.GetInventory() == null)
                return;
            RectTransform panel = __instance.m_container;
            RectTransform scroll = panel.Find("ContainerScroll") as RectTransform;
            if (panel != sizedPanel)
            {
                sizedPanel = panel;
                baseWidth = panel.sizeDelta.x;
                scrollX = scroll != null ? scroll.anchoredPosition.x : 0f;
                shownColumns = InventorySettings.GameWidth;
            }
            int columns = Mathf.Max(InventorySettings.GameWidth, container.GetInventory().GetWidth());
            if (columns == shownColumns)
                return;
            float extra = (columns - InventorySettings.GameWidth) * __instance.m_containerGrid.m_elementSpace;
            panel.sizeDelta = new Vector2(baseWidth + extra, panel.sizeDelta.y);
            if (scroll != null)
                scroll.anchoredPosition = new Vector2(scrollX + extra / 2f, scroll.anchoredPosition.y);
            shownColumns = columns;
        }
    }

    /// <summary>
    /// Whether a product places its Quick stack, Store all, Top up and Sort row (Burrow) on this peer: the
    /// <c>meadworks.owns.stow_row</c> capability's presence, read through Crucible (no plugin lookup). That row is laid
    /// inside the strip marked <see cref="ButtonStrip.Name"/>, like OpenKeep's; the two mods share no code.
    /// </summary>
    internal static class BurrowRow
    {
        private static readonly ExtraPotions.Meadworks.Crucible.FeatureLink owner = new ExtraPotions.Meadworks.Crucible.FeatureLink(
            ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.Id(ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.StowRow));

        public static bool Present => owner.TryOwned(ExtraPotions.Meadworks.Crucible.Plugin.IsReady
            ? ExtraPotions.Meadworks.Crucible.Plugin.Services.Capabilities : null, out _);
    }
}
