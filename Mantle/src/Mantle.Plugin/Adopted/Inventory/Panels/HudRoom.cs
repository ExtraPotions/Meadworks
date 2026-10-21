using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Contracts;
using Mantle.Compatibility;
using UnityEngine;
using Registry = ExtraPotions.Meadworks.Crucible.CapabilityRegistry;

namespace Mantle.Panels
{
    /// <summary>
    /// Screen room for the stat column right of the minimap. The game draws the small map 40 units in from the screen's
    /// right edge, less than the column needs, so while the column shows a box and the map is on (small or big) the map
    /// moves left by what is missing, and the status effects left of it move with it so the two never overlap. With no box
    /// shown, the map off (a world without a map, the player's choice) or Mantle off, both go back where they were.
    /// While the row is moved it also wraps before another product's published HUD rectangle (<c>meadworks.hud.occupied</c>,
    /// Cairn's compass plank), using the game's own spacing and the rule Cairn uses, so the two agree when Cairn is installed
    /// and the row stays clear of the plank when it is not wrapped by Cairn. While a window is open (the inventory and
    /// crafting window above all) the map and the row keep the place they had, and the row its wrap (<see cref="StatusRowHold"/>).
    /// A row that would then reach into an open inventory panel is hidden until the inventory closes, never moved. Positions and visibility only,
    /// local only; <see cref="Reset"/> gives the game its layout back when Mantle stops.
    /// </summary>
    public static class HudRoom
    {
        /// <summary>Units between the column's right edge and the screen's.</summary>
        private const float Margin = 10f;

        private static readonly Spot map = new Spot();
        private static readonly Spot effects = new Spot();
        private static readonly Vector3[] corners = new Vector3[4];
        private static IReadOnlyList<KeyValuePair<string, Func<Tuple<bool, float, float, float, float>>>> occupants;
        private static Registry occupantRegistry;
        private static int occupantGeneration = -1;
        private static bool wrapped;
        private static readonly StatusRowHold hold = new StatusRowHold();
        private static float heldShift;
        private static int heldPerRow;
        private static GameObject hiddenRow;

        /// <summary>Moves the map and the status effects for this column (null: no column, both go home).</summary>
        public static void Fit(RectTransform column)
        {
            Minimap minimap = Minimap.instance;
            RectTransform small = minimap != null && minimap.m_smallRoot != null ? minimap.m_smallRoot.transform as RectTransform : null;
            if (small == null) return;
            bool used = column != null && minimap.m_mode != Minimap.MapMode.None && Shows(column);
            float shift = used ? Mathf.Max(0f, Need(column) + map.Home(small)) : 0f;
            // The column hides while the inventory is open; the map and the row keep the room they had so neither jumps.
            bool held = hold.Holding(WindowOpen(), Time.frameCount);
            if (held) shift = heldShift;
            else heldShift = shift;
            map.Shift(small, shift);
            Hud hud = Hud.instance;
            RectTransform list = hud != null ? hud.m_statusEffectListRoot : null;
            if (list == null) return;
            effects.Shift(list, shift);
            KeepClear(hud, shift > 0f, held);
            Hide(hud, list);
        }

        /// <summary>Gives the game its layout back: the map and the row home, the game's wrap, the row shown.</summary>
        public static void Reset()
        {
            Hud hud = Hud.instance;
            Minimap minimap = Minimap.instance;
            RectTransform small = minimap != null && minimap.m_smallRoot != null ? minimap.m_smallRoot.transform as RectTransform : null;
            if (small != null) map.Shift(small, 0f);
            RectTransform list = hud != null ? hud.m_statusEffectListRoot : null;
            if (list != null) effects.Shift(list, 0f);
            if (wrapped && hud != null) Place(hud, Mathf.Max(1, hud.m_effectsPerRow));
            wrapped = false;
            Show();
            hold.Reset();
            heldShift = 0f;
            heldPerRow = 0;
        }

        /// <summary>A window that hides the stat column or the compass plank while it is open (Cairn holds on the same ones).</summary>
        private static bool WindowOpen()
        {
            if (InventoryGui.IsVisible() || Menu.IsVisible() || Minimap.IsOpen()) return true;
            try { return ExtraPotions.Meadworks.Crucible.UI.KitWindows.HudBlocked; }
            catch { return false; }
        }

