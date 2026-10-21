using CrossMark = ExtraPotions.Meadworks.Crucible.UI.CrossMark;
using Mantle.Look;
using UnityEngine;
using UnityEngine.UI;

namespace Mantle.Panels
{
    /// <summary>
    /// A cell of a worn backpack's partly used last row that the pack does not open (the user's request, 2026-09-28,
    /// before that it was hidden): shown dimmed with a grey cross over it (<see cref="CrossMark"/>), and deaf to the
    /// pointer (a <c>CanvasGroup</c> that blocks no raycasts), so a click or a dragged item let go there lands on the
    /// grid's root and does nothing. No item can be put there anyway (<see cref="Layout.InventoryLayout.IsMain"/>), and
    /// the gamepad skips it (<see cref="Ring.KeyRingGamepad"/>). Every element is marked or cleared each time the grid is
    /// placed, since the same element can be a blocked cell under one layout and an open or slot cell under the next.
    /// </summary>
    public static class BlockedCell
    {
        private const string Name = "Mantle_blocked";
        private const float Dimmed = 0.55f;
        private const float Inset = 8f;
        private static readonly Color Grey = new Color(0.62f, 0.6f, 0.56f, 0.9f);

        public static void Mark(InventoryElement element, bool blocked)
        {
            CanvasGroup group = element.GetComponent<CanvasGroup>();
            if (group == null && blocked)
                group = element.gameObject.AddComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = blocked ? Dimmed : 1f;
                group.blocksRaycasts = !blocked;
                group.interactable = !blocked;
            }
            Transform cross = element.transform.Find(Name);
            if (cross == null && blocked)
                cross = Cross(element.transform);
            if (cross != null)
                cross.gameObject.SetActive(blocked);
        }

        /// <summary>The grey cross over the whole cell, a little inset, drawn over everything else in it.</summary>
        private static Transform Cross(Transform cell)
        {
            GameObject go = new GameObject(Name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(cell, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(Inset, Inset);
            rect.offsetMax = new Vector2(-Inset, -Inset);
            rect.SetAsLastSibling();
            Image image = go.GetComponent<Image>();
            image.sprite = CrossMark.Sprite;
            image.color = Grey;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect;
        }
    }
}
