// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// The under-inventory dock at run time (rules and tests in Crucible.Core: <see cref="KitUnderInventory"/>). It lays
    /// the registered entries out when the inventory opens, when an entry asks (<see cref="KitUnderInventory.MarkDirty"/>),
    /// and when the player panel, the screen, the container panel (opened, closed, moved or resized) or the screen layout changed. With a
    /// chest open the entries hang under the container panel. Each frame it only compares a few cached numbers;
    /// with no entries registered it does nothing at all, and docked products need no per-frame layout of their own.
    /// </summary>
    public static class KitUnderInventoryDock
    {
        private struct Watch
        {
            public Vector2 RootSize, PlayerPosition, PlayerSize, PlayerScale, ContainerPosition, ContainerSize;
            public int ScreenWidth, ScreenHeight;
            public bool Container, Layout;
            public float CraftingLeft;
        }

        private static readonly Vector3[] corners = new Vector3[4];
        private static bool open;
        private static Watch last;
        private static RectTransform root;
        private static bool subscribed;

        /// <summary>The coordinate root of the dock's rectangles: the player panel's parent while the inventory is open.</summary>
        public static RectTransform Root => open ? root : null;

        /// <summary>Puts a panel's top-left corner at (rect.XMin, rect.YMax) of the dock's coordinates, whatever its parent, anchors and pivot.</summary>
        public static void PlaceTopLeft(RectTransform panel, KitRect rect)
        {
            if (panel == null || root == null || panel.parent == null)
                return;
            Vector3 world = root.TransformPoint(new Vector3(root.rect.xMin + rect.XMin, root.rect.yMin + rect.YMax, 0f));
            Vector3 local = panel.parent.InverseTransformPoint(world);
            Vector2 size = Vector2.Scale(panel.rect.size, new Vector2(Mathf.Abs(panel.localScale.x), Mathf.Abs(panel.localScale.y)));
            Vector3 at = new Vector3(local.x + panel.pivot.x * size.x, local.y - (1f - panel.pivot.y) * size.y, panel.localPosition.z);
            if ((panel.localPosition - at).sqrMagnitude > 0.0025f)
                panel.localPosition = at;
            // A panel whose background (its Bkg) reaches past its rect tells the dock, which keeps that frame clear.
            Image back = KitSkin.Background(panel);
            if (back == null || !back.gameObject.activeInHierarchy)
                return;
            KitRect inner = InRoot(panel), drawn = InRoot(back.rectTransform);
            KitUnderInventory.Registry.ReportFrame(Mathf.Max(Mathf.Max(inner.XMin - drawn.XMin, drawn.XMax - inner.XMax), Mathf.Max(inner.YMin - drawn.YMin, drawn.YMax - inner.YMax)));
        }

        internal static void Update()
        {
            KitUnderInventoryRegistry registry = KitUnderInventory.Registry;
            if (!registry.HasEntries)
            {
                if (open)
                    Close(registry);
                return;
            }
            if (!subscribed)
            {
                subscribed = true;
                KitInventoryScreen.RegionsChanged += KitUnderInventory.MarkDirty;
                KitLook.Changed += KitUnderInventory.MarkDirty; // the panels' frames change size with the look (10 or 12)
            }
            InventoryGui gui = InventoryGui.instance;
            RectTransform player = gui != null ? gui.m_player : null;
            if (player == null || !InventoryGui.IsVisible() || !(player.parent is RectTransform parent) || !player.gameObject.activeInHierarchy)
            {
                if (open)
                    Close(registry);
                return;
            }
            Watch now = new Watch
            {
                RootSize = parent.rect.size,
                PlayerPosition = player.localPosition,
                PlayerSize = player.rect.size,
                PlayerScale = player.localScale,
                ScreenWidth = Screen.width,
                ScreenHeight = Screen.height,
                Container = gui.IsContainerOpen(),
                ContainerPosition = gui.m_container != null ? (Vector2)gui.m_container.localPosition : Vector2.zero,
                ContainerSize = gui.m_container != null ? gui.m_container.rect.size : Vector2.zero,
                Layout = KitInventoryScreen.Active,
                CraftingLeft = KitInventoryScreen.CraftingRegion.XMin,
            };
            if (open && ReferenceEquals(root, parent) && !registry.Dirty && Same(now, last))
                return;
            open = true;
            root = parent;
            last = now;
            registry.Apply(Measure(gui, player, now));
        }

        private static bool Same(Watch a, Watch b) =>
            a.RootSize == b.RootSize && a.PlayerPosition == b.PlayerPosition && a.PlayerSize == b.PlayerSize && a.PlayerScale == b.PlayerScale
            && a.ScreenWidth == b.ScreenWidth && a.ScreenHeight == b.ScreenHeight && a.Container == b.Container && a.ContainerPosition == b.ContainerPosition && a.ContainerSize == b.ContainerSize && a.Layout == b.Layout
            && a.CraftingLeft == b.CraftingLeft;

        private static KitUnderInventoryRoom Measure(InventoryGui gui, RectTransform player, Watch now)
        {
            // The drawn frames, not the rects: the backgrounds reach 10-12 units past them, so a row placed by rect starts inside the chest frame.
            KitRect playerRect = Drawn(player);
            // With a chest open the stack hangs under the container panel instead of covering it; the floor stays the key hints.
            KitRect? containerRect = null;
            if (now.Container && gui.m_container != null && gui.m_container.gameObject.activeInHierarchy)
                containerRect = Drawn(gui.m_container);
            float right = root.rect.width;
            if (now.Layout && now.CraftingLeft > InRoot(player).XMax)
                right = Mathf.Min(right, now.CraftingLeft - KitScreenRules.DefaultSideGap);
            return KitUnderInventoryRoom.Under(playerRect, containerRect, KitScreenRules.DefaultBottomClear, 0f, right);
        }

        // World corners, so a panel the screen layout scaled is measured as drawn.
        private static KitRect InRoot(RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            Vector2 low = ToRoot(corners[0]), high = ToRoot(corners[2]);
            return KitRect.MinMax(low.x, low.y, high.x, high.y);
        }

        // A panel with its background frame: the Bkg's corners, or the rect grown by the skin's overhang when it has none.
        private static KitRect Drawn(RectTransform panel)
        {
            KitRect rect = InRoot(panel);
            Image back = KitSkin.Background(panel);
            if (back != null && back.gameObject.activeInHierarchy)
            {
                KitRect bkg = InRoot(back.rectTransform);
                return KitRect.MinMax(Mathf.Min(rect.XMin, bkg.XMin), Mathf.Min(rect.YMin, bkg.YMin), Mathf.Max(rect.XMax, bkg.XMax), Mathf.Max(rect.YMax, bkg.YMax));
            }
            float scale = root.lossyScale.y != 0f ? Mathf.Abs(panel.lossyScale.y / root.lossyScale.y) : 1f;
            float o = KitSkin.PanelOverhang * scale;
            return KitRect.MinMax(rect.XMin - o, rect.YMin - o, rect.XMax + o, rect.YMax + o);
        }

        private static Vector2 ToRoot(Vector3 world)
        {
            Vector3 local = root.InverseTransformPoint(world);
            return new Vector2(local.x - root.rect.xMin, local.y - root.rect.yMin);
        }

        private static void Close(KitUnderInventoryRegistry registry)
        {
            open = false;
            root = null;
            registry.HideAll();
        }
    }
}
