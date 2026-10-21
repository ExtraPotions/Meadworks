// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (parity choices #19 and #59): the chest preview panel. ChestPreview 1.0.2 has no license, so only its
// behavior (a panel of icons and counts while you look at a chest, sorting, combining, columns, position and alpha) is
// followed; the rows come from Burrow.Core ChestPreviewRules and the look from the Crucible.UI kit panel.
using System;
using System.Collections.Generic;
using System.Text;
using Burrow.Core;
using ExtraPotions.Burrow;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Burrow.Capacity
{
    /// <summary>
    /// While the local player looks at a container they could open (the same access check as the hover text, so dungeon
    /// and world loot chests are shown as well, the user's call on #59), a kit panel lists its contents: an icon, the
    /// name with its quality and the count per row, sorted and combined as set, over up to three columns, with the used
    /// slots beside the chest's name and "+ n more" below. Hidden while the inventory, a menu, the map, chat, the console
    /// or a text field is open, and when the HUD is hidden. Rebuilt only when the chest, its contents or a setting change.
    /// </summary>
    public static class ChestPreview
    {
        // Solo's panel at 1280x720 was 200 x 161 px for a title and six rows (300 x 241 HUD units). The text is about a fifth
        // smaller since the playtest of 2026-10-20 (rows 20 -> 16, title 22 -> 18); rows, title and icons shrink with it.
        private const float RowHeight = 25f;
        private const float Pad = 8f;
        private const float TitleHeight = 32f;
        private const float IconSize = 21f;
        private const float IconGap = 6f;
        private const float RowText = 16f;
        private const float TitleSize = 18f;
        // Solo's colors, read from its capture: a cream-gold title, near-white rows, black at 40 % behind them.
        private static readonly Color TitleColour = new Color(247f / 255f, 229f / 255f, 172f / 255f);
        private static readonly Color RowColour = new Color(247f / 255f, 244f / 255f, 215f / 255f);

        private static RectTransform panel;
        private static Image background;
        private static CanvasGroup group;
        private static TMP_Text title;
        private static TMP_Text more;
        private static readonly List<GameObject> rowObjects = new List<GameObject>();
        private static string signature = "";

        /// <summary>The container the panel shows now, or null.</summary>
        public static Container Shown { get; private set; }

        /// <summary>The panel is on screen.</summary>
        public static bool Visible => panel != null && panel.gameObject.activeSelf && Shown != null;

        /// <summary>The rows' texts as drawn (localized), in order; the title is <see cref="TitleText"/>.</summary>
        public static readonly List<string> Lines = new List<string>();

        /// <summary>Rows that show an icon.</summary>
        public static int Icons { get; private set; }

        /// <summary>Rows left out by Max Entries.</summary>
        public static int Hidden { get; private set; }

        /// <summary>Columns drawn.</summary>
        public static int ColumnCount { get; private set; }

        public static string TitleText => title != null ? title.text : "";

        /// <summary>The panel shows this container now (the hover text then leaves its content lines out).</summary>
        public static bool Shows(Container container) => container != null && Visible && ReferenceEquals(Shown, container);

        // The on/off switch lives in the settings (Chest Preview / Enabled, shown in the F8 menu and Almanac); the toggle key stays.
        public static void Initialize()
        {
            PreviewSettings.Enabled.SettingChanged += (_, _) => signature = "";
        }

        public static void Shutdown()
        {
            Hide();
            if (panel != null)
                UnityEngine.Object.Destroy(panel.gameObject);
            panel = null;
        }

        static string StateText() => global::Burrow.Integration.StowRowYield.Text(PreviewSettings.Enabled.Value ? "chest_preview_on" : "chest_preview_off");

        /// <summary>The toggle key.</summary>
        public static void Toggle()
        {
            PreviewSettings.Enabled.Value = !PreviewSettings.Enabled.Value;
            Messages.TopLeft(StateText());
        }

        /// <summary>Every frame (Plugin.Update).</summary>
        public static void Tick()
        {
            if (Keys.Pressed(PreviewSettings.ToggleKey))
                Toggle();
            Container container = Target();
            if (container == null)
            {
                Hide();
                return;
            }
            if (!Ensure())
                return;
            string now = Signature(container);
            if (!ReferenceEquals(container, Shown) || now != signature)
                Build(container, now);
            panel.gameObject.SetActive(true);
            Place();
        }

        /// <summary>The container looked at, when the panel may show it; null otherwise.</summary>
        private static Container Target()
        {
            Player player = Player.m_localPlayer;
            if (!PreviewSettings.Enabled.Value || player == null || Hud.instance == null || Hud.IsUserHidden())
                return null;
            if (InventoryGui.IsVisible() || Menu.IsVisible() || Minimap.IsOpen() || Keys.TextInputActive || StoreGui.IsVisible() || TextViewer.IsShowingIntro())
                return null;
            GameObject hovered = player.GetHoverObject();
            Container container = hovered != null ? hovered.GetComponentInParent<Container>() : null;
            if (container == null || !HoverText.CanSee(container))
                return null;
            Inventory inventory = container.GetInventory();
            return inventory != null && ChestPreviewRules.ShowsChest(inventory.NrOfItems(), PreviewSettings.HideEmpty.Value) ? container : null;
        }

        private static bool Ensure()
        {
            if (panel != null)
                return true;
            Transform parent = Hud.instance != null && Hud.instance.m_rootObject != null ? Hud.instance.m_rootObject.transform : null;
            if (parent == null)
                return false;
            // A plain dark translucent box as Solo draws it, not a kit wood board.
            GameObject go = new GameObject("Burrow_ChestPreview", typeof(RectTransform), typeof(Image));
            panel = (RectTransform)go.transform;
            panel.SetParent(parent, false);
            panel.sizeDelta = new Vector2(PreviewSettings.ColumnWidth.Value, 120f);
            background = go.GetComponent<Image>();
            background.sprite = null;
            background.raycastTarget = false;
            group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            title = KitCanvas.Text(panel, "Title", "", TitleSize, TitleColour, TextAlignmentOptions.Center, false);
            more = KitCanvas.Text(panel, "More", "", RowText, KitUnity.Colour(KitPalette.TabDimText), TextAlignmentOptions.Left, false);
            panel.gameObject.SetActive(false);
            return true;
        }

        private static void Hide()
        {
            Shown = null;
            if (panel != null && panel.gameObject.activeSelf)
                panel.gameObject.SetActive(false);
        }

        /// <summary>What the panel depends on: the chest's revision and items, the language and every setting of the section.</summary>
        private static string Signature(Container container)
        {
            Inventory inventory = container.GetInventory();
            StringBuilder text = new StringBuilder();
            text.Append(container.GetInstanceID()).Append('|').Append(inventory.GetWidth()).Append('x').Append(inventory.GetHeight()).Append('|');
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                text.Append(item.m_shared.m_name).Append(':').Append(item.m_stack).Append(':').Append(item.m_quality).Append(':').Append(item.m_variant).Append(';');
            text.Append('|').Append(PreviewSettings.SortBy.Value).Append(PreviewSettings.SortDescending.Value).Append(PreviewSettings.CombineStacks.Value)
                .Append(PreviewSettings.Columns.Value).Append(PreviewSettings.MaxEntries.Value).Append(PreviewSettings.ShowItemIcons.Value)
                .Append(PreviewSettings.ShowQuality.Value).Append(PreviewSettings.ColumnWidth.Value).Append(PreviewSettings.CapacityFormat.Value)
                .Append(PreviewSettings.BackgroundAlpha.Value).Append(Localization.instance != null ? Localization.instance.GetSelectedLanguage() : "");
            return text.ToString();
        }

        private static void Build(Container container, string now)
        {
            Shown = container;
            signature = now;
            foreach (GameObject row in rowObjects)
                UnityEngine.Object.Destroy(row);
            rowObjects.Clear();
            Lines.Clear();
            Icons = 0;
            Inventory inventory = container.GetInventory();
            List<PreviewItem> items = new List<PreviewItem>();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item == null || item.m_shared == null)
                    continue;
                string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name;
                // The shown name, so Sort By Name follows the words the player reads (Boar Trophy before Coal), not the $tokens.
                items.Add(new PreviewItem(Language.Localize(item.m_shared.m_name), prefab, item.m_quality, item.m_variant, item.m_stack, item.m_shared.m_value, item.m_shared.m_weight) { Tag = item });
            }
            List<PreviewRow> rows = ChestPreviewRules.Rows(items, PreviewSettings.CombineStacks.Value, PreviewSettings.SortBy.Value,
                PreviewSettings.SortDescending.Value, PreviewSettings.MaxEntries.Value, out int hidden);
            Hidden = hidden;
            int[] columns = ChestPreviewRules.Columns(rows.Count, PreviewSettings.Columns.Value);
            ColumnCount = columns.Length;
            float width = PreviewSettings.ColumnWidth.Value;
            int tallest = 0;
            foreach (int count in columns)
                tallest = Mathf.Max(tallest, count);
            float height = Pad * 2f + TitleHeight + tallest * RowHeight + (hidden > 0 ? RowHeight : 0f) + (rows.Count == 0 ? RowHeight : 0f);
            panel.sizeDelta = new Vector2(width * columns.Length + Pad * 2f, height);

            string name = Language.Localize(container.m_name);
            string capacity = ChestPreviewRules.Capacity(inventory.NrOfItems(), inventory.GetWidth() * inventory.GetHeight(), PreviewSettings.CapacityFormat.Value);
            title.text = capacity.Length > 0 ? name + "  " + capacity : name;
            Rect(title.rectTransform, Pad, Pad, width * columns.Length, TitleHeight);

            int index = 0;
            for (int column = 0; column < columns.Length; column++)
            {
                for (int line = 0; line < columns[column]; line++, index++)
                    AddRow(rows[index], Pad + column * width, Pad + TitleHeight + line * RowHeight, width);
            }
            more.text = hidden > 0 ? "+ " + hidden + " " + Language.Localize("$burrow_more") : rows.Count == 0 ? Language.Localize("$piece_container_empty") : "";
            Rect(more.rectTransform, Pad, Pad + TitleHeight + tallest * RowHeight, width * columns.Length, RowHeight);
            background.color = new Color(0f, 0f, 0f, PreviewSettings.BackgroundAlpha.Value);
        }

        private static void AddRow(PreviewRow row, float x, float y, float width)
        {
            GameObject go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(panel, false);
            rowObjects.Add(go);
            Rect((RectTransform)go.transform, x, y, width, RowHeight);
            float textLeft = 0f;
            ItemDrop.ItemData item = row.First.Tag as ItemDrop.ItemData;
            if (PreviewSettings.ShowItemIcons.Value && item != null && item.GetIcon() != null)
            {
                GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(go.transform, false);
                Image image = icon.GetComponent<Image>();
                image.sprite = item.GetIcon();
                image.preserveAspect = true;
                image.raycastTarget = false;
                Rect((RectTransform)icon.transform, 0f, (RowHeight - IconSize) / 2f, IconSize, IconSize);
                textLeft = IconSize + IconGap;
                Icons++;
            }
            string label = Language.Localize(ChestPreviewRules.Label(row.Name, row.Quality, row.Count, PreviewSettings.ShowQuality.Value));
            TMP_Text text = KitCanvas.Text(go.transform, "Label", label, RowText, RowColour, TextAlignmentOptions.Left, false);
            Rect(text.rectTransform, textLeft, 0f, width - textLeft - 4f, RowHeight);
            KitCanvas.Fit(text);
            Lines.Add(label);
        }

        /// <summary>A child rect placed from the parent's top-left corner.</summary>
        private static void Rect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>Left edge at Position X, middle at Position Y, as fractions of the HUD; Alpha on the whole panel.</summary>
        private static void Place()
        {
            if (!(panel.parent is RectTransform parent))
                return;
            panel.anchorMin = panel.anchorMax = new Vector2(PreviewSettings.PositionX.Value, PreviewSettings.PositionY.Value);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            if (group != null)
                group.alpha = PreviewSettings.Alpha.Value;
        }
    }
}
