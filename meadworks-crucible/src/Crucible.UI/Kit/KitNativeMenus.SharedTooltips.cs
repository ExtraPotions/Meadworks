// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        static UITooltip sharedTooltipOwner;
        static GameObject sharedTooltipRoot, sharedTooltipPrefab;
        static Image sharedTooltipPanel;
        static ImageLook sharedTooltipLook;
        static bool sharedTooltipRegistered, sharedTooltipStyled, sharedTooltipYielded;
        static string sharedTooltipReason = "not-observed";
        static string sharedTooltipRejectedStage = "", sharedTooltipRejectedAssembly = "", sharedTooltipRejectedType = "", sharedTooltipRejectedRoot = "", sharedTooltipRejectedPath = "", sharedTooltipRejectedRule = "";
        static Color sharedTooltipColor;
        static bool sharedTooltipRaycast;
        static Sprite sharedTooltipOriginal;
        static Material sharedTooltipMaterial;
        static Image.Type sharedTooltipType;
        static float sharedTooltipPpu, sharedTooltipNextComponents, sharedTooltipCaptureUntil;
        static readonly List<MonoBehaviour> sharedTooltipComponents = new List<MonoBehaviour>();
        static readonly List<Component> sharedTooltipMarkerComponents = new List<Component>();

        internal static object SharedTooltipStatus => new {
            instance = sharedTooltipRoot != null ? sharedTooltipRoot.name : "",
            owner = sharedTooltipOwner != null ? sharedTooltipOwner.name : "",
            registered = sharedTooltipRegistered, admitted = sharedTooltipLook != null,
            styled = sharedTooltipStyled, yielded = sharedTooltipYielded, reason = sharedTooltipReason,
            binding = sharedTooltipRegistered ? "KitTooltip.Mark registered template reference" : "",
            panel = sharedTooltipPanel != null ? sharedTooltipPanel.name : "", on = On,
            rejectedStage = sharedTooltipRejectedStage, rejectedAssembly = sharedTooltipRejectedAssembly,
            rejectedType = sharedTooltipRejectedType, rejectedRoot = sharedTooltipRejectedRoot,
            rejectedPath = sharedTooltipRejectedPath, rejectedRule = sharedTooltipRejectedRule
        };

        static void TickSharedTooltip()
        {
            if (!MeadworksHeadless.AllowsUi || tooltipCurrent == null || tooltipInstance == null) { ShutdownSharedTooltip(); return; }
            var current = tooltipCurrent.GetValue(null) as UITooltip;
            var root = tooltipInstance.GetValue(null) as GameObject;
            var prefab = current != null ? current.m_tooltipPrefab : null;
            if (!ReferenceEquals(current, sharedTooltipOwner) || !ReferenceEquals(root, sharedTooltipRoot) || !ReferenceEquals(prefab, sharedTooltipPrefab))
            {
                ShutdownSharedTooltip(); sharedTooltipOwner = current; sharedTooltipRoot = root; sharedTooltipPrefab = prefab;
                sharedTooltipRegistered = KitTooltip.OwnsTemplate(prefab);
                sharedTooltipCaptureUntil = Time.unscaledTime + 3f;
                sharedTooltipReason = sharedTooltipRegistered ? "waiting-native-visible" : "unregistered-template";
            }
            if (sharedTooltipYielded || root == null || current == null || !sharedTooltipRegistered) return;
            if (sharedTooltipLook == null)
            {
                if (sharedTooltipReason != "waiting-native-visible" || !root.activeInHierarchy) return;
                CaptureSharedTooltip();
                if (sharedTooltipLook == null) return;
            }
            if (!KitTooltip.OwnsTemplate(prefab) || SharedTooltipPanel(root, true) != sharedTooltipPanel)
            { YieldSharedTooltip("registered-panel-binding-changed"); return; }
            if (Time.unscaledTime >= sharedTooltipNextComponents)
            {
                sharedTooltipNextComponents = Time.unscaledTime + .2f;
                if (!SharedTooltipSafe(root, true, true, "instance") || !SharedTooltipSafe(prefab, true, false, "template"))
                { YieldSharedTooltip(sharedTooltipReason); return; }
            }
            if (sharedTooltipPanel.color != sharedTooltipColor || sharedTooltipPanel.raycastTarget != sharedTooltipRaycast)
            { YieldSharedTooltip("foreign-panel-property"); return; }
            if (!sharedTooltipStyled && !SharedTooltipOriginalMatches())
            { YieldSharedTooltip("foreign-panel-property"); return; }
            bool styled = sharedTooltipLook.Apply(On);
            if (On && !styled) { YieldSharedTooltip("appearance-unavailable-or-changed"); return; }
            sharedTooltipStyled = styled;
            sharedTooltipColor = sharedTooltipPanel.color;
            sharedTooltipReason = styled ? "styled-shared-background-only" : "shared-original-restored";
        }
        static bool SharedTooltipOriginalMatches() => sharedTooltipPanel != null
            && sharedTooltipPanel.sprite == sharedTooltipOriginal && sharedTooltipPanel.material == sharedTooltipMaterial
            && sharedTooltipPanel.type == sharedTooltipType && Mathf.Approximately(sharedTooltipPanel.pixelsPerUnitMultiplier, sharedTooltipPpu);
        static void YieldSharedTooltip(string reason)
        {
            sharedTooltipLook?.Dispose(); sharedTooltipLook = null; sharedTooltipStyled = false;
            sharedTooltipYielded = true; sharedTooltipReason = reason;
        }
        static void ShutdownSharedTooltip()
        {
            sharedTooltipLook?.Dispose(); sharedTooltipLook = null; sharedTooltipOwner = null;
            sharedTooltipRoot = sharedTooltipPrefab = null; sharedTooltipPanel = null;
            sharedTooltipRegistered = sharedTooltipStyled = sharedTooltipYielded = false;
            sharedTooltipReason = "no-shared-instance"; sharedTooltipNextComponents = 0;
            sharedTooltipRejectedStage = sharedTooltipRejectedAssembly = sharedTooltipRejectedType = sharedTooltipRejectedRoot = sharedTooltipRejectedPath = sharedTooltipRejectedRule = "";
            sharedTooltipComponents.Clear();
        }
        static void CaptureSharedTooltip()
        {
            if (!SharedTooltipSafe(sharedTooltipPrefab, true, false, "template") || !SharedTooltipSafe(sharedTooltipRoot, true, true, "instance"))
            { return; }
            var original = SharedTooltipPanel(sharedTooltipPrefab, false);
            var panel = SharedTooltipPanel(sharedTooltipRoot, true);
            if (original == null || panel == null || original.name != panel.name)
            { sharedTooltipReason = "unverified-shared-panel"; return; }
            var canvas = sharedTooltipRoot.GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRect == null || panel.rectTransform.rect.width <= 0 || panel.rectTransform.rect.height <= 0
                || panel.rectTransform.rect.width >= canvasRect.rect.width)
            {
                // Native layout and Inscribe fitting can settle after the first visible frame; never force either.
                sharedTooltipReason = Time.unscaledTime < sharedTooltipCaptureUntil ? "waiting-native-visible" : "unbounded-shared-panel"; return;
            }
            if (owned.Contains(panel) || KitSkin.Owns(panel) || KitNativeDialogs.Owns(panel))
            { sharedTooltipReason = "shared-image-already-owned"; return; }
            if (panel.sprite != original.sprite || panel.material != original.material || panel.type != original.type
                || panel.color != original.color || !Mathf.Approximately(panel.pixelsPerUnitMultiplier, original.pixelsPerUnitMultiplier))
            { sharedTooltipReason = "shared-image-differs-from-template"; return; }
            sharedTooltipPanel = panel; sharedTooltipOriginal = panel.sprite; sharedTooltipMaterial = panel.material;
            sharedTooltipType = panel.type; sharedTooltipPpu = panel.pixelsPerUnitMultiplier;
            sharedTooltipColor = panel.color; sharedTooltipRaycast = panel.raycastTarget;
            sharedTooltipLook = new ImageLook(panel, KitSurfaceRole.Card, protectOffState: true);
            sharedTooltipNextComponents = 0; sharedTooltipReason = "verified-registered-shared-panel";
        }
        static Image SharedTooltipPanel(GameObject root, bool allowScroll)
        {
            if (root == null || root.transform.childCount < 1 || root.transform.childCount > 2) return null;
            if (root.transform.childCount == 2)
            {
                var marker = root.transform.GetChild(1);
                sharedTooltipMarkerComponents.Clear(); marker.GetComponents(sharedTooltipMarkerComponents);
                if (marker.name != "Scroll View" || marker.gameObject.activeSelf || marker.childCount != 0
                    || sharedTooltipMarkerComponents.Count != 1) return null;
            }
            var panel = root.transform.GetChild(0);
            var image = panel.GetComponent<Image>(); var topic = panel.Find("Topic"); var text = panel.Find("Text");
            if (text == null && allowScroll && TooltipScrollOwner(root)) text = panel.Find("inscribe_tooltip_scroll/Text");
            if (image == null || !image.enabled || !image.fillCenter || image.sprite == null
                || topic == null || topic.GetComponent<TMP_Text>() == null || text == null || text.GetComponent<TMP_Text>() == null) return null;
            return image;
        }
        static string SharedTooltipPath(Transform target)
        {
            string path = "";
            for (var at = target; at != null; at = at.parent) path = at.name + (path.Length == 0 ? "" : "/" + path);
            return path;
        }
        static bool RejectSharedTooltipComponent(GameObject root, MonoBehaviour component, string stage, string rule)
        {
            sharedTooltipRejectedStage = stage; sharedTooltipRejectedRule = rule;
            sharedTooltipRejectedRoot = SharedTooltipPath(root.transform);
            sharedTooltipRejectedPath = component != null ? SharedTooltipPath(component.transform) : sharedTooltipRejectedRoot;
            sharedTooltipRejectedType = component != null ? component.GetType().FullName : "<missing-script>";
            sharedTooltipRejectedAssembly = component != null ? component.GetType().Assembly.FullName : "";
            sharedTooltipReason = "foreign-" + stage + "-component";
            return false;
        }
        static bool SharedTooltipSafe(GameObject root, bool descendants, bool allowScroll, string stage)
        {
            sharedTooltipComponents.Clear();
            if (descendants) root.GetComponentsInChildren(true, sharedTooltipComponents);
            else root.GetComponents(sharedTooltipComponents);
            foreach (var component in sharedTooltipComponents)
            {
                if (component == null) return RejectSharedTooltipComponent(root, null, stage, "missing-script");
                var assembly = component.GetType().Assembly; string name = assembly.GetName().Name;
                if (allowScroll && component.gameObject == root && IsTooltipScroll(component)) continue;
                if (assembly != typeof(Menu).Assembly && assembly != typeof(UITooltip).Assembly
                    && !name.StartsWith("Unity", StringComparison.Ordinal) && name != "gui_framework" && name != "assembly_utils")
                    return RejectSharedTooltipComponent(root, component, stage, "assembly-not-admitted");
            }
            return true;
        }
    }
}
