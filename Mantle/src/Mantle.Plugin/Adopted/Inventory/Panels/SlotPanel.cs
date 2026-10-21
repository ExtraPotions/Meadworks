using ExtraPotions.Mantle.Embedded.PlateColumn;
using Mantle.Core;
using Mantle.Look;
using UnityEngine.UI;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The panel of labelled slots, right of the stat boxes' panel (<see cref="StatsPanel"/>), which sits right of the
    /// inventory panel: a child of the player panel (so it opens, closes and moves with it) anchored to its top-right
    /// corner (<see cref="PanelDress"/>). Across its top the two tab buttons (<see cref="TabButtons"/>), under them the
    /// shown tab's cells (<see cref="SlotPanelLayout"/>), and its last row holds the coin purse. Its top lines up with
    /// the inventory panel's background; it fits its slot rows and normal margins, the same in both tabs. Its cells are the player
    /// grid's own elements for the slot cells, moved here by <see cref="SlotElements"/>; the game's grid still owns them,
    /// so clicks, drags, tooltips and the gamepad work as on any cell. The background takes clicks, like the player
    /// panel's, so a dragged item let go between two slots is not dropped on the ground.
    /// </summary>
    public static class SlotPanel
    {
        public const string Name = "Mantle_slots";

        /// <summary>From the panel's edge to its first cell: the inventory panel's own margin (overhang plus the first row's top).</summary>
        public static float Pad { get; private set; } = 16f;

        public static RectTransform Find(InventoryGui gui) => gui != null ? gui.m_player.Find(Name) as RectTransform : null;

        /// <summary>The panel's layout for the current slots and tab.</summary>
        public static SlotPanelLayout Plan() => SlotPanelLayout.For(InventoryState.Layout, SlotTabs.Shown);

        /// <summary>Builds or updates the panels for the current layout; hidden with the module off or no slot to show.</summary>
        public static void Refresh(InventoryGui gui)
        {
            GridSkin.Panels(gui);
            StatsPanel.Refresh(gui);
            RectTransform panel = Find(gui);
            SlotPanelLayout plan = InventoryState.Active ? Plan() : null;
            bool show = plan != null && plan.Rows > 0;
            if (panel == null && show)
                panel = MakePanel(gui, Name);
            if (panel == null)
                return;
            panel.gameObject.SetActive(show);
            if (show)
                Fit(gui, panel, plan);
            ScreenLayout.ReportEquipment(gui, show ? panel.sizeDelta : Vector2.zero);
        }

        /// <summary>Extra room above the purse's row, with the divider line in the middle of it.</summary>
        public const float PurseGap = 12f;

        /// <summary>From the panel's top margin to its first row of cells: the tab buttons and the gap under them.</summary>
        public const float TabStrip = TabButtons.Height + TabButtons.Gap;

        private static SlotPanelLayout fitted;

        /// <summary>The top-left of a panel cell, in the panel's space, under the tabs; the purse's row sits <see cref="PurseGap"/> lower.</summary>
        public static Vector2 CellPosition(Vector2Int cell, float step)
        {
            float drop = fitted != null && fitted.PurseRow >= 0 && cell.y >= fitted.PurseRow ? PurseGap : 0f;
            return new Vector2(Pad + cell.x * step, -(Pad + TabStrip + cell.y * step + drop));
        }

        /// <summary>From the first cell's left edge to the last column's right edge.</summary>
        private static float InnerWidth(int columns, float step) => columns * step - (step - Column.BoxSize);

        private static void Fit(InventoryGui gui, RectTransform panel, SlotPanelLayout plan)
        {
            float step = gui.m_playerGrid.m_elementSpace;
            float overhang = PanelDress.Overhang(gui);
            Pad = PanelDress.Margin(gui);
            float trim = step - Column.BoxSize;
            float gap = plan.PurseRow > 0 ? PurseGap : 0f;
            float height = Pad * 2f + TabStrip + plan.Rows * step - trim + gap;
            float width = InnerWidth(plan.Columns, step);
            fitted = plan;
            panel.sizeDelta = new Vector2(Pad * 2f + width, height);
            panel.anchoredPosition = new Vector2(PanelDress.SlotPanelLeft(gui), overhang);
            GridSkin.GamePanel(panel.GetComponent<Image>());
            BehindBackground(gui, panel);
            float divider = Pad + TabStrip + plan.PurseRow * step - trim + (trim + PurseGap) / 2f;
            Divider.Place(panel, new Vector2(Pad, -divider), width, plan.PurseRow > 0);
            TabButtons.Place(gui, panel, plan, width);
            GearStats.Place(gui, panel, plan, step, ElementPlacer.ButtonFont(gui) ?? (gui.m_weight != null ? gui.m_weight.font : null));
        }

        /// <summary>
        /// Drawn before the player panel's background, so the stat boxes (drawn right after that background, where the
        /// library keeps them) sit over the stats panel; the backgrounds do not overlap.
        /// </summary>
        public static void BehindBackground(InventoryGui gui, RectTransform panel)
        {
            Image background = GridSkin.Background(gui.m_player);
            int at = background != null ? background.transform.GetSiblingIndex() : 0;
            if (panel.GetSiblingIndex() > at)
                panel.SetSiblingIndex(at);
        }

        /// <summary>A panel beside the inventory panel: anchored to its top-right corner, the game's panel look until skinned.</summary>
        public static RectTransform MakePanel(InventoryGui gui, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform panel = (RectTransform)go.transform;
            panel.SetParent(gui.m_player, false);
            panel.anchorMin = Vector2.one;
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(0f, 1f);
            Image background = go.GetComponent<Image>();
            Image game = GridSkin.Background(gui.m_player);
            background.sprite = GridSkin.GameSprite(game);
            background.material = GridSkin.GameMaterial(game);
            background.type = Image.Type.Sliced;
            background.raycastTarget = true;
            return panel;
        }
    }
}