        /// <summary>
        /// While the inventory is open, hides the row if it reaches into the inventory, container or crafting panel. It stays
        /// hidden until the inventory closes (no flicker while the window animates in or effects come and go) and is then shown
        /// exactly as it was. A row someone else hid is left alone.
        /// </summary>
        private static void Hide(Hud hud, RectTransform list)
        {
            if (!InventoryGui.IsVisible())
            {
                Show();
                return;
            }
            if (hiddenRow != null || !list.gameObject.activeSelf) return;
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || !RowBounds(hud, out float left, out float right, out float bottom, out float top)) return;
            if (Covers(gui.m_player, left, right, bottom, top) || Covers(gui.m_container, left, right, bottom, top) || Covers(gui.m_crafting, left, right, bottom, top))
            {
                hiddenRow = list.gameObject;
                hiddenRow.SetActive(false);
            }
        }

        private static void Show()
        {
            if (hiddenRow != null) hiddenRow.SetActive(true);
            hiddenRow = null;
        }

        /// <summary>The row's screen rectangle: every cell with its icon, name and timer.</summary>
        private static bool RowBounds(Hud hud, out float left, out float right, out float bottom, out float top)
        {
            left = bottom = float.MaxValue;
            right = top = float.MinValue;
            Camera camera = CameraOf(hud.m_statusEffectListRoot);
            bool any = false;
            foreach (RectTransform cell in hud.m_statusEffects)
            {
                if (cell == null || !cell.gameObject.activeSelf) continue;
                any |= Grow(cell, camera, ref left, ref right, ref bottom, ref top);
                for (int i = 0; i < cell.childCount; i++)
                    if (cell.GetChild(i) is RectTransform part && part.gameObject.activeSelf)
                        Grow(part, camera, ref left, ref right, ref bottom, ref top);
            }
            return any;
        }

