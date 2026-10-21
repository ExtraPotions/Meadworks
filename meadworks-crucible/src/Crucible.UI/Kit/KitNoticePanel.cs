// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// MeadWorks notice stack. Timber or Brown cards, newest at the top, each fading out on its own timer.
    /// This object is ours: it does not restyle MessageHud or any other mod's UI. While the inventory, map, pause
    /// menu, or a MeadWorks window is open, the cards hide and their timers wait. The stack stays off the compass, the
    /// status row and the hotbar. When that gap is shorter than the stack, fewer cards show and the rest wait.
    /// </summary>
    public static class KitNoticePanel
    {
        public const string Section = "Notices";
        const float CardWidth = 360f, CardHeight = 46f, Gap = 6f, Margin = 8f, PlaceInterval = 0.5f;

        public static ConfigEntry<bool>? StackedNotices { get; private set; }
        public static ConfigEntry<KitNoticeCorner>? Position { get; private set; }
        public static ConfigEntry<float>? DurationSetting { get; private set; }
        public static ConfigEntry<int>? MaximumVisible { get; private set; }

        sealed class CardView
        {
            internal RectTransform Root;
            internal Image Icon;
            internal TMP_Text Body;
            internal TMP_Text Count;
            internal CanvasGroup Fade;
            internal string BodyCache = "";
            internal string CountCache = "";
            internal float Alpha = -1f;
        }

        static readonly KitNoticeStack stack = new KitNoticeStack();
        static readonly Dictionary<int, Sprite> icons = new Dictionary<int, Sprite>();
        static readonly CardView[] views = new CardView[KitNoticeStack.MaxVisibleLimit];
        static readonly List<Rect> screenBlocks = new List<Rect>(16);
        static readonly List<KitRect> blocks = new List<KitRect>(16);
        static RectTransform root;
        static HotkeyBar hotbar;
        static bool layoutDirty = true;
        static bool heldLast;
        static int appliedRevision = -1, appliedMax = -1, screenW, screenH, obstacleSignature;
        static int fitCards = KitNoticeStack.MaxVisibleLimit;
        static float fitWidth = CardWidth;
        static float nextPlace;
        static string languageSeen = "";

        internal static void Bind(ConfigFile config)
        {
            if (StackedNotices != null || config == null) return;
            StackedNotices = MeadworksConfig.Bind(config, "Notices", "Stacked Notices", true,
                "Show MeadWorks notices in a stack. Off uses the game's own message. Local to this player, drawn by Crucible UI, and the server does not override it. Takes effect at once.");
            Position = MeadworksConfig.Bind(config, "Notices", "Position", KitNoticeCorner.TopLeft,
                "Corner of the screen for the MeadWorks notice stack: TopLeft, TopRight, BottomLeft or BottomRight. The stack stays clear of the compass and the status effect row. Local to this player. Takes effect at once.");
            DurationSetting = MeadworksConfig.Bind(config, "Notices", "Duration", KitNoticeStack.DefaultDuration,
                new ConfigDescription("How long each MeadWorks notice stays, in seconds. The same notice again restarts this time and raises its count. Local to this player. Applies to the next notice.",
                    new AcceptableValueRange<float>(KitNoticeStack.MinDuration, KitNoticeStack.MaxDuration)));
            MaximumVisible = MeadworksConfig.Bind(config, "Notices", "Maximum Visible", KitNoticeStack.DefaultMaxVisible,
                new ConfigDescription("How many MeadWorks notices show at once. The newest sits at the top. Further notices wait until a space opens. Local to this player. Takes effect at once.",
                    new AcceptableValueRange<int>(KitNoticeStack.MinVisible, KitNoticeStack.MaxVisibleLimit)));
            StackedNotices.SettingChanged += SettingChanged;
            Position.SettingChanged += SettingChanged;
            DurationSetting.SettingChanged += SettingChanged;
            MaximumVisible.SettingChanged += SettingChanged;
        }

        /// <summary>Queues a TopLeft or Center notice. False leaves the caller on the game's own message.</summary>
        internal static bool TryShow(KitNotice notice)
        {
            if (notice == null || StackedNotices == null || !StackedNotices.Value || KitCanvas.Front == null) return false;
            if (notice.Kind != KitNoticeKind.TopLeft && notice.Kind != KitNoticeKind.Center) return false;
            int id = stack.Push(notice.Kind, notice.RawText, notice.Title, notice.Amount, notice.Icon != null, Duration, Maximum, notice.ShowWhenHidden);
            if (notice.Icon != null) icons[id] = notice.Icon;
            layoutDirty = true;
            return true;
        }

        public static void Tick()
        {
            if (!CrucibleUiPlugin.Active) return;
            if (stack.Idle && (root == null || !root.gameObject.activeSelf)) return;
            bool hold = Held();
            bool screen = false, due = false;
            if (!hold)
            {
                screen = ScreenChanged();
                due = PlaceDue();
            }
            bool opened = heldLast && !hold;
            heldLast = hold;
            int previousCards = fitCards;
            float previousWidth = fitWidth;
            if (!hold && (layoutDirty || screen || due || opened)) Measure();
            bool fitChanged = fitCards != previousCards || Math.Abs(fitWidth - previousWidth) > 0.01f;
            int max = Maximum;
            int space = fitCards;
            bool changed = stack.Tick(Time.unscaledDeltaTime, hold, max, space);
            DropIcons();
            if (stack.Idle || StackedNotices == null || !StackedNotices.Value || hold || space <= 0)
            {
                if (space <= 0 && !hold) layoutDirty = false;
                Hide();
                return;
            }
            if (!EnsureRoot()) return;
            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
            if (changed || layoutDirty || stack.Revision != appliedRevision || max != appliedMax || screen || due || opened || fitChanged)
            {
                appliedRevision = stack.Revision;
                appliedMax = max;
                layoutDirty = false;
                Apply();
            }
            else if (stack.Fading)
                Fade();
        }

        internal static void Reset()
        {
            stack.Clear();
            icons.Clear();
            layoutDirty = true;
            appliedRevision = -1;
            appliedMax = -1;
            obstacleSignature = 0;
            languageSeen = "";
            heldLast = false;
            fitCards = KitNoticeStack.MaxVisibleLimit;
            fitWidth = CardWidth;
            hotbar = null;
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            root = null;
            for (int i = 0; i < views.Length; i++) views[i] = null;
        }

        static void SettingChanged(object sender, EventArgs args)
        {
            layoutDirty = true;
            if (StackedNotices != null && !StackedNotices.Value)
            {
                stack.Clear();
                icons.Clear();
                Hide();
            }
        }

        static bool Held()
        {
            if (InventoryGui.IsVisible() || Menu.IsVisible() || Minimap.IsOpen() || KitWindows.HudBlocked) return true;
            return Hud.IsUserHidden() && !stack.ShownWhileHidden;
        }

        static float Duration => DurationSetting == null ? KitNoticeStack.DefaultDuration : DurationSetting.Value;
        static int Maximum => MaximumVisible == null ? KitNoticeStack.DefaultMaxVisible : MaximumVisible.Value;
        static KitNoticeCorner Corner => Position == null ? KitNoticeCorner.TopLeft : Position.Value;

        static void DropIcons()
        {
            var gone = stack.Removed;
            for (int i = 0; i < gone.Count; i++) icons.Remove(gone[i]);
        }

        static void Hide()
        {
            if (root != null && root.gameObject.activeSelf) root.gameObject.SetActive(false);
        }

        static bool ScreenChanged()
        {
            int w = Screen.width, h = Screen.height;
            if (w == screenW && h == screenH) return false;
            screenW = w;
            screenH = h;
            return true;
        }

        static bool PlaceDue()
        {
            if (Time.unscaledTime < nextPlace) return false;
            nextPlace = Time.unscaledTime + PlaceInterval;
            string language = CrucibleUiPlugin.Language ?? "";
            bool languageChanged = language != languageSeen;
            if (languageChanged)
            {
                languageSeen = language;
                for (int i = 0; i < views.Length; i++)
                    if (views[i] != null) views[i].BodyCache = "";
            }
            return languageChanged || ObstaclesChanged();
        }

        static bool EnsureRoot()
        {
            if (root != null) return true;
            var parent = KitCanvas.Front as RectTransform;
            if (parent == null) return false;
            var go = new GameObject("Crucible_NoticeStack", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            root = (RectTransform)go.transform;
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 1f);
            var group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            root.SetAsLastSibling();
            return true;
        }

        static CardView View(int index)
        {
            if (views[index] != null) return views[index];
            var card = KitPanel.Create(root, "Notice", new Vector2(CardWidth, CardHeight));
            if (card == null) return null;
            card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            var background = KitPanel.Background(card);
            if (background != null) background.raycastTarget = false;
            var fade = card.gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = false;
            fade.interactable = false;
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.layer = card.gameObject.layer;
            iconObject.transform.SetParent(card, false);
            var icon = iconObject.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            Move(icon.rectTransform, new Vector2(8f, -7f), new Vector2(32f, 32f));
            var body = KitCanvas.Text(card, "Text", "", 16f, KitUnity.Colour(KitPalette.SheetLabel), TextAlignmentOptions.MidlineLeft, false);
            body.overflowMode = TextOverflowModes.Truncate;
            var count = KitCanvas.Text(card, "Count", "", 16f, KitUnity.Colour(KitPalette.TabLitText), TextAlignmentOptions.MidlineRight, false);
            count.overflowMode = TextOverflowModes.Truncate;
            iconObject.SetActive(false);
            return views[index] = new CardView { Root = card, Icon = icon, Body = body, Count = count, Fade = fade };
        }

        static void Measure()
        {
            float scale = Scale();
            int cards = KitNoticeStack.ClampVisible(Maximum);
            if (Screen.width < 2 || Screen.height < 2)
            {
                fitCards = cards;
                fitWidth = CardWidth;
                return;
            }
            Collect(blocks);
            float desired = (cards * CardHeight + (cards - 1) * Gap) * scale;
            var slot = KitNoticePlacement.Place(Corner, Screen.width, Screen.height, CardWidth * scale, desired, blocks, Margin);
            fitCards = KitNoticePlacement.CardsThatFit(slot.Height, CardHeight * scale, Gap * scale, cards);
            float width = slot.Width / scale;
            if (float.IsNaN(width) || float.IsInfinity(width) || width > CardWidth) width = CardWidth;
            if (width < 1f) width = 1f;
            fitWidth = width;
        }

        static float Scale()
        {
            Transform source = root != null ? root : KitCanvas.Front;
            if (source == null) return 1f;
            float scale = source.lossyScale.x;
            return scale < 0.01f ? 1f : scale;
        }

        static void Apply()
        {
            int count = stack.ShownCount;
            float width = fitWidth;
            if (width > CardWidth) width = CardWidth;
            if (width < 1f) width = 1f;
            float y = 0f;
            var cardSize = new Vector2(width, CardHeight);
            for (int i = 0; i < count; i++)
            {
                var view = View(i);
                if (view == null) return;
                if (!view.Root.gameObject.activeSelf) view.Root.gameObject.SetActive(true);
                var pos = new Vector2(0f, -y);
                if (view.Root.anchoredPosition != pos) view.Root.anchoredPosition = pos;
                if (view.Root.sizeDelta != cardSize) view.Root.sizeDelta = cardSize;
                string body = Line(stack.TitleAt(i), KitNotices.Localize(stack.TextAt(i)));
                if (view.BodyCache != body) { view.BodyCache = body; view.Body.text = body; }
                int badge = stack.BadgeAt(i);
                string badgeText = badge > 1 ? "\u00d7" + badge.ToString(CultureInfo.InvariantCulture) : "";
                if (view.CountCache != badgeText) { view.CountCache = badgeText; view.Count.text = badgeText; }
                icons.TryGetValue(stack.IdAt(i), out var icon);
                bool showIcon = icon != null;
                if (view.Icon.sprite != icon) view.Icon.sprite = icon;
                if (view.Icon.gameObject.activeSelf != showIcon) view.Icon.gameObject.SetActive(showIcon);
                float textX = showIcon ? 46f : 12f;
                float textW = width - textX - 72f;
                if (textW < 1f) textW = 1f;
                Move(view.Body.rectTransform, new Vector2(textX, -7f), new Vector2(textW, 32f));
                float countX = width - 68f;
                if (countX < 1f) countX = 1f;
                Move(view.Count.rectTransform, new Vector2(countX, -7f), new Vector2(56f, 32f));
                float alpha = stack.OpacityAt(i);
                if (!Mathf.Approximately(view.Alpha, alpha)) { view.Alpha = alpha; view.Fade.alpha = alpha; }
                y += CardHeight + Gap;
            }
            for (int i = count; i < views.Length; i++)
                if (views[i] != null && views[i].Root != null && views[i].Root.gameObject.activeSelf) views[i].Root.gameObject.SetActive(false);
            float height = count == 0 ? 0f : y - Gap;
            var size = new Vector2(width, height);
            if (root.sizeDelta != size) root.sizeDelta = size;
            PlaceRoot();
        }

        static void Fade()
        {
            int count = stack.ShownCount;
            for (int i = 0; i < count; i++)
            {
                var view = views[i];
                if (view == null || view.Fade == null) continue;
                float alpha = stack.OpacityAt(i);
                if (Mathf.Approximately(view.Alpha, alpha)) continue;
                view.Alpha = alpha;
                view.Fade.alpha = alpha;
            }
        }

        static void PlaceRoot()
        {
            var parent = root.parent as RectTransform;
            if (parent == null) return;
            float scale = root.lossyScale.x;
            if (scale < 0.01f) scale = 1f;
            float pixelW = root.rect.width * scale;
            float pixelH = root.rect.height * scale;
            if (pixelW < 1f) pixelW = CardWidth * scale;
            if (pixelH < 1f) pixelH = CardHeight * scale;
            Collect(blocks);
            var place = KitNoticePlacement.Place(Corner, Screen.width, Screen.height, pixelW, pixelH, blocks, Margin);
            var canvas = parent.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(place.X, place.Y + place.Height), camera, out var local)) return;
            var anchored = local - parent.rect.min;
            if (root.anchoredPosition != anchored) root.anchoredPosition = anchored;
        }

        static bool ObstaclesChanged()
        {
            Collect(blocks);
            int signature = blocks.Count;
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                signature = unchecked(signature * 31 + block.X.GetHashCode() + block.Y.GetHashCode() + block.Width.GetHashCode() + block.Height.GetHashCode());
            }
            if (signature == obstacleSignature) return false;
            obstacleSignature = signature;
            return true;
        }

        static void Collect(List<KitRect> into)
        {
            screenBlocks.Clear();
            into.Clear();
            KitHudLayout.ShownScreenRects(screenBlocks);
            var hud = Hud.instance;
            // m_statusEffects is private on this game build. The public list root is the status row.
            var row = hud != null ? hud.m_statusEffectListRoot : null;
            if (row != null && row.gameObject.activeInHierarchy)
            {
                if (KitHudLayout.ScreenRect(row, out var rowRect))
                    screenBlocks.Add(rowRect);
                for (int i = 0; i < row.childCount; i++)
                {
                    if (row.GetChild(i) is RectTransform cell
                        && cell.gameObject.activeInHierarchy
                        && KitHudLayout.ScreenRect(cell, out var rect))
                        screenBlocks.Add(rect);
                }
            }
            if (hotbar == null && hud != null) hotbar = hud.GetComponentInChildren<HotkeyBar>(true);
            if (hotbar != null)
            {
                var bar = hotbar.transform;
                for (int i = 0; i < bar.childCount; i++)
                    if (bar.GetChild(i) is RectTransform slot && slot.gameObject.activeInHierarchy && KitHudLayout.ScreenRect(slot, out var rect))
                        screenBlocks.Add(rect);
            }
            for (int i = 0; i < screenBlocks.Count; i++) into.Add(KitUnity.Rect(screenBlocks[i]));
        }

        static string Line(string title, string text)
        {
            if (string.IsNullOrEmpty(title)) return text ?? "";
            if (string.IsNullOrEmpty(text)) return title;
            return title + ": " + text;
        }

        static void Move(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            var corner = new Vector2(0f, 1f);
            if (rect.anchorMin != corner) rect.anchorMin = corner;
            if (rect.anchorMax != corner) rect.anchorMax = corner;
            if (rect.pivot != corner) rect.pivot = corner;
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
            if (rect.sizeDelta != size) rect.sizeDelta = size;
        }
    }
}
