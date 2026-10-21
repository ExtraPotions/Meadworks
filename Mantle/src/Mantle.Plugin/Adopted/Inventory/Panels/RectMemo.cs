using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// A game element's place and size as the game made it, so a layout can be put on it again and again from the same
    /// start (and taken off by setting it with no change): never added onto what an earlier pass left.
    /// </summary>
    public sealed class RectMemo
    {
        private readonly RectTransform rect;
        private readonly Vector2 position;
        private readonly Vector2 size;

        private RectMemo(RectTransform rect)
        {
            this.rect = rect;
            position = rect.anchoredPosition;
            size = rect.sizeDelta;
        }

        public static RectMemo Of(Transform transform) => transform is RectTransform rect ? new RectMemo(rect) : null;

        public Vector2 Size => size;

        public bool Alive => rect != null;

        /// <summary>The game's place moved by <paramref name="move"/> and its size grown by <paramref name="grow"/>.</summary>
        public void Set(Vector2 move, Vector2 grow)
        {
            if (rect == null)
                return;
            Vector2 at = position + move;
            Vector2 sized = size + grow;
            if (rect.anchoredPosition != at)
                rect.anchoredPosition = at;
            if (rect.sizeDelta != sized)
                rect.sizeDelta = sized;
        }
    }
}
