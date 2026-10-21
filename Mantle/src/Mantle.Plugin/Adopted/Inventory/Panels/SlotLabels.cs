using System.Collections.Generic;
using Mantle.Core;
using Mantle.Slots;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// What an empty slot shows (the user's mockup): a bronze-tinted icon of what belongs there (<see cref="SlotIcons"/>)
    /// with the slot's name under it in the game's serif (the font of its buttons), light and easy to read. An item in
    /// the slot covers both: they show only while the slot is empty (<c>InventoryElement.m_used</c>, which the game sets
    /// every frame). No outline: on the game's font material an outline swallowed the letters. Children of the element,
    /// so they go where the element goes and die with it when the grid rebuilds. Slot Labels off hides the name, Slot
    /// Icons off the icon (asked on GitHub 2026-10-07: the names off, the drawings kept); the icon alone sits in the
    /// middle of the slot. A change of either sets every hint again (<see cref="SlotElements.Invalidate"/>).
    /// </summary>
    public static class SlotLabels
    {
        private const string Name = "Mantle_hint";
        private const float IconSize = 34f;
        private const float IconTop = 7f;
        private static readonly Color CaptionColour = new Color(0.96f, 0.89f, 0.74f, 1f);

        private static readonly List<InventoryElement> captioned = new List<InventoryElement>();
        private static readonly List<Transform> hints = new List<Transform>();

        public static string TextFor(Slot slot) => Language.Localize(Words.Caption(slot.Kind));

        /// <summary>Whether an empty slot shows anything at all: its name, its icon or both.</summary>
        public static bool Shown => InventorySettings.SlotLabels.Value || InventorySettings.SlotIcons.Value;

        /// <summary>The grid was placed again: the hints to keep up to date are the ones set from now on.</summary>
        public static void Clear()
        {
            captioned.Clear();
            hints.Clear();
        }

        public static void Set(InventoryElement element, Slot slot, TMP_FontAsset font)
        {
            Transform hint = element.transform.Find(Name) ?? Create(element, font);
            TMP_Text caption = hint.GetComponentInChildren<TMP_Text>(true);
            caption.text = TextFor(slot);
            caption.gameObject.SetActive(InventorySettings.SlotLabels.Value);
            Image icon = hint.Find("icon").GetComponent<Image>();
            icon.sprite = SlotIcons.For(slot.Kind);
            icon.color = SlotIcons.TintFor(slot.Kind);
            icon.enabled = icon.sprite != null && InventorySettings.SlotIcons.Value;
            PlaceIcon((RectTransform)icon.transform, InventorySettings.SlotLabels.Value);
            captioned.Add(element);
            hints.Add(hint);
        }

        /// <summary>Above the name when it shows, in the middle of the slot when the icon is alone.</summary>
        private static void PlaceIcon(RectTransform rect, bool underName)
        {
            float y = underName ? 1f : 0.5f;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, y);
            rect.anchoredPosition = new Vector2(0f, underName ? -IconTop : 0f);
        }

        /// <summary>Every frame the grid is drawn: a hint shows while its slot is empty.</summary>
        public static void Refresh()
        {
            bool on = Shown;
            for (int i = 0; i < captioned.Count; i++)
            {
                InventoryElement element = captioned[i];
                Transform hint = hints[i];
                if (element == null || hint == null)
                    continue;
                bool show = on && !element.m_used;
                if (hint.gameObject.activeSelf != show)
                    hint.gameObject.SetActive(show);
            }
        }

        private static Transform Create(InventoryElement element, TMP_FontAsset font)
        {
            RectTransform hint = Child(element.transform, Name);
            hint.anchorMin = Vector2.zero;
            hint.anchorMax = Vector2.one;
            hint.offsetMin = hint.offsetMax = Vector2.zero;
            hint.SetSiblingIndex(element.m_icon.transform.GetSiblingIndex());
            Icon(Child(hint, "icon"));
            Caption(Child(hint, "caption"), font ?? element.m_amount.font);
            return hint;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Icon(RectTransform rect)
        {
            rect.sizeDelta = new Vector2(IconSize, IconSize);
            Image image = rect.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = SlotIcons.Hint;
        }

        private static void Caption(RectTransform rect, TMP_FontAsset font)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(-4f, 17f);
            rect.anchoredPosition = new Vector2(0f, 3f);
            rect.gameObject.SetActive(false);   // the text wakes with its font set, so it never looks for TextMeshPro's missing default
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            rect.gameObject.SetActive(true);
            Style(label);
            rect.gameObject.AddComponent<CaptionStyle>();
        }

        /// <summary>The slot name's look: small enough for the longest name ("Backpack") on one line.</summary>
        public static void Style(TextMeshProUGUI label)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = 7f;
            label.fontSizeMax = 12f;
            label.color = CaptionColour;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;   // the game fonts have no ellipsis glyph (Crucible.UI 0.3.9)
            label.raycastTarget = false;
        }
    }
}
