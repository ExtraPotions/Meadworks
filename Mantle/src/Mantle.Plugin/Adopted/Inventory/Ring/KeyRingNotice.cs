using System.Collections.Generic;
using Mantle.Core;
using Mantle.Panels;
using Mantle.Presentation;
using Mantle.Slots;
using ExtraPotions.Mantle.Embedded.PlateColumn;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mantle.Ring
{
    /// <summary>
    /// The announcement of a key the character never had before (<see cref="KeyRingNews"/>). While one is waiting, the ring
    /// button's key icon breathes slowly between its own colours and gold (<see cref="Period"/> seconds a breath) and a small box
    /// under the button says so ("Swamp Key went onto your key ring", or "2 new keys went onto your key ring"). Like the
    /// game's own messages it fades out over four seconds of being shown (<see cref="NoticeFade"/>), or goes at once when
    /// the ring is opened; it is never saved. The button shows only with the inventory open, so a key picked up on the
    /// road is announced the next time the inventory opens. The box takes no clicks.
    /// </summary>
    public static class KeyRingNotice
    {
        private const string Name = "Mantle_keynotice";
        private const string HintIcon = "Mantle_hint/icon";
        private const float Period = 1.6f;
        private const float Height = 26f;
        private static readonly Color Glow = new Color(1f, 0.86f, 0.4f, 1f);
        private static readonly List<string> waiting = new List<string>();
        private static string shownText;
        private static float shownSince = -1f;

        public static void Add(ItemDrop.ItemData key)
        {
            waiting.Add(key.m_shared.m_name);
            shownText = null;
            shownSince = -1f;
        }

        public static void Clear()
        {
            waiting.Clear();
            shownSince = -1f;
        }

        /// <summary>The language changed: the box is written again in the new one.</summary>
        public static void Relabel() => shownText = null;

        /// <summary>Every frame the button is refreshed (after it set its icon's colour): the box and the breathing icon, or neither.</summary>
        public static void Show(InventoryElement button)
        {
            if (shownSince < 0f && waiting.Count > 0)
                shownSince = Time.unscaledTime;
            if (KeyRingState.Open || (waiting.Count > 0 && NoticeFade.Over(Time.unscaledTime - shownSince)))
                Clear();
            Transform box = button.transform.Find(Name);
            if (waiting.Count == 0)
            {
                if (box != null && box.gameObject.activeSelf)
                    Rest(button, box);
                return;
            }
            box = box != null ? box : Make(button);
            box.gameObject.SetActive(true);
            box.GetComponent<CanvasGroup>().alpha = NoticeFade.Alpha(Time.unscaledTime - shownSince);
            Write(box);
            Breathe(button, 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / Period));
        }

        /// <summary>Both icons the button may show (its own with Slot Labels off, the hint's with them on), from their hint colour towards gold.</summary>
        private static void Breathe(InventoryElement button, float glow)
        {
            button.m_icon.color = Color.Lerp(SlotIcons.Hint, Glow, glow);
            Transform hint = button.transform.Find(HintIcon);
            if (hint != null)
                hint.GetComponent<Image>().color = Color.Lerp(SlotIcons.TintFor(SlotKind.Key), Glow, glow);
        }

        private static void Rest(InventoryElement button, Transform box)
        {
            box.gameObject.SetActive(false);
            Transform hint = button.transform.Find(HintIcon);
            if (hint != null)
                hint.GetComponent<Image>().color = SlotIcons.TintFor(SlotKind.Key);
        }

        /// <summary>The text for what is waiting, set (and the box sized to it) only when it changed.</summary>
        private static void Write(Transform box)
        {
            string text = waiting.Count == 1
                ? string.Format(Language.Localize(Words.KeyNew), Language.Localize(waiting[0]))
                : string.Format(Language.Localize(Words.KeysNew), waiting.Count);
            if (text == shownText)
                return;
            shownText = text;
            TMP_Text label = box.GetComponentInChildren<TMP_Text>(true);
            label.text = text;
            ((RectTransform)box).sizeDelta = new Vector2(label.GetPreferredValues(text).x + 16f, Height);
        }

        /// <summary>A small box in the column's style hanging under the button, centred, with one line of text.</summary>
        private static Transform Make(InventoryElement button)
        {
            GameObject go = new GameObject(Name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.layer = button.gameObject.layer;
            go.transform.SetParent(button.transform, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -6f);
            Image image = go.GetComponent<Image>();
            image.sprite = Skin.Box;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            Label(button, rect);
            return go.transform;
        }

        private static void Label(InventoryElement button, RectTransform box)
        {
            GameObject go = new GameObject("text", typeof(RectTransform));
            go.SetActive(false);   // the text wakes with its font set, so it never looks for TextMeshPro's missing default
            go.layer = box.gameObject.layer;
            go.transform.SetParent(box, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.font = button.m_amount.font;
            go.SetActive(true);
            text.fontSize = 14f;
            text.color = Skin.LabelColour;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
        }
    }
}