        private static bool Covers(RectTransform panel, float left, float right, float bottom, float top)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) return false;
            float panelLeft = float.MaxValue, panelBottom = float.MaxValue, panelRight = float.MinValue, panelTop = float.MinValue;
            return Grow(panel, CameraOf(panel), ref panelLeft, ref panelRight, ref panelBottom, ref panelTop) &&
                StatusRowRoom.Overlaps(left, right, bottom, top, panelLeft, panelRight, panelBottom, panelTop);
        }

        private static bool Grow(RectTransform rect, Camera camera, ref float left, ref float right, ref float bottom, ref float top)
        {
            rect.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                left = Mathf.Min(left, point.x);
                right = Mathf.Max(right, point.x);
                bottom = Mathf.Min(bottom, point.y);
                top = Mathf.Max(top, point.y);
            }
            return right > left && top > bottom;
        }

        /// <summary>The camera a canvas draws with; null for an overlay canvas, where world space is screen space.</summary>
        private static Camera CameraOf(Transform target)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        /// <summary>
        /// While Mantle has moved the row: cells per row so the row's last cell stays clear of every published HUD rectangle
        /// in its height band (<see cref="StatusRowRoom"/>). Runs after the game and Cairn place the row this frame and writes
        /// a position only when it differs; once Mantle no longer needs the wrap, the game's placement is written back once.
        /// While <paramref name="held"/>, the count chosen before the window opened stays, whatever the hidden plank reports.
        /// </summary>
        private static void KeepClear(Hud hud, bool moved, bool held)
        {
            List<RectTransform> cells = hud.m_statusEffects;
            int native = Mathf.Max(1, hud.m_effectsPerRow);
            int perRow = native;
            RectTransform root = hud.m_rootObject != null ? hud.m_rootObject.transform as RectTransform : null;
            if (held)
                perRow = heldPerRow > 0 ? Math.Min(heldPerRow, native) : native;
            else if (moved && cells.Count > 1 && root != null && cells[0] != null)
            {
                IReadOnlyList<KeyValuePair<string, Func<Tuple<bool, float, float, float, float>>>> others = Occupants();
                if (others.Count > 0)
                {
                    // The first cell never moves (row 0, column 0); its center and the game's spacing give every other cell.
                    cells[0].GetWorldCorners(corners);
                    Vector3 low = root.InverseTransformPoint(corners[0]), high = root.InverseTransformPoint(corners[2]);
                    float center = (low.x + high.x) * .5f - root.rect.xMin;
                    float top = root.rect.yMax - Mathf.Max(low.y, high.y);
                    float unit = root.lossyScale.x != 0f ? hud.m_statusEffectListRoot.lossyScale.x / root.lossyScale.x : 1f;
                    float cell = hud.m_statusEffectSpacing * unit;
                    for (int i = 0; i < others.Count; i++)
                    {
                        if (HudOccupiedContract.TryRead(others[i].Value, out bool shown, out float left, out float y, out float width, out float height) && shown)
                            perRow = Math.Min(perRow, StatusRowRoom.PerRow(native, center, cell, top, top + cell, left, left + width, y, y + height));
                    }
                }
            }
            if (!held) heldPerRow = perRow;
            if (perRow == native && !wrapped) return;
            Place(hud, perRow);
            wrapped = perRow != native;
        }

        /// <summary>The game's own placement (<c>Hud.UpdateStatusEffects</c>), with <paramref name="perRow"/> cells per row.</summary>
        private static void Place(Hud hud, int perRow)
        {
            List<RectTransform> cells = hud.m_statusEffects;
            for (int i = 0; i < cells.Count; i++)
            {
                RectTransform cell = cells[i];
                if (cell == null) continue;
                int row = i / perRow, column = i - row * perRow;
                Vector2 wanted = new Vector2(-4f - column * hud.m_statusEffectSpacing, -row * hud.m_statusEffectSpacing);
                if (cell.anchoredPosition != wanted) cell.anchoredPosition = wanted;
            }
        }

        /// <summary>Other products' HUD rectangles, resolved again only when the capability registry changes.</summary>
        private static IReadOnlyList<KeyValuePair<string, Func<Tuple<bool, float, float, float, float>>>> Occupants()
        {
            try
            {
                if (!ExtraPotions.Meadworks.Crucible.Plugin.IsReady)
                    return Array.Empty<KeyValuePair<string, Func<Tuple<bool, float, float, float, float>>>>();
                Registry registry = ExtraPotions.Meadworks.Crucible.Plugin.Services.Capabilities;
                if (occupants == null || !ReferenceEquals(registry, occupantRegistry) || registry.Generation != occupantGeneration)
                {
                    occupants = registry.ResolveEach<Func<Tuple<bool, float, float, float, float>>>(HudOccupiedContract.Id, HudOccupiedContract.ContractVersion);
                    occupantRegistry = registry;
                    occupantGeneration = registry.Generation;
                }
                return occupants;
            }
            catch
            {
                return Array.Empty<KeyValuePair<string, Func<Tuple<bool, float, float, float, float>>>>();
            }
        }

        /// <summary>From the map's right edge to the screen's: the gap, the column's width on screen and the margin.</summary>
        private static float Need(RectTransform column) =>
            column.anchoredPosition.x + column.rect.width * column.localScale.x + Margin;

        private static bool Shows(Transform column)
        {
            for (int i = 0; i < column.childCount; i++)
                if (column.GetChild(i).gameObject.activeSelf) return true;
            return false;
        }

        /// <summary>
        /// Where one piece of the game's HUD sat before it was moved: its x, anchored to the screen's right edge (negative,
        /// the room to its right), read the first time the piece is seen, again for a new one after a new world loads.
        /// </summary>
        private sealed class Spot
        {
            private RectTransform rect;
            private float home;

            public float Home(RectTransform target)
            {
                if (rect != target)
                {
                    rect = target;
                    home = target.anchoredPosition.x;
                }
                return home;
            }

            public void Shift(RectTransform target, float shift)
            {
                float x = Home(target) - shift;
                if (!Mathf.Approximately(target.anchoredPosition.x, x))
                    target.anchoredPosition = new Vector2(x, target.anchoredPosition.y);
            }
        }
    }
}
