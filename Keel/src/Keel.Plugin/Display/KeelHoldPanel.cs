// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the container panel of a hold wider than the game's 8 columns (native defect e5db2aac).
using System;
using ExtraPotions.Keel.Display;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// The game draws its container panel for 8 columns and centers the grid on it, so a ship's hold of 9 to 12 columns
    /// (Cargo Columns) spilled out of the panel and off the left of the screen. While such a hold is open the panel widens
    /// by one grid step per column beyond 8 (the game's scrollbar kept at its right edge, the grid rebuilt so it centers on
    /// the wider panel) and is kept on screen: moved inside the screen's margins, and scaled down only when the screen is
    /// narrower than the panel. Columns stay as configured. With an inventory owner (Mantle) installed it owns the container panel (it
    /// widens wide containers itself and places the panel in its stack layout), so Keel only rebuilds the grid after the
    /// panel's width changes. Everything is put back when another container opens or Keel stops.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainer))]
    internal static class KeelHoldPanel
    {
        private const string ScrollName = "ContainerScroll";
        private static readonly Vector3[] corners = new Vector3[4];

        private static RectTransform? panel;
        private static float gameWidth;
        private static Vector3 gameScale;
        private static RectTransform? scroll;
        private static float scrollX;
        private static int shownColumns = HoldPanel.GameColumns;
        private static float shift;
        private static float seenWidth = -1f;
        private static readonly FeatureLink inventoryOwner = new FeatureLink(FeatureOwnerContract.Id(FeatureOwnerContract.Inventory));

        // Presence, as before: an installed inventory owner (Mantle, through meadworks.owns.inventory) owns the panel whatever its switch says.
        private static bool Mantle => inventoryOwner.TryOwned(ExtraPotions.Meadworks.Crucible.Plugin.IsReady ? ExtraPotions.Meadworks.Crucible.Plugin.Services.Capabilities : null, out _);

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(InventoryGui __instance)
        {
            try { Apply(__instance); }
            catch (Exception) { Restore(); }
        }

        private static void Apply(InventoryGui gui)
        {
            var current = gui.m_container;
            if (current == null || gui.m_containerGrid == null) return;
            if (current != panel)
            {
                panel = current;
                gameWidth = current.sizeDelta.x;
                gameScale = current.localScale;
                scroll = current.Find(ScrollName) as RectTransform;
                scrollX = scroll != null ? scroll.anchoredPosition.x : 0f;
                shownColumns = HoldPanel.GameColumns;
                shift = 0f;
                seenWidth = -1f;
            }
            var container = gui.m_currentContainer;
            var inventory = container != null ? container.GetInventory() : null;
            var hold = Plugin.Ready && KeelCatalog.Active && inventory != null && inventory.GetWidth() > HoldPanel.GameColumns
                && container!.GetComponentInParent<Ship>() != null;
            if (Mantle)
            {
                // Mantle sizes the panel after the game built the grid; rebuild it once so the cells center on the new width.
                var width = current.rect.width;
                if (hold && seenWidth >= 0f && Mathf.Abs(width - seenWidth) > 0.5f) gui.m_containerGrid.m_width = 0;
                seenWidth = width;
                return;
            }
            var columns = hold ? inventory!.GetWidth() : HoldPanel.GameColumns;
            if (columns != shownColumns)
            {
                var width = HoldPanel.Width(gameWidth, gui.m_containerGrid.m_elementSpace, columns);
                current.sizeDelta = new Vector2(width, current.sizeDelta.y);
                if (scroll != null)
                    scroll.anchoredPosition = new Vector2(scrollX + HoldPanel.CentreShift(gameWidth, width), scroll.anchoredPosition.y);
                gui.m_containerGrid.m_width = 0;
                shownColumns = columns;
            }
            if (hold) KeepOnScreen(gui, current);
            else Unplace(current);
        }

        private static void KeepOnScreen(InventoryGui gui, RectTransform current)
        {
            var screen = gui.m_player != null ? gui.m_player.parent as RectTransform : null;
            if (screen == null) return;
            current.GetWorldCorners(corners);
            var left = screen.InverseTransformPoint(corners[0]).x;
            var right = screen.InverseTransformPoint(corners[2]).x;
            var factor = gameScale.x != 0f ? current.localScale.x / gameScale.x : 1f;
            var unscaled = factor > 0f ? (right - left) / factor : right - left;
            var place = HoldPanel.Place(left, unscaled, screen.rect.xMin, screen.rect.xMax);
            var scale = gameScale * place.Scale;
            if ((current.localScale - scale).sqrMagnitude > 1e-6f) current.localScale = scale;
            if (Mathf.Abs(place.Shift) < 0.5f || current.parent == null) return;
            var parentShift = current.parent.InverseTransformVector(screen.TransformVector(new Vector3(place.Shift, 0f, 0f))).x;
            current.anchoredPosition += new Vector2(parentShift, 0f);
            shift += parentShift;
        }

        private static void Unplace(RectTransform current)
        {
            if ((current.localScale - gameScale).sqrMagnitude > 1e-6f) current.localScale = gameScale;
            if (shift == 0f) return;
            current.anchoredPosition -= new Vector2(shift, 0f);
            shift = 0f;
        }

        /// <summary>Puts the game's container panel back (Keel stopping).</summary>
        public static void Restore()
        {
            var current = panel;
            if (current == null) return;
            try
            {
                if (!Mantle && shownColumns != HoldPanel.GameColumns)
                {
                    current.sizeDelta = new Vector2(gameWidth, current.sizeDelta.y);
                    if (scroll != null) scroll.anchoredPosition = new Vector2(scrollX, scroll.anchoredPosition.y);
                    var gui = InventoryGui.instance;
                    if (gui != null && gui.m_containerGrid != null) gui.m_containerGrid.m_width = 0;
                }
                if (!Mantle) Unplace(current);
            }
            catch (Exception) { }
            panel = null;
        }
    }
}
