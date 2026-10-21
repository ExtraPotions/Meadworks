// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Drag/clamp and canvas ordering adapted from OpenKeep TrackerDrag/TrackerPanel.
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later.
// Reference ad278ecf323901c3645eb1f9f6fac6833a0f8cb1; Changed by expDARE, 2026: shared kit presentation and callback ownership.
using System;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>Movable kit panel on the HUD. Only product-designated graphics catch a free cursor.</summary>
    public sealed class KitHudPanel : IDisposable
    {
        public RectTransform Root { get; }
        public RectTransform Content => Root;
        readonly CanvasGroup group;
        float opacity = 1;
        bool disposed;
        Vector2 automaticPosition;
        bool productVisible, cursorFree;
        float productScale = 1, productOpacity = 1;
        internal Vector2 AutomaticPosition => automaticPosition;
        internal bool ProductVisible => productVisible;
        internal float ProductScale => productScale;
        internal float ProductOpacity => productOpacity;

        KitHudPanel(RectTransform root)
        {
            Root = root;
            group = root.gameObject.AddComponent<CanvasGroup>();
            KitPanel.Background(root).raycastTarget = false;
            KitLook.Changed += Restyle;
        }

        public static KitHudPanel Create(Transform hudRoot, string name, Vector2 size, Vector2 position)
            => Create(hudRoot, name, size, position, "com.meadworks.crucible.ui", name);

        /// <summary>Stable owner/id keeps local layout choices when this owned panel is recreated.</summary>
        public static KitHudPanel Create(Transform hudRoot, string name, Vector2 size, Vector2 position, string owner, string id)
        {
            KitHudLayout.ValidateIdentity(owner, id);
            var root = KitPanel.Create(hudRoot, name, size);
            if (root == null) return null;
            KitForged.SidePlate(root);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0, 1);
            var hud = hudRoot.GetComponentInParent<Canvas>();
            var inventory = InventoryGui.instance != null ? InventoryGui.instance.GetComponentInParent<Canvas>() : null;
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            if (hud != null) canvas.sortingLayerID = hud.sortingLayerID;
            canvas.sortingOrder = (inventory != null ? inventory.sortingOrder : 600) + 50;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var panel = new KitHudPanel(root);
            panel.SetPosition(position);
            KitHudLayout.Register(panel, owner, id);
            return panel;
        }

        public void SetInteractive(Graphic graphic, bool interactive)
        {
            if (graphic != null) graphic.raycastTarget = interactive;
        }

        public void SetPosition(Vector2 position)
        {
            if (disposed || Root == null) return;
            automaticPosition = position;
            KitHudLayout.Apply(this);
        }

        public void Update(bool visible, bool cursorFree, float scale, float backgroundOpacity)
        {
            if (disposed || Root == null) return;
            productVisible = visible;
            this.cursorFree = cursorFree;
            productScale = HudLayoutRules.Finite(scale) ? Mathf.Clamp(scale, .5f, 3) : 1;
            productOpacity = HudLayoutRules.Finite(backgroundOpacity) ? Mathf.Clamp01(backgroundOpacity) : 1;
            KitHudLayout.Apply(this);
        }

        internal void Present(HudLayoutState state, bool editing)
        {
            if (disposed || Root == null) return;
            Root.gameObject.SetActive(productVisible && !state.Hidden);
            group.blocksRaycasts = group.interactable = cursorFree || editing;
            var background = KitPanel.Background(Root);
            if (background != null) background.raycastTarget = editing;
            Root.localScale = Vector3.one * (state.Scale > 0 ? state.Scale : productScale);
            if (!state.Placed) Root.anchoredPosition = new Vector2(automaticPosition.x, -automaticPosition.y);
            var nextOpacity = state.Opacity >= 0 ? state.Opacity : productOpacity;
            if (!Mathf.Approximately(opacity, nextOpacity)) { opacity = nextOpacity; Restyle(); }
            if (background != null && background.GetComponent<CanvasGroup>() is CanvasGroup backgroundGroup)
                backgroundGroup.blocksRaycasts = editing;
        }

        void Restyle()
        {
            if (disposed || Root == null) return;
            KitPanel.Refresh(Root);
            // Isolate the background subtree, including Timber's mesh, from text/control alpha.
            var background = KitPanel.Background(Root);
            if (background == null) return;
            var alpha = background.GetComponent<CanvasGroup>() ?? background.gameObject.AddComponent<CanvasGroup>();
            alpha.alpha = opacity;
            alpha.blocksRaycasts = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            KitHudLayout.Unregister(this);
            KitLook.Changed -= Restyle;
            if (Root != null) { Root.gameObject.SetActive(false); UnityEngine.Object.Destroy(Root.gameObject); }
        }
    }

}
