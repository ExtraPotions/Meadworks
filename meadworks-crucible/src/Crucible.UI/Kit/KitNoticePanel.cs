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
    /// status row, the hotbar and the game's own unlock and notice cards. A card wraps to two lines, measured in the
    /// game font, and grows to that text; "..." is used only when a third line would be needed. When the gap is
    /// shorter than the stack, fewer cards show and the rest wait.
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
        static readonly List<Rect> nativeRects = new List<Rect>(8);
        static readonly List<KitRect> blocks = new List<KitRect>(16);
        static RectTransform root;
        static TMP_Text probe;
        static float oneLineHeight;
        static HotkeyBar hotbar;
        static bool layoutDirty = true;
        static bool heldLast;
        static int appliedRevision = -1, appliedMax = -1, screenW, screenH, obstacleSignature;
        static int fitCards = KitNoticeStack.MaxVisibleLimit;
        static float fitWidth = CardWidth;
        static float fitSlot = 10000f;
        static int nativeSignature;
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
            float previousSlot = fitSlot;
            bool notices = !hold && NativeNoticesMoved();
            if (!hold && (layoutDirty || screen || due || opened || notices)) Measure();
            bool fitChanged = fitCards != previousCards || Math.Abs(fitWidth - previousWidth) > 0.01f || Math.Abs(fitSlot - previousSlot) > 0.5f;
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
            if (changed || layoutDirty || stack.Revision != appliedRevision || max != appliedMax || screen || due || opened || fitChanged || notices)
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
            nativeSignature = 0;
            languageSeen = "";
            heldLast = false;
            fitCards = KitNoticeStack.MaxVisibleLimit;
            fitWidth = CardWidth;
            fitSlot = 10000f;
            hotbar = null;
            if (probe != null) { UnityEngine.Object.Destroy(probe.gameObject); probe = null; }
            oneLineHeight = 0f;
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
            var body = KitCanvas.Text(card, "Text", "", 16f, KitUnity.Colour(KitPalette.SheetLabel), TextAlignmentOptions.MidlineLeft, true);
            body.overflowMode = TextOverflowModes.Truncate;
            body.maxVisibleLines = KitNoticeText.MaxLines;
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
                fitSlot = 10000f;
                return;
            }
            int n = Math.Min(cards, stack.HeldCount);
            if (n < 1)
            {
                fitCards = cards;
                fitWidth = CardWidth;
                fitSlot = 10000f;
                return;
            }
            var heights = new float[n];
            Fill(n, CardWidth, heights, null);
            Collect(blocks);
            var slot = PlaceHeights(scale, CardWidth, heights);
            float width = slot.Width / scale;
            if (float.IsNaN(width) || float.IsInfinity(width) || width > CardWidth) width = CardWidth;
            if (width < 1f) width = 1f;
            if (width < CardWidth - 0.5f)
            {
                Fill(n, width, heights, null);
                slot = PlaceHeights(scale, width, heights);
            }
            var pixels = new float[n];
            for (int i = 0; i < n; i++) pixels[i] = heights[i] * scale;
            fitCards = KitNoticePlacement.PrefixThatFits(slot.Height, pixels, Gap * scale);
            fitSlot = slot.Height / scale;
            if (float.IsNaN(fitSlot) || fitSlot < 0f) fitSlot = 0f;
            fitWidth = width;
        }

        static KitRect PlaceHeights(float scale, float width, float[] heights)
        {
            float desired = KitNoticePlacement.StackHeight(heights, heights.Length, Gap) * scale;
            return KitNoticePlacement.Place(Corner, Screen.width, Screen.height, width * scale, desired, blocks, Margin);
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
            var heights = new float[count];
            var lines = new string[count];
            Fill(count, width, heights, lines);
            int draw = KitNoticePlacement.PrefixThatFits(fitSlot, heights, Gap);
            float y = 0f;
            for (int i = 0; i < draw; i++)
            {
                var view = View(i);
                if (view == null) return;
                if (!view.Root.gameObject.activeSelf) view.Root.gameObject.SetActive(true);
                var pos = new Vector2(0f, -y);
                if (view.Root.anchoredPosition != pos) view.Root.anchoredPosition = pos;
                float cardH = heights[i];
                var cardSize = new Vector2(width, cardH);
                if (view.Root.sizeDelta != cardSize) view.Root.sizeDelta = cardSize;
                var reading = KitCanvas.Font;
                if (reading != null && view.Body.font != reading) view.Body.font = reading;
                string body = lines[i] ?? "";
                if (view.BodyCache != body) { view.BodyCache = body; view.Body.text = body; }
                int lineCount = cardH > KitNoticeText.OneLineCard + 0.5f || body.IndexOf('\n') >= 0 ? 2 : 1;
                var align = lineCount > 1 ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
                if (view.Body.alignment != align) view.Body.alignment = align;
                int badge = stack.BadgeAt(i);
                string badgeText = badge > 1 ? "\u00d7" + badge.ToString(CultureInfo.InvariantCulture) : "";
                if (view.CountCache != badgeText) { view.CountCache = badgeText; view.Count.text = badgeText; }
                icons.TryGetValue(stack.IdAt(i), out var icon);
                bool showIcon = icon != null;
                if (view.Icon.sprite != icon) view.Icon.sprite = icon;
                if (view.Icon.gameObject.activeSelf != showIcon) view.Icon.gameObject.SetActive(showIcon);
                float textX = showIcon ? 46f : 12f;
                float box = cardH - KitNoticeText.VerticalPad;
                if (box < 1f) box = KitNoticeText.TextBoxHeight(lineCount);
                Move(view.Body.rectTransform, new Vector2(textX, -7f), new Vector2(TextWidth(width, showIcon), box));
                float countX = width - 68f;
                if (countX < 1f) countX = 1f;
                Move(view.Count.rectTransform, new Vector2(countX, -7f), new Vector2(56f, 32f));
                float alpha = stack.OpacityAt(i);
                if (!Mathf.Approximately(view.Alpha, alpha)) { view.Alpha = alpha; view.Fade.alpha = alpha; }
                y += cardH + Gap;
            }
            for (int i = draw; i < views.Length; i++)
                if (views[i] != null && views[i].Root != null && views[i].Root.gameObject.activeSelf) views[i].Root.gameObject.SetActive(false);
            float height = draw == 0 ? 0f : y - Gap;
            var size = new Vector2(width, height);
            if (root.sizeDelta != size) root.sizeDelta = size;
            PlaceRoot();
        }

        static void Fill(int count, float width, float[] heights, string[] lines)
        {
            for (int i = 0; i < count; i++)
            {
                string body = Line(stack.TitleHeld(i), KitNotices.Localize(stack.RawHeld(i)));
                bool icon = stack.IconHeld(i);
                var laid = Lay(body, TextWidth(width, icon));
                heights[i] = KitNoticeText.VerticalPad + laid.Box;
                if (lines != null) lines[i] = laid.Text;
            }
        }

        static float TextWidth(float cardWidth, bool icon)
        {
            float textX = icon ? 46f : 12f;
            float room = cardWidth - textX - 72f;
            return room < 1f ? 1f : room;
        }

        readonly struct LaidNotice
        {
            public LaidNotice(string text, int lines, float box)
            {
                Text = text ?? "";
                Lines = lines < 1 ? 1 : lines;
                Box = box < 1f ? KitNoticeText.TextBoxHeight(Lines) : box;
            }

            public string Text { get; }
            public int Lines { get; }
            public float Box { get; }
        }

        /// <summary>Line count from the notice font. With no font yet, every character counts as one em so wide text still wraps.</summary>
        static LaidNotice Lay(string text, float width)
        {
            var fontProbe = Probe();
            if (fontProbe == null)
            {
                var fit = KitNoticeText.Fit(text, width, KitNoticeText.FontSize, FallbackWidth);
                return new LaidNotice(fit.Text, fit.Lines, KitNoticeText.TextBoxHeight(fit.Lines));
            }
            return FitMeasured(fontProbe, text, width);
        }

        static float FallbackWidth(string text) => string.IsNullOrEmpty(text) ? 0f : text.Length * KitNoticeText.FontSize;

        static TMP_Text Probe()
        {
            var font = KitCanvas.Font;
            if (probe != null)
            {
                if (font != null && probe.font != font) { probe.font = font; oneLineHeight = 0f; }
                probe.fontSize = KitNoticeText.FontSize;
                return probe;
            }
            var parent = KitCanvas.Front as RectTransform;
            if (parent == null) return null;
            probe = KitCanvas.Text(parent, "NoticeMeasure", "", KitNoticeText.FontSize, Color.white, TextAlignmentOptions.TopLeft, true);
            if (probe == null) return null;
            probe.overflowMode = TextOverflowModes.Overflow;
            probe.maxVisibleLines = 64;
            probe.raycastTarget = false;
            var hide = probe.gameObject.AddComponent<CanvasGroup>();
            hide.alpha = 0f;
            hide.blocksRaycasts = false;
            hide.interactable = false;
            var rect = probe.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(-10000f, -10000f);
            if (font != null) probe.font = font;
            return probe;
        }

        static LaidNotice FitMeasured(TMP_Text fontProbe, string text, float width)
        {
            int lines = CountLines(fontProbe, text, width);
            string shown;
            int shownLines;
            if (lines > KitNoticeText.MaxLines)
            {
                shown = Ellipsize(fontProbe, text, width);
                shownLines = CountLines(fontProbe, shown, width);
                if (shownLines > KitNoticeText.MaxLines) shownLines = KitNoticeText.MaxLines;
            }
            else if (lines > 1)
            {
                string joined = JoinMeasured(fontProbe, lines);
                shown = joined.IndexOf('\n') >= 0 ? joined : (text ?? "");
                shownLines = KitNoticeText.MaxLines;
            }
            else
            {
                shown = text ?? "";
                shownLines = 1;
            }
            float preferred = fontProbe.GetPreferredValues(shown, width, 0f).y;
            return new LaidNotice(shown, shownLines, BoxHeight(shownLines, preferred));
        }

        static float BoxHeight(int lines, float preferred)
        {
            float formula = KitNoticeText.TextBoxHeight(lines);
            float cap = KitNoticeText.FontSize * 4f * lines;
            if (float.IsNaN(preferred) || float.IsInfinity(preferred) || preferred <= formula || preferred > cap)
                return formula;
            return preferred;
        }

        static int CountLines(TMP_Text fontProbe, string text, float width)
        {
            if (string.IsNullOrEmpty(text)) return 1;
            var rect = fontProbe.rectTransform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width < 1f ? 1f : width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 800f);
            fontProbe.textWrappingMode = TextWrappingModes.Normal;
            fontProbe.overflowMode = TextOverflowModes.Overflow;
            fontProbe.maxVisibleLines = 64;
            fontProbe.text = text;
            fontProbe.ForceMeshUpdate(true);
            var info = fontProbe.textInfo;
            int count = info == null ? 1 : info.lineCount;
            while (count > 1 && info.lineInfo[count - 1].characterCount <= 0) count--;
            if (count < 1) count = 1;
            float one = OneLine(fontProbe);
            fontProbe.textWrappingMode = TextWrappingModes.Normal;
            float all = fontProbe.GetPreferredValues(text, width, 0f).y;
            if (count == 1 && one > 1f && !float.IsNaN(all) && !float.IsInfinity(all) && all > one * 1.25f)
            {
                int fromHeight = Mathf.RoundToInt(all / one);
                if (fromHeight >= 2 && fromHeight <= 8) count = fromHeight;
            }
            fontProbe.textWrappingMode = TextWrappingModes.Normal;
            fontProbe.text = text;
            fontProbe.ForceMeshUpdate(true);
            return count;
        }

        static float OneLine(TMP_Text fontProbe)
        {
            if (oneLineHeight > 1f) return oneLineHeight;
            fontProbe.textWrappingMode = TextWrappingModes.NoWrap;
            float latin = fontProbe.GetPreferredValues("Ag").y;
            float wide = fontProbe.GetPreferredValues("\u6728").y;
            float height = latin > wide ? latin : wide;
            if (float.IsNaN(height) || float.IsInfinity(height) || height < 1f) return 0f;
            oneLineHeight = height;
            return height;
        }

        const string Dots = "...";

        static string Ellipsize(TMP_Text fontProbe, string text, float width)
        {
            int lo = 0, hi = text.Length, best = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (CountLines(fontProbe, Candidate(text, mid), width) <= KitNoticeText.MaxLines)
                {
                    best = mid;
                    lo = mid + 1;
                }
                else hi = mid - 1;
            }
            int atWord = WordBoundary(text, best);
            if (atWord < best && CountLines(fontProbe, WordAfter(text, atWord), width) <= 1)
                best = atWord;
            string chosen = Candidate(text, best);
            int lines = CountLines(fontProbe, chosen, width);
            if (lines > KitNoticeText.MaxLines) lines = KitNoticeText.MaxLines;
            if (lines <= 1) return chosen;
            return JoinMeasured(fontProbe, lines);
        }

        static string WordAfter(string text, int boundary)
        {
            int i = boundary;
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            return start >= text.Length ? "" : text.Substring(start, i - start);
        }

        static int WordBoundary(string text, int count)
        {
            if (count <= 0 || count >= text.Length) return count;
            if (char.IsWhiteSpace(text[count - 1])) return count;
            for (int i = count - 1; i > 0; i--)
                if (char.IsWhiteSpace(text[i])) return i;
            return count;
        }

        static string Candidate(string text, int count)
        {
            if (count <= 0) return Dots;
            if (count > text.Length) count = text.Length;
            if (count == text.Length) return text;
            string prefix = text.Substring(0, count).TrimEnd();
            return prefix.Length == 0 ? Dots : prefix + Dots;
        }

        static string JoinMeasured(TMP_Text fontProbe, int lines)
        {
            var info = fontProbe.textInfo;
            string raw = fontProbe.text ?? "";
            if (info == null || lines <= 1 || raw.Length == 0) return raw;
            int take = lines < info.lineCount ? lines : info.lineCount;
            if (take > KitNoticeText.MaxLines) take = KitNoticeText.MaxLines;
            string first = Slice(info, raw, 0);
            if (take < 2) return first.Length > 0 ? first : raw;
            string second = Slice(info, raw, 1);
            if (second.Length == 0) return first.Length > 0 ? first : raw;
            if (first.Length == 0) return second;
            return first + "\n" + second;
        }

        static string Slice(TMP_TextInfo info, string raw, int lineIndex)
        {
            if (info == null || lineIndex < 0 || lineIndex >= info.lineCount) return "";
            var line = info.lineInfo[lineIndex];
            if (line.characterCount <= 0) return "";
            int from = line.firstCharacterIndex;
            int to = line.lastCharacterIndex;
            if (from < 0 || to < from || to >= info.characterCount) return "";
            int start = info.characterInfo[from].index;
            int end = info.characterInfo[to].index;
            if (start < 0 || end < start || start >= raw.Length) return "";
            if (end >= raw.Length) end = raw.Length - 1;
            return raw.Substring(start, end - start + 1).TrimEnd(' ', '\t', '\r', '\n');
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
            KitNativeNotices.AppendShownRects(screenBlocks);
            for (int i = 0; i < screenBlocks.Count; i++) into.Add(KitUnity.Rect(screenBlocks[i]));
        }

        static bool NativeNoticesMoved()
        {
            nativeRects.Clear();
            KitNativeNotices.AppendShownRects(nativeRects);
            int signature = nativeRects.Count;
            for (int i = 0; i < nativeRects.Count; i++)
            {
                var block = nativeRects[i];
                signature = unchecked(signature * 31 + block.x.GetHashCode() + block.y.GetHashCode() + block.width.GetHashCode() + block.height.GetHashCode());
            }
            if (signature == nativeSignature) return false;
            nativeSignature = signature;
            return true;
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
