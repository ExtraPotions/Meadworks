// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        static readonly FieldInfo tooltipCurrent = typeof(UITooltip).GetField("m_current", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly FieldInfo tooltipGridElements = typeof(InventoryGrid).GetField("m_elements", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo tooltipInstance = typeof(UITooltip).GetField("m_tooltip", BindingFlags.Static | BindingFlags.NonPublic);
        static UITooltip tooltipOwner;
        static GameObject tooltipRoot, tooltipPrefab;
        static Image tooltipPanel;
        static ImageLook tooltipLook;
        static string tooltipReason = "not-observed", tooltipBinding = "";
        static bool tooltipStyled, tooltipYielded, tooltipTinted;
        static Color tooltipOriginalColor, tooltipAssignedColor;
        static float tooltipCaptureUntil, tooltipPanelWidth, tooltipPanelHeight, tooltipCanvasWidth;

        internal static object TooltipStatus => new { instance = tooltipRoot != null ? tooltipRoot.name : "",
            owner = tooltipOwner != null ? tooltipOwner.name : "", binding = tooltipBinding, reason = tooltipReason,
            captured = tooltipLook != null, styled = tooltipStyled, yielded = tooltipYielded,
            panel = tooltipPanel != null ? tooltipPanel.name : "", on = On,
            panelWidth = tooltipPanelWidth, panelHeight = tooltipPanelHeight, canvasWidth = tooltipCanvasWidth,
            originalTint = tooltipOriginalColor.ToString("R"), currentTint = tooltipPanel != null ? tooltipPanel.color.ToString("R") : "",
            rendererAlpha = tooltipPanel != null ? tooltipPanel.canvasRenderer.GetAlpha() : 0f, tintOwned = tooltipTinted };

        static void TickTooltip()
        {
            if (!MeadworksHeadless.AllowsUi) { ShutdownTooltip(); return; }
            if (tooltipCurrent == null || tooltipInstance == null) { tooltipReason = "native-fields-unavailable"; return; }
            var current = tooltipCurrent.GetValue(null) as UITooltip;
            var instance = tooltipInstance.GetValue(null) as GameObject;
            var prefab = current != null ? current.m_tooltipPrefab : null;
            if (!ReferenceEquals(instance, tooltipRoot) || !ReferenceEquals(current, tooltipOwner) || !ReferenceEquals(prefab, tooltipPrefab))
            {
                ShutdownTooltip(); tooltipRoot = instance; tooltipOwner = current; tooltipPrefab = prefab;
                if (instance != null && current != null) { tooltipCaptureUntil = Time.unscaledTime + 3f; CaptureTooltip(); }
            }
            if (tooltipLook == null && tooltipReason == "unbounded-panel" && Time.unscaledTime < tooltipCaptureUntil) CaptureTooltip();
            if (tooltipLook == null || tooltipYielded) return;
            if (tooltipPanel == null || tooltipRoot == null || tooltipRoot.transform.childCount == 0
                || tooltipRoot.transform.GetChild(0) != tooltipPanel.transform)
            {
                RestoreTooltipTint(); tooltipLook.Dispose(); tooltipLook = null; tooltipYielded = true; tooltipStyled = false;
                tooltipReason = "panel-binding-changed"; return;
            }
            if (tooltipPanel.color != (tooltipTinted ? tooltipAssignedColor : tooltipOriginalColor))
            {
                tooltipLook.Dispose(); tooltipLook = null; tooltipTinted = false;
                tooltipYielded = true; tooltipStyled = false; tooltipReason = "background-color-changed"; return;
            }
            tooltipStyled = tooltipLook.Apply(On);
            if (tooltipStyled)
            {
                tooltipAssignedColor = new Color(1f, 1f, 1f, tooltipOriginalColor.a);
                tooltipPanel.color = tooltipAssignedColor; tooltipTinted = true;
            }
            else RestoreTooltipTint();
            if (On && !tooltipStyled)
            { tooltipYielded = true; tooltipReason = "appearance-unavailable-or-changed"; }
            else tooltipReason = On ? "styled-background-only" : "native-appearance-restored";
        }
        static void RestoreTooltipTint()
        {
            if (tooltipPanel != null && tooltipTinted && tooltipPanel.color == tooltipAssignedColor) tooltipPanel.color = tooltipOriginalColor;
            tooltipTinted = false;
        }
        static void ShutdownTooltip()
        {
            RestoreTooltipTint();
            tooltipLook?.Dispose(); tooltipLook = null; tooltipPanel = null;
            tooltipOwner = null; tooltipRoot = null; tooltipPrefab = null;
            tooltipCaptureUntil = tooltipPanelWidth = tooltipPanelHeight = tooltipCanvasWidth = 0;
            tooltipStyled = tooltipYielded = false; tooltipReason = "no-native-instance"; tooltipBinding = "";
        }
        static void CaptureTooltip()
        {
            var gui = InventoryGui.instance;
            if (gui == null || tooltipPrefab == null) { tooltipReason = "no-known-native-prefab"; return; }
            var element = gui.m_playerGrid != null ? gui.m_playerGrid.m_elementPrefab : null;
            var itemTip = element != null ? element.GetComponent<UITooltip>() : null;
            var craftTip = gui.m_craftButton != null ? gui.m_craftButton.GetComponent<UITooltip>() : null;
            if (itemTip != null && itemTip.m_tooltipPrefab == tooltipPrefab && NativeTooltipGridOwner(gui.m_playerGrid, tooltipOwner))
                tooltipBinding = "inventory.elementPrefab";
            else if (craftTip != null && craftTip.m_tooltipPrefab == tooltipPrefab && ReferenceEquals(tooltipOwner, craftTip))
                tooltipBinding = "inventory.craftButton";
            else { tooltipReason = "unknown-prefab-binding"; return; }
            if (tooltipBinding == "inventory.elementPrefab" ? !NativeTooltipGridOwner(gui.m_playerGrid, tooltipOwner) : !ReferenceEquals(tooltipOwner, craftTip))
            { tooltipReason = "owner-outside-native-inventory"; return; }
            if (!TooltipComponents(tooltipPrefab, true, false) || !TooltipComponents(tooltipRoot, true, true))
            { tooltipReason = "foreign-or-shared-owner"; return; }
            var original = TooltipPanel(tooltipPrefab, false);
            var panel = TooltipPanel(tooltipRoot, true);
            if (original == null || panel == null || panel.name != original.name)
            { tooltipReason = "unverified-panel-hierarchy"; return; }
            var canvas = tooltipRoot.GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            var rect = panel.rectTransform.rect;
            tooltipPanelWidth = rect.width; tooltipPanelHeight = rect.height; tooltipCanvasWidth = canvasRect != null ? canvasRect.rect.width : 0;
            if (canvasRect == null || rect.width <= 0 || rect.height <= 0 || rect.width >= canvasRect.rect.width)
            { tooltipReason = "unbounded-panel"; return; }
            if (owned.Contains(panel) || KitSkin.Owns(panel) || KitNativeDialogs.Owns(panel))
            { tooltipReason = "image-already-owned"; return; }
            if (panel.sprite != original.sprite || panel.material != original.material || panel.type != original.type
                || panel.color != original.color || !Mathf.Approximately(panel.pixelsPerUnitMultiplier, original.pixelsPerUnitMultiplier))
            { tooltipReason = "image-differs-from-prefab"; return; }
            tooltipPanel = panel; tooltipOriginalColor = panel.color;
            // Card uses the existing compact panel art without adding frame/layout children.
            tooltipLook = new ImageLook(panel, KitSurfaceRole.Card, protectOffState: true);
            tooltipReason = "verified-native-panel";
        }
        static bool NativeTooltipGridOwner(InventoryGrid grid, UITooltip current)
        {
            if (grid == null || current == null || grid.m_gridRoot == null || tooltipGridElements == null) return false;
            var element = current.GetComponent<InventoryElement>();
            var roster = tooltipGridElements.GetValue(grid) as System.Collections.Generic.List<InventoryElement>;
            return element != null && roster != null && roster.Contains(element)
                && element.transform.parent == grid.m_gridRoot && ReferenceEquals(element.m_tooltip, current)
                && ReferenceEquals(element.GetComponent<UITooltip>(), current);
        }
        static Image TooltipPanel(GameObject root, bool allowScroll)
        {
            if (root.transform.childCount != 1) return null;
            var panel = root.transform.GetChild(0);
            var image = panel.GetComponent<Image>();
            var topic = panel.Find("Topic");
            var text = panel.Find("Text");
            if (text == null && allowScroll && TooltipScrollOwner(root))
                text = panel.Find("inscribe_tooltip_scroll/Text");
            if (image == null || !image.enabled || !image.fillCenter || image.sprite == null
                || topic == null || topic.GetComponent<TMP_Text>() == null || text == null || text.GetComponent<TMP_Text>() == null)
                return null;
            return image;
        }
        static bool TooltipScrollOwner(GameObject root)
        {
            foreach (var component in root.GetComponents<MonoBehaviour>())
                if (component != null && IsTooltipScroll(component)) return true;
            return false;
        }
        static bool IsTooltipScroll(MonoBehaviour component) => component.GetType().Assembly.GetName().Name == "Inscribe"
            && component.GetType().FullName == "ExtraPotions.Inscribe.Adopted.Display.Tooltips.TooltipScroll";
        static bool TooltipComponents(GameObject root, bool descendants, bool allowScroll)
        {
            var components = descendants ? root.GetComponentsInChildren<MonoBehaviour>(true) : root.GetComponents<MonoBehaviour>();
            foreach (var component in components)
            {
                if (component == null) return false;
                var assembly = component.GetType().Assembly;
                var name = assembly.GetName().Name;
                if (allowScroll && component.gameObject == root && IsTooltipScroll(component)) continue;
                if (assembly != typeof(Menu).Assembly && assembly != typeof(UITooltip).Assembly
                    && !name.StartsWith("Unity", StringComparison.Ordinal) && name != "gui_framework" && name != "assembly_utils") return false;
            }
            for (var at = root.transform; at != null; at = at.parent)
                if (at.name.StartsWith("Crucible_", StringComparison.Ordinal) || at.name.StartsWith("Meadworks_", StringComparison.Ordinal)
                    || at.name.StartsWith("PlateColumn_", StringComparison.Ordinal)) return false;
            return true;
        }
    }
}
