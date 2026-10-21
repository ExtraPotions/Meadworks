using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The strip at the bottom of the player panel kept free for OpenKeep's button row (Quick stack, Store all, Top up,
    /// Sort and the trash can; 26 high, 2 above the edge, 2 below the grid), while Mantle lays the panel out and
    /// OpenKeep 1.8.0 or later is installed (<see cref="PanelSize"/>). It is marked by an empty object named
    /// <see cref="Name"/>, a child of the player panel stretched along its bottom edge, active exactly while the strip is
    /// kept: OpenKeep finds it by name and puts its row inside, else hangs the row below the panel as it does alone. No
    /// code is shared between the mods. The object has no graphic, so it takes no clicks.
    /// Changed for Mantle, 2026-10-07: the name stays PackPanel's own, the cross-mod contract OpenKeep and Burrow look
    /// up (the source rename had made it Mantle_buttonstrip, which neither finds), and Burrow's row counts like
    /// OpenKeep's (<see cref="PanelSize.ButtonsInside"/>).
    /// </summary>
    public static class ButtonStrip
    {
        public const string Name = "PackPanel_buttonstrip";
        public const float Height = 30f;

        public static void Mark(RectTransform panel, bool on)
        {
            RectTransform strip = panel.Find(Name) as RectTransform;
            if (strip == null)
            {
                if (!on)
                    return;
                strip = Make(panel);
            }
            if (strip.gameObject.activeSelf != on)
                strip.gameObject.SetActive(on);
        }

        private static RectTransform Make(RectTransform panel)
        {
            GameObject go = new GameObject(Name, typeof(RectTransform));
            go.layer = panel.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(panel, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, Height);
            return rect;
        }
    }
}
