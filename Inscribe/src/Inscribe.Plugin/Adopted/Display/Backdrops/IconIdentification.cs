// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Runtime;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Inscribe.Adopted.Display.Backdrops
{
    internal static class IconIdentification
    {
        private const int MaxTicks = 4;
        private static readonly Color Ivory = new Color(.97f, .95f, .89f, 1f);
        private static readonly Dictionary<Image, Made> All = new Dictionary<Image, Made>();
        private static readonly List<Image> Gone = new List<Image>();
        private static int _sweepAt = 64;

        private sealed class Made
        {
            internal RectTransform Holder = null!;
            internal Image Badge = null!;
            internal TMP_Text Level = null!;
            internal RectTransform Rarity = null!;
            internal RectTransform[] Ticks = new RectTransform[MaxTicks];
            internal TMP_Text Rank = null!;
            internal string? ShownBadge;
            internal int ShownRank = -1;
        }

        /// <summary>The ivory badge text from <see cref="ItemBadgeHooks.BadgeText"/>; empty (no badge) while nothing supplies one.</summary>
        internal static string BadgeOf(ItemDrop.ItemData? item) => ItemBadgeHooks.Badge(item);

        internal static bool HasBadge(ItemDrop.ItemData? item) => BadgeOf(item).Length > 0;

        internal static void Set(Image icon, ItemDrop.ItemData? item, bool rarityVisible)
        {
            string badge = BadgeOf(item);
            bool shown = badge.Length > 0;
            int rank = rarityVisible && item != null ? ItemState.Read(item).Rarity?.Index ?? 0 : 0;
            if (!shown && rank <= 0)
            {
                Hide(icon);
                return;
            }
            Made made = Get(icon);
            Fit(made.Holder, icon.rectTransform);
            made.Holder.gameObject.SetActive(true);
            made.Badge.gameObject.SetActive(shown);
            if (shown)
            {
                if (badge != made.ShownBadge)
                {
                    made.Level.text = badge;
                    made.ShownBadge = badge;
                }
                float fontSize = 15f * Mathf.Clamp(icon.rectTransform.rect.width / 64f, .5f, 2f);
                if (made.Level.fontSizeMax != fontSize)
                {
                    made.Level.fontSizeMax = fontSize;
                    made.Level.fontSizeMin = fontSize * .5f;
                }
                // Stacked items retain the native quantity's lower-left corner.
                Place(made.Badge.rectTransform, 9f, item!.m_shared.m_maxStackSize > 1 ? 30f : 11f, 27f, 16f);
            }
            made.Rarity.gameObject.SetActive(rank > 0);
            bool numericRank = rank > MaxTicks;
            for (int i = 0; i < MaxTicks; i++)
            {
                made.Ticks[i].gameObject.SetActive(!numericRank && i < rank);
                if (!numericRank) Place(made.Ticks[i], 32f + (i - (rank - 1) * .5f) * 5f - 1f, 54f, 2f, 5f);
            }
            made.Rank.gameObject.SetActive(numericRank);
            if (numericRank && rank != made.ShownRank) made.Rank.text = "R" + rank.ToString(CultureInfo.InvariantCulture);
            made.ShownRank = rank;
        }

        internal static void Hide(Image? icon)
        {
            if (icon != null && All.TryGetValue(icon, out Made made) && made.Holder != null)
                made.Holder.gameObject.SetActive(false);
        }

        internal static void HideAll()
        {
            foreach (Made made in All.Values)
                if (made.Holder != null) made.Holder.gameObject.SetActive(false);
        }

        internal static void Fade(Image icon, float seconds, float alpha)
        {
            if (All.TryGetValue(icon, out Made made) && made.Holder != null)
                foreach (Graphic graphic in made.Holder.GetComponentsInChildren<Graphic>(true)) IconBackdrop.FadeGraphic(graphic, seconds, alpha);
        }

        internal static void Shutdown()
        {
            foreach (Made made in All.Values)
                if (made.Holder != null) KitUnity.Retire(made.Holder.gameObject);
            All.Clear();
            Gone.Clear();
            _sweepAt = 64;
        }

        private static Made Get(Image icon)
        {
            if (All.TryGetValue(icon, out Made made) && made.Holder != null) return made;
            if (All.Count >= _sweepAt)
            {
                Gone.Clear();
                foreach (var pair in All)
                    if (pair.Key == null || pair.Value.Holder == null) Gone.Add(pair.Key!);
                foreach (Image key in Gone) All.Remove(key);
                _sweepAt = Math.Max(64, All.Count * 2);
            }
            var go = new GameObject("Inscribe_identification", typeof(RectTransform), typeof(LayoutElement));
            go.layer = icon.gameObject.layer;
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var holder = (RectTransform)go.transform;
            holder.SetParent(icon.transform.parent, false);
            holder.SetSiblingIndex(icon.transform.GetSiblingIndex() + 1);
            var badgeFill = Image(holder, "enhancement", RoundedFill.Sprite, new Color(.09f, .075f, .05f, .96f));
            badgeFill.type = UnityEngine.UI.Image.Type.Sliced;
            badgeFill.pixelsPerUnitMultiplier = 3f;
            TMP_Text level = KitCanvas.Text(badgeFill.transform, "level", "", 15f, Ivory, TextAlignmentOptions.Center, false);
            Stretch(level.rectTransform);
            level.richText = false;
            level.overflowMode = TextOverflowModes.Truncate;
            level.enableAutoSizing = true;
            level.fontSizeMin = 5f;
            level.fontSizeMax = 15f;
            var rarityGo = new GameObject("rarity", typeof(RectTransform));
            rarityGo.layer = icon.gameObject.layer;
            var rarity = (RectTransform)rarityGo.transform;
            rarity.SetParent(holder, false);
            Stretch(rarity);
            made = new Made { Holder = holder, Badge = badgeFill, Level = level, Rarity = rarity };
            for (int i = 0; i < MaxTicks; i++)
            {
                var tickGo = new GameObject("tick" + i, typeof(RectTransform));
                tickGo.layer = icon.gameObject.layer;
                var tick = (RectTransform)tickGo.transform;
                tick.SetParent(rarity, false);
                Image cut = Image(tick, "cut", null, new Color(.06f, .05f, .03f, 1f));
                Stretch(cut.rectTransform);
                cut.rectTransform.anchoredPosition = new Vector2(1f, -1f);
                Image stroke = Image(tick, "stroke", null, Ivory);
                Stretch(stroke.rectTransform);
                made.Ticks[i] = tick;
            }
            made.Rank = KitCanvas.Text(rarity, "rank", "", 11f, Ivory, TextAlignmentOptions.Center, false);
            made.Rank.richText = false;
            Place(made.Rank.rectTransform, 21f, 49f, 22f, 13f);
            All[icon] = made;
            return made;
        }

        private static Image Image(Transform parent, string name, Sprite? sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            Vector2 min = new Vector2(x / 64f, y / 64f);
            Vector2 max = new Vector2((x + width) / 64f, (y + height) / 64f);
            if (rect.anchorMin != min) rect.anchorMin = min;
            if (rect.anchorMax != max) rect.anchorMax = max;
            if (rect.offsetMin != Vector2.zero) rect.offsetMin = Vector2.zero;
            if (rect.offsetMax != Vector2.zero) rect.offsetMax = Vector2.zero;
        }

        private static void Fit(RectTransform holder, RectTransform icon)
        {
            if (holder.anchorMin != icon.anchorMin) holder.anchorMin = icon.anchorMin;
            if (holder.anchorMax != icon.anchorMax) holder.anchorMax = icon.anchorMax;
            if (holder.pivot != icon.pivot) holder.pivot = icon.pivot;
            if (holder.offsetMin != icon.offsetMin) holder.offsetMin = icon.offsetMin;
            if (holder.offsetMax != icon.offsetMax) holder.offsetMax = icon.offsetMax;
            if (holder.localScale != icon.localScale) holder.localScale = icon.localScale;
        }
    }
}
