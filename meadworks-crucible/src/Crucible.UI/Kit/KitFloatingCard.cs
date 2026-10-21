// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// A read-only floating kit card (a comparison tooltip and the like): a kit panel of fixed width whose height follows
    /// its content (a vertical layout group padded by <see cref="KitCardLayout.Padding"/>), drawn above the product's
    /// canvas content, taking no input, and placed beside the pointer inside its parent by
    /// <see cref="KitCardLayout.FloatTopLeft"/>. The product fills <see cref="Content"/> and calls <see cref="ShowAt"/>.
    /// </summary>
    public sealed class KitFloatingCard : IDisposable
    {
        public RectTransform Root { get; }
        /// <summary>Where the product adds its rows (text, dividers, cards).</summary>
        public RectTransform Content => Root;
        public bool Visible => Root != null && Root.gameObject.activeSelf;
        bool disposed;

        KitFloatingCard(RectTransform root) { Root = root; }

        /// <summary>
        /// A hidden floating card under <paramref name="parent"/> (usually a full-screen canvas rect); null headless or
        /// before the graphical kit is active.
        /// </summary>
        public static KitFloatingCard Create(RectTransform parent, string name, float width = KitCardLayout.FloatWidth)
        {
            if (!MeadworksHeadless.AllowsUi) return null;
            var root = KitPanel.Create(parent, name, new Vector2(Mathf.Max(32, width), 32));
            if (root == null) return null;
            root.anchorMin = root.anchorMax = new Vector2(0, 0);
            root.pivot = new Vector2(0, 1);
            KitPanel.Background(root).raycastTarget = false;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            var parentCanvas = parent.GetComponentInParent<Canvas>();
            if (parentCanvas != null) { canvas.sortingLayerID = parentCanvas.sortingLayerID; canvas.sortingOrder = parentCanvas.sortingOrder + 100; }
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            int pad = Mathf.CeilToInt(KitCardLayout.Padding);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = 4;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            root.gameObject.SetActive(false);
            return new KitFloatingCard(root);
        }

        /// <summary>Shows the card beside a screen point (for example <c>Input.mousePosition</c>), kept inside its parent.</summary>
        public void ShowAt(Vector2 screenPoint, Camera eventCamera = null)
        {
            if (disposed || Root == null) return;
            var parent = Root.parent as RectTransform;
            if (parent == null) return;
            Root.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(Root);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, eventCamera, out var local)) return;
            var bounds = parent.rect;
            var topLeft = KitCardLayout.FloatTopLeft(KitUnity.Vector(local), KitUnity.Vector(Root.rect.size), KitUnity.Rect(bounds), KitSkin.PanelOverhang);
            // Anchors sit at the parent's bottom-left corner, so the anchored position is measured from it.
            Root.anchoredPosition = KitUnity.Vector(topLeft) - bounds.min;
        }

        public void Hide() { if (!disposed && Root != null) Root.gameObject.SetActive(false); }

        public void Dispose() { if (disposed) return; disposed = true; if (Root != null) UnityEngine.Object.Destroy(Root.gameObject); }
    }
}
