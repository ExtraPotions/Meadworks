// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>
    /// Small helpers for building the window from copies of the game's own UI: placing a rect from the frame's top-left
    /// corner, copying a game button or text with its look, and clearing the inspector listeners a copy carries over
    /// (a copied game button would otherwise still call the game window it came from).
    /// </summary>
    internal static class UiParts
    {
        /// <summary>Puts <paramref name="rect"/> at x, y from its parent's top-left corner (y down), w by h.</summary>
        public static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Fills the parent, inset by <paramref name="inset"/> on every side.</summary>
        public static void Fill(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static RectTransform Node(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>A copy of a game button, its label set, its clicks going only to <paramref name="onClick"/>.</summary>
        public static Button Button(Button template, Transform parent, string name, string label, UnityAction onClick)
        {
            GameObject copy = Object.Instantiate(template.gameObject, parent, false);
            copy.name = name;
            // The template's gamepad hint and gamepad key would answer the game's B button on every copy.
            Transform hint = copy.transform.Find("gamepad_hint");
            if (hint != null)
            {
                Object.DestroyImmediate(hint.gameObject);
            }
            Destroy(copy.GetComponent("UIGamePad"));
            Button button = copy.GetComponent<Button>();
            Rewire(button, onClick);
            TMP_Text? text = copy.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
            }
            return button;
        }

        public static void Rewire(Button button, UnityAction onClick)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(onClick);
        }

        /// <summary>A copy of a game text: its font, outline and colour, with new size, alignment and words.</summary>
        public static TMP_Text Text(TMP_Text template, Transform parent, string name, float size, TextAlignmentOptions align)
        {
            GameObject copy = Object.Instantiate(template.gameObject, parent, false);
            copy.name = name;
            foreach (Transform child in copy.transform)
            {
                Object.Destroy(child.gameObject);
            }
            Destroy(copy.GetComponent<ContentSizeFitter>(), copy.GetComponent<LayoutElement>());
            copy.SetActive(true);
            TMP_Text text = copy.GetComponent<TMP_Text>();
            text.enableAutoSizing = false;
            text.fontSize = size;
            text.alignment = align;
            text.richText = true;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.text = "";
            return text;
        }

        /// <summary>A copy of a game image's look (sprite, slicing, colour) on a new node.</summary>
        public static Image Image(Image template, Transform parent, string name)
        {
            Image image = Node(name, parent).gameObject.AddComponent<Image>();
            image.sprite = template.sprite;
            image.type = template.type;
            image.color = template.color;
            image.pixelsPerUnitMultiplier = template.pixelsPerUnitMultiplier;
            image.raycastTarget = false;
            return image;
        }

        public static void Destroy(params Component?[] components)
        {
            foreach (Component? component in components)
            {
                if (component != null)
                {
                    Object.DestroyImmediate(component);
                }
            }
        }
    }
}
