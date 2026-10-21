using UnityEngine;
using UnityEngine.UI;

namespace Mantle.Panels
{
    /// <summary>
    /// The stat sheet's scrolling list (<see cref="GearStats"/>): a viewport clipped to its rect (<c>RectMask2D</c>)
    /// holding a content rect as tall as its rows, moved by a <c>ScrollRect</c> with the mouse wheel at the game's own
    /// recipe list's speed, and a thin bronze bar in the right margin that shows only when the list is longer than the
    /// viewport. The viewport's clear image takes the pointer, so the wheel works anywhere over the list.
    /// </summary>
    public static class SheetScroll
    {
        private const float BarWidth = 4f;
        private const float FallbackSensitivity = 30f;
        private static readonly Color Track = new Color(0f, 0f, 0f, 0.25f);
        private static readonly Color Handle = new Color(0.55f, 0.4f, 0.24f, 0.9f);

        /// <summary>The list inside <paramref name="panel"/>, between <paramref name="top"/> and the margin; returns the content rect.</summary>
        public static RectTransform Make(InventoryGui gui, RectTransform panel, float margin, float top)
        {
            RectTransform viewport = Child(panel, "viewport");
            Stretch(viewport, new Vector2(margin, margin), new Vector2(-margin, -top));
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Child(viewport, "content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = GameSensitivity(gui);
            scroll.verticalScrollbar = Bar(panel, margin, top);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        /// <summary>The wheel's speed of the game's own recipe list, so both scroll alike.</summary>
        private static float GameSensitivity(InventoryGui gui)
        {
            ScrollRect game = gui.m_crafting != null ? gui.m_crafting.GetComponentInChildren<ScrollRect>(true) : null;
            return game != null && game.scrollSensitivity > 0f ? game.scrollSensitivity : FallbackSensitivity;
        }

        private static Scrollbar Bar(RectTransform panel, float margin, float top)
        {
            RectTransform track = Child(panel, "scrollbar");
            track.anchorMin = new Vector2(1f, 0f);
            track.anchorMax = Vector2.one;
            track.pivot = new Vector2(0.5f, 0.5f);
            track.offsetMin = new Vector2(-(margin + BarWidth) / 2f, margin);
            track.offsetMax = new Vector2(-(margin - BarWidth) / 2f, -top);
            track.gameObject.AddComponent<Image>().color = Track;
            RectTransform handle = Child(track, "handle");
            Stretch(handle, Vector2.zero, Vector2.zero);
            Image fill = handle.gameObject.AddComponent<Image>();
            fill.color = Handle;
            Scrollbar bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle;
            bar.targetGraphic = fill;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            return bar;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }
    }
}
