// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>
    /// The deaths on the left, newest first: each the picture at the moment of death, the killer, and when it happened.
    /// Clicking one selects it for the viewer.
    /// </summary>
    internal sealed class DeathList
    {
        private const float Height = 64f;
        private const float Spacing = 68f;
        private const float ThumbWidth = 96f;
        private const float ThumbHeight = 54f;

        private readonly ElementList _list;

        public DeathList(ElementList list)
        {
            _list = list;
        }

        public static float RowHeight => Height;
        public static float RowSpacing => Spacing;

        public System.Action<int>? Clicked
        {
            set => _list.Clicked = value;
        }

        public void Fill(List<DeathRecap> deaths)
        {
            _list.Clear();
            foreach (DeathRecap recap in deaths)
            {
                GameObject row = _list.Add(RecapText.Entry(recap), 16f);
                Thumb(row, recap.Thumbnail);
            }
        }

        public void Mark(int index) => _list.Mark(index, true);

        // The picture sits at the row's left; the text moves right of it.
        private static void Thumb(GameObject row, Texture2D? picture)
        {
            TMP_Text? label = ElementList.LabelOf(row);
            if (label != null)
            {
                label.rectTransform.offsetMin = new Vector2(ThumbWidth + 14f, label.rectTransform.offsetMin.y);
            }
            RectTransform rect = UiParts.Node("ecr_recap_thumb", row.transform);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(6f, 0f);
            rect.sizeDelta = new Vector2(ThumbWidth, ThumbHeight);
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.texture = picture;
            image.color = picture != null ? Color.white : new Color(0f, 0f, 0f, 0.5f);
        }
    }
}
