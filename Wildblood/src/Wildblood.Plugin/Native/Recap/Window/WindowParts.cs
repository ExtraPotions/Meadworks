// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>
    /// The pieces of the game's compendium window (its "Texts" dialog) the recap is built from, found in a fresh copy:
    /// the framed panel, its title, the list on the left with its entry template, the scrolling area on the right, its
    /// text, and the two close buttons (the panel's own and the dark backdrop's).
    /// </summary>
    internal sealed class WindowParts
    {
        public GameObject Root = null!;
        public RectTransform Frame = null!;
        public TMP_Text Topic = null!;
        public RectTransform List = null!;
        public ScrollRect ListScroll = null!;
        public RectTransform ListRoot = null!;
        public GameObject Element = null!;
        public RectTransform Area = null!;
        public ScrollRect AreaScroll = null!;
        public RectTransform AreaContent = null!;
        public TMP_Text Text = null!;
        public Button Close = null!;
        public Button Backdrop = null!;
        public Image Inset = null!;

        /// <summary>Read from the copy's own dialog component, before that component is removed.</summary>
        public static WindowParts? From(GameObject root, TextsDialog dialog)
        {
            Transform? frame = Utils.FindChild(dialog.transform, "Texts_frame");
            Transform? close = frame != null ? frame.Find("Closebutton") : null;
            Transform? backdrop = dialog.transform.Find("Closebutton");
            Transform? topic = frame != null ? frame.Find("topic") : null;
            if (frame == null || close == null || backdrop == null || topic == null || dialog.m_textArea == null)
            {
                return null;
            }
            WindowParts parts = Lists(dialog);
            parts.Root = root;
            parts.Frame = (RectTransform)frame;
            parts.Topic = topic.GetComponent<TMP_Text>();
            parts.Close = close.GetComponent<Button>();
            parts.Backdrop = backdrop.GetComponent<Button>();
            return parts;
        }

        private static WindowParts Lists(TextsDialog dialog) => new WindowParts
        {
            List = (RectTransform)dialog.m_leftScrollRect.transform.parent,
            ListScroll = dialog.m_leftScrollRect,
            ListRoot = dialog.m_listRoot,
            Element = dialog.m_elementPrefab,
            Area = (RectTransform)dialog.m_rightScrollbar.transform.parent,
            AreaScroll = dialog.m_rightScrollbar.transform.parent.GetComponentInChildren<ScrollRect>(true),
            AreaContent = (RectTransform)dialog.m_textArea.transform.parent,
            Text = dialog.m_textArea,
            Inset = dialog.m_leftScrollRect.transform.parent.GetComponent<Image>(),
        };
    }
}
