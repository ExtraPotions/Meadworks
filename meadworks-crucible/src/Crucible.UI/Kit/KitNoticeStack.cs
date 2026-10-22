// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public enum KitNoticeKind { TopLeft, Center, Unlock, Biome }

    /// <summary>Screen corner for the MeadWorks notice stack. Stored by name in the local config.</summary>
    public enum KitNoticeCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    /// <summary>
    /// Engine-free stack: newest at the front, a repeat of the same kind and text merges, and cards past the visible
    /// limit wait without losing their time. Timers run only for the visible prefix, so an idle stack does no work.
    /// </summary>
    public sealed class KitNoticeStack
    {
        public const int DefaultMaxVisible = 5;
        public const int MinVisible = 1;
        public const int MaxVisibleLimit = 8;
        public const float DefaultDuration = 4f;
        public const float MinDuration = 1f;
        public const float MaxDuration = 30f;
        /// <summary>The last part of a card's time, during which it fades out.</summary>
        public const float FadeSeconds = 0.4f;

        sealed class Slot
        {
            internal int Id;
            internal string Key = "";
            internal KitNoticeKind Kind;
            internal string Raw = "";
            internal string Title = "";
            internal int Amount;
            internal int Count;
            internal bool HasIcon;
            internal bool ShowWhenHidden;
            internal float Remaining;
            internal float Duration;
        }

        readonly List<Slot> cards = new List<Slot>(32);
        readonly List<Slot> pool = new List<Slot>(32);
        readonly List<int> removed = new List<int>(8);
        readonly Dictionary<string, Slot> byKey = new Dictionary<string, Slot>(StringComparer.Ordinal);
        int nextId = 1;
        int limit = DefaultMaxVisible;
        int spaceCap = -1;
        int revision;

        public int Revision => revision;
        public bool Idle => cards.Count == 0;

        public bool Fading
        {
            get
            {
                int visible = ShownCount;
                for (int i = 0; i < visible; i++)
                    if (cards[i].Remaining < FadeSeconds) return true;
                return false;
            }
        }

        /// <summary>A visible card was asked to show while the game HUD is hidden.</summary>
        public bool ShownWhileHidden
        {
            get
            {
                int visible = ShownCount;
                for (int i = 0; i < visible; i++)
                    if (cards[i].ShowWhenHidden) return true;
                return false;
            }
        }
        public int ShownCount => cards.Count < limit ? cards.Count : limit;
        public int QueuedCount => cards.Count - ShownCount;
        public IReadOnlyList<int> Removed => removed;

        /// <summary>Advances visible cards. A space limit below zero keeps the configured maximum. Zero shows nothing and spends no time.</summary>
        public bool Tick(float delta, bool hold, int maxVisible, int spaceLimit = -1)
        {
            spaceCap = spaceLimit;
            limit = VisibleLimit(maxVisible);
            if (cards.Count == 0) return false;
            removed.Clear();
            bool changed = false;
            if (!hold && delta > 0f)
            {
                int visible = ShownCount;
                for (int i = 0; i < visible; i++) cards[i].Remaining -= delta;
                for (int i = cards.Count - 1; i >= 0; i--)
                    if (cards[i].Remaining <= 0f) { RemoveAt(i); changed = true; }
            }
            return changed;
        }

        /// <summary>Inserts a notice at the top, or merges it into the live card with the same kind, raw text and title.</summary>
        public int Push(KitNoticeKind kind, string rawText, string title, int amount, bool hasIcon, float duration, int maxVisible, bool showWhenHidden = false)
        {
            if (rawText == null) throw new ArgumentNullException(nameof(rawText));
            if (!Enum.IsDefined(typeof(KitNoticeKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            title = title ?? "";
            duration = ClampDuration(duration);
            limit = VisibleLimit(maxVisible);
            string key = Key(kind, rawText, title);
            if (byKey.TryGetValue(key, out var live))
            {
                live.Count++;
                if (amount > 0) live.Amount += amount;
                if (hasIcon) live.HasIcon = true;
                if (showWhenHidden) live.ShowWhenHidden = true;
                live.Duration = duration;
                live.Remaining = duration;
                int index = cards.IndexOf(live);
                if (index > 0) { cards.RemoveAt(index); cards.Insert(0, live); }
                revision++;
                return live.Id;
            }
            var slot = Rent();
            slot.Id = nextId++;
            slot.Key = key;
            slot.Kind = kind;
            slot.Raw = rawText;
            slot.Title = title;
            slot.Amount = amount > 0 ? amount : 0;
            slot.Count = 1;
            slot.HasIcon = hasIcon;
            slot.ShowWhenHidden = showWhenHidden;
            slot.Duration = duration;
            slot.Remaining = duration;
            byKey.Add(key, slot);
            cards.Insert(0, slot);
            revision++;
            return slot.Id;
        }

        public void Clear()
        {
            if (cards.Count == 0) return;
            removed.Clear();
            for (int i = cards.Count - 1; i >= 0; i--) RemoveAt(i);
            revision++;
        }

        public string TextAt(int index) => SlotAt(index).Raw;
        public string TitleAt(int index) => SlotAt(index).Title;
        public KitNoticeKind KindAt(int index) => SlotAt(index).Kind;
        public int IdAt(int index) => SlotAt(index).Id;
        public int RepeatAt(int index) => SlotAt(index).Count;
        public bool HasIconAt(int index) => SlotAt(index).HasIcon;
        public float RemainingAt(int index) => SlotAt(index).Remaining;

        /// <summary>Summed item amount when that is more than one, otherwise the repeat count when that is more than one.</summary>
        public int BadgeAt(int index)
        {
            var slot = SlotAt(index);
            if (slot.Amount > 1) return slot.Amount;
            return slot.Count > 1 ? slot.Count : 0;
        }

        public float OpacityAt(int index)
        {
            float remaining = SlotAt(index).Remaining;
            if (remaining >= FadeSeconds) return 1f;
            if (remaining <= 0f) return 0f;
            return remaining / FadeSeconds;
        }

        Slot SlotAt(int index)
        {
            if (index < 0 || index >= ShownCount) throw new ArgumentOutOfRangeException(nameof(index));
            return cards[index];
        }

        void RemoveAt(int index)
        {
            var slot = cards[index];
            cards.RemoveAt(index);
            byKey.Remove(slot.Key);
            removed.Add(slot.Id);
            slot.Key = "";
            slot.Raw = "";
            slot.Title = "";
            pool.Add(slot);
        }

        Slot Rent()
        {
            int last = pool.Count - 1;
            if (last < 0) return new Slot();
            var slot = pool[last];
            pool.RemoveAt(last);
            return slot;
        }

        static string Key(KitNoticeKind kind, string raw, string title) => ((int)kind).ToString() + "\n" + raw + "\n" + title;

        public static int ClampVisible(int value) => value < MinVisible ? MinVisible : value > MaxVisibleLimit ? MaxVisibleLimit : value;
        public static float ClampDuration(float value) => value < MinDuration ? MinDuration : value > MaxDuration ? MaxDuration : value;

        internal int HeldCount => cards.Count;
        internal string RawHeld(int index) => Held(index).Raw;
        internal string TitleHeld(int index) => Held(index).Title;
        internal bool IconHeld(int index) => Held(index).HasIcon;

        Slot Held(int index)
        {
            if (index < 0 || index >= cards.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return cards[index];
        }

        int VisibleLimit(int maxVisible)
        {
            int cap = ClampVisible(maxVisible);
            if (spaceCap >= 0 && spaceCap < cap) return spaceCap;
            return cap;
        }
    }

    /// <summary>
    /// Screen placement (pixels, y up) that keeps a notice stack in the chosen corner and clear of the rectangles it must
    /// not cover. The stack only moves on the vertical axis. When the requested stack is taller than the free gap, the
    /// placed height shrinks to that gap. A rectangle that fills the screen wins over clearance. A panel wider than the
    /// screen shrinks to the side margins.
    /// </summary>
    public static class KitNoticePlacement
    {
        public static KitRect Place(KitNoticeCorner corner, float screenW, float screenH, float panelW, float panelH, IList<KitRect> obstacles, float margin)
        {
            if (!Enum.IsDefined(typeof(KitNoticeCorner), corner)) throw new ArgumentOutOfRangeException(nameof(corner));
            if (margin < 0f) margin = 0f;
            if (panelW < 1f) panelW = 1f;
            if (panelH < 1f) panelH = 1f;
            if (screenW < 1f) screenW = 1f;
            if (screenH < 1f) screenH = 1f;
            bool top = corner == KitNoticeCorner.TopLeft || corner == KitNoticeCorner.TopRight;
            bool left = corner == KitNoticeCorner.TopLeft || corner == KitNoticeCorner.BottomLeft;
            float innerW = screenW - margin * 2f;
            if (innerW < 1f) innerW = screenW;
            if (panelW > innerW) panelW = innerW;
            float x = left ? margin : screenW - margin - panelW;
            float limitX = screenW - panelW;
            if (limitX < 0f) limitX = 0f;
            if (x < 0f) x = 0f;
            if (x > limitX) x = limitX;

            float band0 = margin;
            float band1 = screenH - margin;
            if (band1 - band0 < 1f)
            {
                float h = panelH > screenH ? screenH : panelH;
                float yEdge = top ? screenH - h : 0f;
                if (yEdge < 0f) yEdge = 0f;
                return new KitRect(x, yEdge, panelW, h);
            }

            var free = new List<float>(8) { band0, band1 };
            IList<KitRect> blocks = obstacles ?? Array.Empty<KitRect>();
            float panelRight = x + panelW;
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (block.Width <= 0f || block.Height <= 0f || x >= block.XMax || panelRight <= block.XMin) continue;
                Subtract(free, block.YMin - margin, block.YMax + margin);
            }

            float height = panelH;
            if (!PickGap(free, top, ref height, out float y))
            {
                float minY = band0;
                float maxY = band1 - panelH;
                if (maxY < minY) maxY = minY;
                y = KitMath.Clamp(top ? band1 - panelH : band0, minY, maxY);
                return new KitRect(x, y, panelW, panelH);
            }
            return new KitRect(x, y, panelW, height);
        }

        /// <summary>How many cards of <paramref name="cardHeight"/>, separated by <paramref name="gap"/>, fit in <paramref name="slotHeight"/>. Capped by <paramref name="max"/>.</summary>
        public static int CardsThatFit(float slotHeight, float cardHeight, float gap, int max)
        {
            if (max < 1) return 0;
            if (cardHeight < 1f) cardHeight = 1f;
            if (gap < 0f) gap = 0f;
            if (slotHeight + 0.01f < cardHeight) return 0;
            float stride = cardHeight + gap;
            int count = 1;
            if (stride > 0.01f)
            {
                float extra = (slotHeight - cardHeight) / stride;
                if (extra > 0f) count += (int)Math.Floor(extra + 0.001f);
            }
            return count > max ? max : count;
        }

        /// <summary>How many leading card heights, separated by <paramref name="gap"/>, fit in <paramref name="slotHeight"/>.</summary>
        public static int PrefixThatFits(float slotHeight, IList<float> heights, float gap)
        {
            if (heights == null || heights.Count == 0) return 0;
            if (gap < 0f) gap = 0f;
            float used = 0f;
            int count = 0;
            for (int i = 0; i < heights.Count; i++)
            {
                float card = heights[i] < 1f ? 1f : heights[i];
                float next = count == 0 ? card : used + gap + card;
                if (next > slotHeight + 0.01f) break;
                used = next;
                count++;
            }
            return count;
        }

        /// <summary>Height of the first <paramref name="count"/> cards and the gaps between them.</summary>
        public static float StackHeight(IList<float> heights, int count, float gap)
        {
            if (heights == null || count <= 0) return 0f;
            if (gap < 0f) gap = 0f;
            int n = count < heights.Count ? count : heights.Count;
            float height = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) height += gap;
                height += heights[i] < 1f ? 1f : heights[i];
            }
            return height;
        }

        public static bool Overlaps(KitRect a, KitRect b) => a.XMin < b.XMax && a.XMax > b.XMin && a.YMin < b.YMax && a.YMax > b.YMin;

        static void Subtract(List<float> free, float cutA, float cutB)
        {
            if (cutB <= cutA) return;
            for (int i = 0; i + 1 < free.Count;)
            {
                float a = free[i], b = free[i + 1];
                if (cutB <= a || cutA >= b) { i += 2; continue; }
                if (cutA <= a && cutB >= b) { free.RemoveRange(i, 2); continue; }
                if (cutA <= a) { free[i] = cutB; i += 2; continue; }
                if (cutB >= b) { free[i + 1] = cutA; i += 2; continue; }
                free[i + 1] = cutA;
                free.Insert(i + 2, cutB);
                free.Insert(i + 3, b);
                i += 4;
            }
        }

        static bool PickGap(List<float> free, bool top, ref float height, out float y)
        {
            y = 0f;
            int fit = -1, tall = -1;
            float fitEdge = 0f, tallH = 0f, tallEdge = 0f;
            for (int i = 0; i + 1 < free.Count; i += 2)
            {
                float a = free[i], b = free[i + 1], h = b - a;
                if (h < 1f) continue;
                float edge = top ? b : -a;
                if (h + 0.001f >= height && (fit < 0 || edge > fitEdge)) { fit = i; fitEdge = edge; }
                if (tall < 0 || h > tallH + 0.001f || (Math.Abs(h - tallH) <= 0.001f && edge > tallEdge))
                {
                    tall = i;
                    tallH = h;
                    tallEdge = edge;
                }
            }
            int use = fit >= 0 ? fit : tall;
            if (use < 0) return false;
            float gapA = free[use], gapB = free[use + 1];
            if (height > gapB - gapA) height = gapB - gapA;
            y = top ? gapB - height : gapA;
            return true;
        }
    }

    /// <summary>One notice after it has been wrapped. <see cref="Text"/> uses a newline between the two lines.</summary>
    public readonly struct KitNoticeLineFit
    {
        public KitNoticeLineFit(string text, int lines, bool ellipsis)
        {
            Text = text ?? "";
            Lines = lines < 1 ? 1 : lines;
            Ellipsis = ellipsis;
        }

        public string Text { get; }
        public int Lines { get; }
        public bool Ellipsis { get; }
    }

    /// <summary>
    /// Notice copy laid into at most two lines from a measured string width. Three periods stand in for an ellipsis:
    /// the game fonts have no ellipsis glyph, and TextMeshPro warns when Ellipsis is used without one.
    /// </summary>
    public static class KitNoticeText
    {
        public const int MaxLines = 2;
        public const float FontSize = 16f;
        public const float OneLineCard = 46f;
        public const float OneLineBox = 32f;
        public const float LineBox = 20f;
        public const float VerticalPad = 14f;
        const string Dots = "...";

        public static float CardHeight(int lines) => lines <= 1 ? OneLineCard : VerticalPad + Math.Max(1, lines) * LineBox;
        public static float TextBoxHeight(int lines) => lines <= 1 ? OneLineBox : Math.Max(1, lines) * LineBox;

        /// <summary><paramref name="widthOf"/> is the rendered width of a string at <paramref name="fontSize"/>.</summary>
        public static KitNoticeLineFit Fit(string text, float width, float fontSize, Func<string, float> widthOf)
        {
            if (widthOf == null) throw new ArgumentNullException(nameof(widthOf));
            if (string.IsNullOrEmpty(text)) return new KitNoticeLineFit("", 1, false);
            if (width < 1f) width = 1f;
            if (fontSize < 1f) fontSize = FontSize;
            if (text.IndexOf('\n') < 0 && Width(text, widthOf, fontSize) <= width) return new KitNoticeLineFit(text, 1, false);
            var lines = new List<string>(4);
            string current = "";
            foreach (string word in Words(text))
                AppendWord(lines, ref current, word, width, fontSize, widthOf);
            if (current.Length > 0) lines.Add(current);
            if (lines.Count == 0) return new KitNoticeLineFit("", 1, false);
            if (lines.Count <= MaxLines)
                return new KitNoticeLineFit(string.Join("\n", lines), lines.Count, false);
            string rest = Rest(text, lines[0]);
            return new KitNoticeLineFit(lines[0] + "\n" + Ellipsize(rest, width, fontSize, widthOf), MaxLines, true);
        }

        static float Width(string text, Func<string, float> widthOf, float fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float measured = widthOf(text);
            if (float.IsNaN(measured) || float.IsInfinity(measured) || measured < 0f)
                return text.Length * fontSize;
            return measured;
        }

        static string Rest(string text, string first)
        {
            string trimmed = text.Trim();
            if (trimmed.StartsWith(first, StringComparison.Ordinal))
                return trimmed.Substring(first.Length).TrimStart();
            return trimmed;
        }

        static string Ellipsize(string text, float width, float fontSize, Func<string, float> widthOf)
        {
            if (string.IsNullOrEmpty(text)) return Dots;
            if (Width(text, widthOf, fontSize) <= width) return text;
            float room = width - Width(Dots, widthOf, fontSize);
            if (room < Width("n", widthOf, fontSize)) return Width(Dots, widthOf, fontSize) <= width ? Dots : "";
            string line = "";
            foreach (string word in Words(text))
            {
                if (Width(word, widthOf, fontSize) > room)
                {
                    if (line.Length > 0) break;
                    int n = 1;
                    while (n < word.Length && Width(word.Substring(0, n + 1), widthOf, fontSize) <= room) n++;
                    line = word.Substring(0, n);
                    break;
                }
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (Width(candidate, widthOf, fontSize) > room) break;
                line = candidate;
            }
            return line + Dots;
        }

        static void AppendWord(List<string> lines, ref string current, string word, float width, float fontSize, Func<string, float> widthOf)
        {
            if (Width(word, widthOf, fontSize) > width)
            {
                if (current.Length > 0) { lines.Add(current); current = ""; }
                int i = 0;
                while (i < word.Length)
                {
                    int n = 1;
                    while (i + n < word.Length && Width(word.Substring(i, n + 1), widthOf, fontSize) <= width) n++;
                    string chunk = word.Substring(i, n);
                    i += n;
                    if (i < word.Length) lines.Add(chunk);
                    else current = chunk;
                }
                return;
            }
            if (current.Length == 0) { current = word; return; }
            string joined = current + " " + word;
            if (Width(joined, widthOf, fontSize) <= width) current = joined;
            else { lines.Add(current); current = word; }
        }

        static List<string> Words(string text)
        {
            var words = new List<string>(8);
            int i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && (text[i] == ' ' || text[i] == '\n' || text[i] == '\r' || text[i] == '\t')) i++;
                if (i >= text.Length) break;
                int start = i;
                while (i < text.Length && text[i] != ' ' && text[i] != '\n' && text[i] != '\r' && text[i] != '\t') i++;
                words.Add(text.Substring(start, i - start));
            }
            return words;
        }
    }
}
