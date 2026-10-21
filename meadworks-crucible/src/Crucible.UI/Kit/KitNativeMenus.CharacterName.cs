// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        // Character selection draws the < > arrows over a long character name. While MeadWorks styling is on, the name
        // sits on its own line above the arrows, and the arrows flank a gap as wide as the native Start button. With
        // styling off (or the screen closed) the native places return. Approved by the user on October 19, 2026.
        static RectTransform? movedName, movedLeft, movedRight;
        static Vector2 nativeName, nativeLeft, nativeRight;
        static Vector2Int laidOutFor;

        /// <summary>True for a native rect whose position MeadWorks deliberately owns while styled (character-select name and arrows; proposed notice placement).</summary>
        public static bool OwnsLayout(RectTransform rect) =>
            rect != null && (movedName != null && (rect == movedName || rect == movedLeft || rect == movedRight) || OwnsNoticeLayout(rect));

        static void TickCharacterName()
        {
            var startup = FejdStartup.instance;
            bool want = On && startup != null && startup.m_csName != null && startup.m_csLeftButton != null && startup.m_csRightButton != null
                && startup.m_csStartButton != null && startup.m_characterSelectScreen != null && startup.m_characterSelectScreen.activeInHierarchy;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (movedName != null && (!want || movedName != startup!.m_csName.rectTransform || screen != laidOutFor)) RestoreCharacterName();
            if (!want || movedName != null) return;
            var name = startup!.m_csName.rectTransform;
            var left = (RectTransform)startup.m_csLeftButton.transform;
            var right = (RectTransform)startup.m_csRightButton.transform;
            if (left.IsChildOf(name) || right.IsChildOf(name)) return;   // a layout we do not recognize stays native
            movedName = name; movedLeft = left; movedRight = right; laidOutFor = screen;
            nativeName = name.anchoredPosition; nativeLeft = left.anchoredPosition; nativeRight = right.anchoredPosition;
            name.anchoredPosition = nativeName + new Vector2(0f, name.rect.height + 6f);
            var start = (RectTransform)startup.m_csStartButton.transform;
            float gap = start.rect.width * start.lossyScale.x, half = left.rect.width * left.lossyScale.x * .5f;
            Vector3 l = left.position, r = right.position;
            float center = (l.x + r.x) * .5f;
            left.position = new Vector3(center - gap * .5f - half, l.y, l.z);
            right.position = new Vector3(center + gap * .5f + half, r.y, r.z);
        }

        static void RestoreCharacterName()
        {
            if (movedName != null) movedName.anchoredPosition = nativeName;
            if (movedLeft != null) movedLeft.anchoredPosition = nativeLeft;
            if (movedRight != null) movedRight.anchoredPosition = nativeRight;
            movedName = movedLeft = movedRight = null;
        }
    }
}
