// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// The inventory screen's region layout at run time (rules and tests in Crucible.Core: <see cref="KitScreenRules"/>,
    /// <see cref="KitScreenRegistry"/>). While the inventory is open it measures the grid panel, plans the regions for the
    /// screen, puts the game's crafting panel and character header into their column at the right and places the panels
    /// products registered with <see cref="KitScreen.Register"/> (the Extras dock and the other regions). It starts from the
    /// grid's native position and leaves the grid and its container in place. Switched off by the "Meadworks Screen Layout" look setting.
    /// </summary>
    public static class KitInventoryScreen
    {
        private static bool wasVisible;
        private static KitScreenPlan plan;
        private static RectTransform root;
        private static RectTransform crafting;
        private static RectTransform info;
        private static bool scaledPlayer;
        private static KitVector lastCrafting;
        private static float appliedScale = 1f;
        // Planned every frame while open: the plan is reused while the measured input is unchanged, and the corners buffer is kept.
        private static readonly KitScreenPlanner planner = new KitScreenPlanner();
        private static readonly Vector3[] corners = new Vector3[4];

        /// <summary>
        /// Raised when the crafting region changed size (a new screen, a product reported another size): the owner of the crafting
        /// panel lays it out again. Handlers must not throw.
        /// </summary>
        public static event Action RegionsChanged;

        /// <summary>The layout drives the crafting panel and the header right now.</summary>
        public static bool Active { get; private set; }

        /// <summary>The plan's scale (1 on a roomy screen).</summary>
        public static float Scale => Active && plan != null ? plan.Scale : 1f;

        /// <summary>The last plan; null before the inventory first opened.</summary>
        public static KitScreenPlan Plan => plan;

        /// <summary>The rectangle crafting may fill (canvas units, y up from the screen's bottom left); empty when the layout is not active.</summary>
        public static KitRect CraftingRegion => Active && plan != null ? plan.Rect(KitRegion.Crafting) : new KitRect(0f, 0f, 0f, 0f);

        /// <summary>The coordinate root of the regions: the parent of the player panel.</summary>
        public static RectTransform Root => root;

        /// <summary>The layout positions this panel (the crafting panel or the header) and its owner should not.</summary>
        public static bool Owns(RectTransform panel) => Active && panel != null && (panel == crafting || panel == info);

        internal static void Update()
        {
            InventoryGui gui = InventoryGui.instance;
            bool visible = gui != null && gui.m_player != null && InventoryGui.IsVisible();
            if (!visible || !KitLook.ScreenLayoutOn)
            {
                if (wasVisible || Active)
                    Close(gui);
                wasVisible = false;
                return;
            }
            wasVisible = true;
            root = gui.m_player.parent as RectTransform;
            if (root == null || root.rect.width < 1f || root.rect.height < 1f)
                return;
            crafting = gui.m_crafting;
            info = gui.m_infoPanel as RectTransform;
            plan = planner.Plan(Measure(gui));
            appliedScale = plan.Scale;
            Active = true;
            KitRect craft = plan.Rect(KitRegion.Crafting);
            if (Mathf.Abs(craft.Width - lastCrafting.X) > 1f || Mathf.Abs(craft.Height - lastCrafting.Y) > 1f)
            {
                lastCrafting = new KitVector(craft.Width, craft.Height);
                RaiseChanged();
            }
            PlaceGamePanel(crafting, plan.Rect(KitRegion.Crafting), plan.Scale);
            PlaceGamePanel(info, plan.Rect(KitRegion.Header), plan.Scale);
            ScalePlayer(gui.m_player, plan.Scale);
            KitScreen.Registry.Apply(plan);
        }

        private static void RaiseChanged()
        {
            try { RegionsChanged?.Invoke(); }
            catch (Exception error) { CrucibleUiPlugin.Log?.Warning("Screen layout handler failed: " + error.GetType().Name + ": " + error.Message); }
        }

        private static KitScreenInput Measure(InventoryGui gui)
        {
            // The panels' own (unscaled) sizes: the plan's scale is applied to the panels afterwards, so measuring what is drawn would
            // feed the scale back into the next plan. Only where the grid starts is read from the screen.
            gui.m_player.GetWorldCorners(corners);
            // Where the grid starts as if the last plan's scale were not on it (the scale is applied around the panel's pivot).
            Vector3 pivot = gui.m_player.position;
            float last = Mathf.Max(0.01f, appliedScale);
            Vector2 low = ToRoot(pivot + (corners[0] - pivot) / last), high = ToRoot(pivot + (corners[2] - pivot) / last);
            KitScreenRegistry registry = KitScreen.Registry;
            KitScreenInput input = registry.Input(root.rect.width, root.rect.height, new KitVector(Mathf.Max(1f, gui.m_player.rect.width), Mathf.Max(1f, gui.m_player.rect.height)));
            input.LeftMargin = low.x;
            input.TopMargin = root.rect.height - high.y;
            if (!registry.TryReported(KitRegion.Crafting, out _) && crafting != null)
                input.Crafting = new KitVector(crafting.rect.width, crafting.rect.height);
            if (!registry.TryReported(KitRegion.Header, out _) && info != null)
                input.HeaderHeight = info.rect.height;
            return input;
        }

        /// <summary>A world point in the root's coordinates, origin at the root's bottom left.</summary>
        private static Vector2 ToRoot(Vector3 world)
        {
            Vector3 local = root.InverseTransformPoint(world);
            return new Vector2(local.x - root.rect.xMin, local.y - root.rect.yMin);
        }

        private static void PlaceGamePanel(RectTransform panel, KitRect region, float scale)
        {
            if (panel == null || !Owns(panel))
                return;
            if (!Mathf.Approximately(panel.localScale.x, scale))
                panel.localScale = new Vector3(scale, scale, 1f);
            PlaceCorner(panel, new Vector2(1f, 1f), region.XMax, region.YMax);
        }

        private static void ScalePlayer(RectTransform player, float scale)
        {
            if (scale < 1f)
            {
                scaledPlayer = true;
                if (!Mathf.Approximately(player.localScale.x, scale))
                    player.localScale = new Vector3(scale, scale, 1f);
            }
            else if (scaledPlayer)
            {
                scaledPlayer = false;
                player.localScale = Vector3.one;
            }
        }

        private static void Close(InventoryGui gui)
        {
            Active = false;
            appliedScale = 1f;
            lastCrafting = KitVector.Zero;
            KitScreen.Registry.HideAll();
            if (scaledPlayer && gui != null && gui.m_player != null)
                gui.m_player.localScale = Vector3.one;
            scaledPlayer = false;
        }

        /// <summary>Puts a panel's top-left corner at (rect.XMin, rect.YMax) of the layout's coordinates, whatever the panel's parent, anchors and pivot.</summary>
        public static void PlaceTopLeft(RectTransform panel, KitRect rect) => PlaceCorner(panel, new Vector2(0f, 1f), rect.XMin, rect.YMax);

        /// <summary>Puts a panel's top-right corner at (rect.XMax, rect.YMax).</summary>
        public static void PlaceTopRight(RectTransform panel, KitRect rect) => PlaceCorner(panel, new Vector2(1f, 1f), rect.XMax, rect.YMax);

        private static void PlaceCorner(RectTransform panel, Vector2 corner, float x, float y)
        {
            if (panel == null || root == null || panel.parent == null)
                return;
            Vector3 world = root.TransformPoint(new Vector3(root.rect.xMin + x, root.rect.yMin + y, 0f));
            Vector3 local = panel.parent.InverseTransformPoint(world);
            Vector2 size = Vector2.Scale(panel.rect.size, new Vector2(Mathf.Abs(panel.localScale.x), Mathf.Abs(panel.localScale.y)));
            Vector3 at = new Vector3(local.x - (corner.x - panel.pivot.x) * size.x, local.y - (corner.y - panel.pivot.y) * size.y, panel.localPosition.z);
            if ((panel.localPosition - at).sqrMagnitude > 0.0025f)
                panel.localPosition = at;
        }
    }
}
