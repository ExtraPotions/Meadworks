// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/TipBox.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// The panel a column box's tooltip shows: an instance of <see cref="TipTemplate"/> (the game's inventory item
    /// tooltip with a thin gold border), placed once - its left edge a few units right of the box, centred on the box's
    /// height - and left there. When that would leave the screen it goes left of the box instead, and it is shifted up
    /// or down to stay on screen. It never takes the pointer. A word's tip (<see cref="LinkTips"/>) is placed the same
    /// way beside the word.
    /// </summary>
    internal static class TipBox
    {
        private const float Gap = 6f;

        public static GameObject? Show(GameObject? prefab, RectTransform plate, string topic, string text)
        {
            Vector3[] corners = new Vector3[4];
            plate.GetWorldCorners(corners);
            return Show(prefab, plate.GetComponentInParent<Canvas>(), corners, topic, text);
        }

        /// <summary>A tip beside any area, given by its world corners in Unity's order: bottom-left, top-left, top-right, bottom-right.</summary>
        public static GameObject? Show(GameObject? prefab, Canvas? canvas, Vector3[] corners, string topic, string text)
        {
            if (prefab == null || canvas == null)
            {
                return null;
            }
            GameObject box = Object.Instantiate(prefab, canvas.transform, false);
            box.name = "PlateColumn_tip";
            CanvasGroup group = box.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            Write(box.transform, "Topic", topic);
            Write(box.transform, "Text", text);
            box.SetActive(true);
            if (box.transform.childCount > 0 && box.transform.GetChild(0) is RectTransform panel)
            {
                Place(panel, corners);
            }
            return box;
        }

        private static void Write(Transform root, string name, string words)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == name)
                {
                    text.text = Localization.instance != null ? Localization.instance.Localize(words) : words;
                }
            }
        }

        /// <summary>Sizes the panel to its text first (its layout would otherwise run after this frame), then pins it.</summary>
        private static void Place(RectTransform panel, Vector3[] corners)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            float gap = Gap * panel.lossyScale.x;
            panel.pivot = new Vector2(0f, 0.5f);
            panel.position = (corners[2] + corners[3]) / 2f + Vector3.right * gap;
            Vector3[] own = new Vector3[4];
            panel.GetWorldCorners(own);
            if (own[2].x > Screen.width)
            {
                panel.pivot = new Vector2(1f, 0.5f);
                panel.position = (corners[0] + corners[1]) / 2f + Vector3.left * gap;
                panel.GetWorldCorners(own);
            }
            float up = Mathf.Max(0f, -own[0].y) - Mathf.Max(0f, own[1].y - Screen.height);
            panel.position += Vector3.up * up;
        }
    }
}
