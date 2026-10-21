// Adopted from OpenKeep 2.0.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: OpenKeep/OpenKeep/src/Stow/PanelButtons.cs at 31d8ee35ac4ccfd14c83aa930e4b602ac4dfa230.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: OpenKeep renamed to Burrow and the trash can removed (tools/adopt-storage.py);
// sizes come from Burrow.Look.ButtonRowStyle and every button takes the Meadworks kit look
// (Crucible KitButton: lit face, warm caption, kit caption sizes) through Burrow.Look.ButtonLook, keeping the
// game's gamepad key and navigation.
// Changed by expDARE, 2026-10-10 (parity choice #9): the trash can is back on its own plate (TrashPlate), including
// Mantle's stat column; if the plate does not fit, it joins the buttons.
// Changed by expDARE, 2026-10-09: the buttons step aside while a third-party stack/sort mod is loaded (Burrow.Integration.StowRowYield,
// C09) and a button already on the panel under its name is reused instead of cloned again.
// Changed by expDARE, 2026-10-12 (user decision, parity rows 3/4): the four buttons left the row under the player grid and
// stand in the armour/weight column, stacked under the weight plate; the PackPanel strip follow, the screen clamp and
// Button Row Offset went with the row.
// Changed by expDARE, 2026-10-12 21:37 (user decision): no Place stacks in the crafting header; it duplicated Quick stack.
// Changed by expDARE, 2026-10-13 (user decisions 20:19, 22:35, 22:41): without an inventory layout owner the row is one entry
// of Crucible's under-inventory dock (KitUnderInventory, StowRowOrder), placed only when the dock lays out; the column's
// floor is measured again only when the panel changed or at 5 Hz (W68: Follow cost 0.66 ms a frame).
// Changed by expDARE, 2026-10-17: the controls remain with the player inventory when a chest opens; Sort handles both inventories.
using System;
using System.Collections.Generic;
using HarmonyLib;
using Burrow.Core;
using Burrow.Look;
using ExtraPotions.Burrow.Embedded.PatchGuard;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Burrow.Stow
{
    /// <summary>
    /// The panel buttons: <c>Quick stack</c>, <c>Store all</c>, <c>Top up</c> and <c>Sort</c>, stacked in the player panel's
    /// armour/weight column directly under the weight plate, as wide as that plate; any other box another mod seats in the
    /// column under the weight plate stays above them, so nothing is covered. Without Mantle or PackPanel the game's
    /// column leaves no room under the weight plate, so they stand in one row under the player panel. With a container open they
    /// stay with the player panel, using its column or the gap above the chest. Nothing is added to the crafting header. The trash can
    /// gets its own plate under the armour readout (<see cref="TrashPlate"/>); when that fails or with
    /// <c>Trash Can On Stat Column</c> off it ends the stack. Every button is a clone of the game's take-all button, so
    /// style, font and gamepad selection are the game's.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    public static class PanelButtons
    {
        private const float Height = ButtonRowStyle.Height;
        private const string Prefix = "Burrow_";
        private const string TrashName = Prefix + "trashcan";

        private static readonly List<RectTransform> Placed = new List<RectTransform>();
        private static readonly List<RectTransform> scratch = new List<RectTransform>();
        private static readonly Vector3[] corners = new Vector3[4];

        /// <summary>The dock entry id of the row (no inventory layout owner).</summary>
        public const string DockId = "burrow.stow_row";
        private static IDisposable dockEntry;
        private static RectTransform dockPanel;
        private static bool rowMode, rowWanted;
        private static int rowCount = -1;
        private static RectTransform weightPanel, weightPlate;

        /// <summary>
        /// Every frame of the inventory's update (<see cref="StowHotkeys"/>): while the weight plate is shown. Stacked under it
        /// with a layout owner. Without one, only Burrow leaves the shared dock when a chest opens and stays under the player panel.
        /// </summary>
        public static void Follow(InventoryGui gui)
        {
            RectTransform panel = gui.m_player;
            if (panel == null)
                return;
            if (weightPanel != panel || weightPlate == null)
            {
                weightPanel = panel;
                weightPlate = panel.Find("Weight") as RectTransform;
            }
            RectTransform weight = weightPlate;
            bool show = weight != null && panel.gameObject.activeInHierarchy
                && (weight.gameObject.activeInHierarchy || gui.IsContainerOpen());
            Placed.RemoveAll(rect => rect == null);
            if (!PackPanelLink.LaysOutInventory && !gui.IsContainerOpen())
            {
                Dock(panel, show);
                return;
            }
            if (rowMode)
                Undock();
            foreach (RectTransform rect in Placed)
                if (rect.parent == panel && rect.gameObject.activeSelf != show)
                    rect.gameObject.SetActive(show);
            if (show)
            {
                if (PackPanelLink.LaysOutInventory)
                    Stack(panel, weight);
                else
                    PlayerRow(panel, gui.m_container);
            }
        }

        private static void PlayerRow(RectTransform panel, RectTransform container)
        {
            int count = Count(panel), index = 0;
            float top = ButtonRowStyle.RowTop(panel.rect.yMin, Local(panel, container).yMax);
            foreach (RectTransform rect in Placed)
            {
                if (rect.parent != panel)
                    continue;
                rect.sizeDelta = new Vector2(ButtonRowStyle.RowWidth, ButtonRowStyle.RowHeight);
                rect.localPosition = new Vector3(ButtonRowStyle.RowCenter(panel.rect.center.x, index++, count), top, 0f);
            }
        }

        /// <summary>Row mode: one dock entry; the dock places it when it lays out, so a frame here only compares flags.</summary>
        private static void Dock(RectTransform panel, bool show)
        {
            rowMode = true;
            dockPanel = panel;
            if (dockEntry == null)
                dockEntry = KitUnderInventory.Register(DockId, KitUnderInventory.StowRowOrder, RowSize, PlaceRow, () => rowMode && rowWanted, HideRow);
            int count = Count(panel);
            if (show != rowWanted || count != rowCount)
            {
                rowWanted = show;
                rowCount = count;
                KitUnderInventory.MarkDirty();
            }
        }

        /// <summary>A layout owner appeared: the row leaves the dock (row mode off first, so the dock's hide leaves the column alone).</summary>
        private static void Undock()
        {
            rowMode = false;
            rowWanted = false;
            rowCount = -1;
            dockEntry?.Dispose();
            dockEntry = null;
        }

        private static int Count(RectTransform panel)
        {
            int count = 0;
            foreach (RectTransform rect in Placed)
                if (rect.parent == panel)
                    count++;
            return count;
        }

        private static KitVector RowSize()
        {
            int count = Math.Max(rowCount, 0);
            return new KitVector(count * ButtonRowStyle.RowWidth + Math.Max(0, count - 1) * ButtonRowStyle.Gap, ButtonRowStyle.RowHeight);
        }

        /// <summary>The dock's rectangle for the row: the buttons left to right in it, each <see cref="ButtonRowStyle.RowWidth"/> wide.</summary>
        private static void PlaceRow(KitRect row)
        {
            if (!rowMode || dockPanel == null)
                return;
            Vector2 size = new Vector2(ButtonRowStyle.RowWidth, ButtonRowStyle.RowHeight);
            int index = 0;
            foreach (RectTransform rect in Placed)
            {
                if (rect == null || rect.parent != dockPanel)
                    continue;
                if (!rect.gameObject.activeSelf)
                    rect.gameObject.SetActive(true);
                if (rect.sizeDelta != size)
                    rect.sizeDelta = size;
                KitUnderInventoryDock.PlaceTopLeft(rect, new KitRect(row.XMin + index++ * (ButtonRowStyle.RowWidth + ButtonRowStyle.Gap), row.YMin, ButtonRowStyle.RowWidth, ButtonRowStyle.RowHeight));
            }
        }

        private static void HideRow()
        {
            if (!rowMode)
                return;
            foreach (RectTransform rect in Placed)
                if (rect != null && rect.parent == dockPanel && rect.gameObject.activeSelf)
                    rect.gameObject.SetActive(false);
        }

        /// <summary>
        /// The captions in the game language, rewritten inside the game's language switch (<see cref="LanguageSwitch"/>), so
        /// they change in the same frame as the game's own texts.
        /// </summary>
        private static void Recaption(Localization localization)
        {
            InventoryGui gui = InventoryGui.instance;
            RectTransform panel = gui != null ? gui.m_player : null;
            if (panel == null)
                return;
            const string words = Prefix + "burrow_stow_";
            // The words again for the language now selected, so the buttons and the game's dictionary agree whatever ran first.
            Language.Install(localization);
            foreach (RectTransform rect in Placed)
            {
                TMP_Text caption = rect.parent == panel && rect.name.StartsWith(words, System.StringComparison.Ordinal)
                    ? rect.GetComponentInChildren<TMP_Text>(true) : null;
                if (caption != null)
                    caption.text = Language.Localize("$" + rect.name.Substring(Prefix.Length));
            }
        }

        [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
        private static class LanguageSwitch
        {
            [HarmonyPostfix]
            private static void Postfix(Localization __instance) => Guard.Run("stow captions", () => Recaption(__instance));
        }

        private static Rect lastPlate;
        private static Vector2 lastPanelSize;
        private static int lastChildren = -1, lastActive = -1, lastCount = -1;
        private static float lastFloor, nextFloor;

        /// <summary>
        /// The buttons top to bottom from under the weight plate, or under the lowest box seated below it. The floor
        /// (<see cref="Floor"/>, a walk over every child of the panel) is measured again only when the plate, the panel's size,
        /// its direct children or their active states, or the button count changed, and at most 0.2 s apart otherwise
        /// (a box nested deeper may come or go); the buttons move only when a value differs.
        /// </summary>
        private static void Stack(RectTransform panel, RectTransform weight)
        {
            Rect plate = Local(panel, weight);
            if (plate.width <= 0f)
                return;
            int count = Count(panel);
            int children = panel.childCount, active = 0;
            for (int i = 0; i < children; i++)
                if (panel.GetChild(i).gameObject.activeSelf)
                    active = active * 31 + i + 1;
            float now = Time.unscaledTime;
            if (plate != lastPlate || panel.rect.size != lastPanelSize || children != lastChildren || active != lastActive
                || count != lastCount || now >= nextFloor)
            {
                lastPlate = plate;
                lastPanelSize = panel.rect.size;
                lastChildren = children;
                lastActive = active;
                lastCount = count;
                nextFloor = now + 0.2f;
                lastFloor = Floor(panel, weight, plate);
            }
            float floor = lastFloor;
            // Rectangles as wide as the plate, as tall as the room down to the panel's bottom allows (up to square).
            float height = ButtonRowStyle.ColumnHeight(floor - panel.rect.yMin, count, plate.width);
            for (int i = 0; i < Placed.Count; i++)
            {
                RectTransform rect = Placed[i];
                if (rect.parent != panel)
                    continue;
                Vector2 size = new Vector2(plate.width, height);
                if (rect.sizeDelta != size)
                    rect.sizeDelta = size;
                Vector3 at = new Vector3(plate.center.x, ButtonRowStyle.StackTop(floor, i, height), 0f);
                if ((rect.localPosition - at).sqrMagnitude > 0.0001f)
                    rect.localPosition = at;
            }
        }

        /// <summary>
        /// The weight plate's bottom, lowered past every other box in the column whose top is at or under it and within the
        /// stack's reach (Mantle's free-slot plate, another mod's plate), so the stack never covers one.
        /// </summary>
        private static float Floor(RectTransform panel, RectTransform weight, Rect plate)
        {
            RectTransform container = InventoryGui.instance != null ? InventoryGui.instance.m_container : null;
            float floor = plate.yMin;
            float reach = ButtonRowStyle.StackHeight(Placed.Count, plate.width);
            panel.GetComponentsInChildren(false, scratch);
            bool lowered = true;
            while (lowered)
            {
                lowered = false;
                foreach (RectTransform other in scratch)
                {
                    if (other == panel || other.IsChildOf(weight) || (container != null && other.IsChildOf(container)) || Ours(other))
                        continue;
                    Rect box = Local(panel, other);
                    if (box.height <= 0f || box.height > plate.height * 1.5f || box.xMax <= plate.xMin || box.xMin >= plate.xMax)
                        continue;
                    if (!ButtonRowStyle.IsPlayerColumnBox(box.yMin, panel.rect.yMin))
                        continue;
                    if (box.yMax > floor + 0.5f || box.yMax < floor - reach || box.yMin >= floor)
                        continue;
                    floor = box.yMin;
                    lowered = true;
                }
            }
            scratch.Clear();
            return floor;
        }

        private static bool Ours(Transform other)
        {
            foreach (RectTransform rect in Placed)
                if (other.IsChildOf(rect))
                    return true;
            return false;
        }

        /// <summary><paramref name="rect"/>'s corners in <paramref name="panel"/>'s local space.</summary>
        private static Rect Local(RectTransform panel, RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            Vector3 a = panel.InverseTransformPoint(corners[0]);
            Vector3 b = panel.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        [HarmonyPostfix]
        public static void Postfix(InventoryGui __instance)
        {
            Guard.Run("stow panel buttons", () => Create(__instance));
        }

        public static void Create(InventoryGui gui)
        {
            if (gui == null || gui.m_takeAllButton == null || gui.m_player == null || gui.m_container == null)
                return;
            // A loaded stack/sort mod brings its own buttons; Burrow adds none and says so once.
            if (Burrow.Integration.StowRowYield.SteppedAside)
            {
                Burrow.Integration.StowRowYield.Report();
                return;
            }
            // PackPanel and Mantle seat the named Trash plate between armour and weight.
            Add(gui, gui.m_player, StowWords.QuickStack, StowActions.QuickStack);
            Add(gui, gui.m_player, StowWords.StoreAll, StowActions.StoreAll);
            Add(gui, gui.m_player, StowWords.TopUp, TopUp.Run);
            Add(gui, gui.m_player, StowWords.Sort, Sorting.SortPlayerAndOpenContainer);
            bool onPlate = StowSettings.TrashOnStatColumn.Value
                && TrashPlate.TryCreate(gui, () => TrashMode.CanClicked(gui));
            if (!onPlate)
                AddTrash(gui, gui.m_player);
        }

        private static void Add(InventoryGui gui, RectTransform panel, string word, Action action)
        {
            string name = Prefix + word.TrimStart('$');
            // A second Awake on the same panel reuses the button instead of stacking a clone on it.
            Transform existing = panel.Find(name);
            GameObject go = existing != null ? existing.gameObject : UnityEngine.Object.Instantiate(gui.m_takeAllButton.gameObject, panel);
            go.name = name;
            go.SetActive(true);
            Place((RectTransform)go.transform);
            Button button = go.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => Guard.Run(word, action));
            }
            ButtonLook.Apply(go, Language.Localize(word));
            // The column is narrow: a caption may take two lines rather than shrink below the kit's sizes.
            TMP_Text caption = go.GetComponentInChildren<TMP_Text>(true);
            if (caption != null)
            {
                caption.textWrappingMode = TextWrappingModes.Normal;
                // The game button's caption box is inset 5 units each way: too low for two lines on a short button.
                RectTransform box = caption.rectTransform;
                box.anchorMin = Vector2.zero;
                box.anchorMax = Vector2.one;
                box.offsetMin = new Vector2(ButtonRowStyle.ColumnCaptionInsetX, ButtonRowStyle.ColumnCaptionInsetY);
                box.offsetMax = new Vector2(-ButtonRowStyle.ColumnCaptionInsetX, -ButtonRowStyle.ColumnCaptionInsetY);
                caption.margin = Vector4.zero;
            }
        }

        /// <summary>Centre-top pivot so <see cref="Stack"/> sets each button's top edge; size and place follow every frame.</summary>
        private static void Place(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(ButtonRowStyle.Width, Height);
            rect.localScale = Vector3.one;
            if (!Placed.Contains(rect))
                Placed.Add(rect);
        }

        private static void AddTrash(InventoryGui gui, RectTransform panel)
        {
            // Not "Burrow_trash": the PlateColumn library adopts an object of that name (the pre-library trash plate)
            // into its column of boxes.
            Transform existing = panel.Find(TrashName);
            GameObject go = existing != null ? existing.gameObject : UnityEngine.Object.Instantiate(gui.m_takeAllButton.gameObject, panel);
            go.name = TrashName;
            go.SetActive(true);
            Place((RectTransform)go.transform);
            foreach (TMP_Text text in go.GetComponentsInChildren<TMP_Text>(true))
                text.gameObject.SetActive(false);
            Image icon = go.transform.Find("icon") is Transform found ? found.GetComponent<Image>() : BinIcon(go.transform);
            Burrow.Integration.TrashCanMode.Track(go, icon);
            Button button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => Guard.Run("trash can", () => TrashMode.CanClicked(gui)));
            ExtraPotions.Meadworks.Crucible.UI.KitForged.Button(button);
        }

        /// <summary>The bin inside the vanilla button frame; the frame keeps the game's hover and press states.</summary>
        private static Image BinIcon(Transform button)
        {
            GameObject go = new GameObject("icon", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(button, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
            Image image = go.GetComponent<Image>();
            image.sprite = StowSprites.Bin;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }
    }
}
