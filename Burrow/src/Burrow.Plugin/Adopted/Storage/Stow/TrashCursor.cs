using UnityEngine;
using UnityEngine.UI;

namespace Burrow.Stow
{
    /// <summary>
    /// The trash can as the pointer. The game never sets a cursor image (it only shows or hides the system arrow every
    /// frame through <c>ZCursor</c>), so the arrow is swapped for a fully transparent cursor image while the mode is on,
    /// and the can is drawn centred on the pointer, as big as the can on the trash button, the way the game draws a dragged item (<c>InventoryGui.UpdateItemDrag</c>:
    /// a child of the inventory screen, placed at <c>ZInput.pointerPosition</c> every frame), on top of everything and
    /// taking no clicks. Hiding restores the system arrow.
    /// </summary>
    public static class TrashCursor
    {
        private const string Name = "Burrow_trashcursor";

        /// <summary>The size when the trash can's own icon cannot be measured.</summary>
        private const float FallbackSize = 26f;

        private static RectTransform can;
        private static Texture2D clear;

        /// <summary>The can on the pointer, as big as the can's icon on its button (the user's call), where there is one.</summary>
        public static void Show(InventoryGui gui, Sprite bin)
        {
            if (can == null)
                can = Make(gui.transform, bin);
            // Changed by expDARE, 2026-10-20: the icon is set on every show, so the pointer follows the can's mode.
            can.GetComponent<Image>().sprite = bin;
            can.sizeDelta = Vector2.one * IconSize(gui);
            can.gameObject.SetActive(true);
            can.SetAsLastSibling();
            Cursor.SetCursor(Clear(), Vector2.zero, CursorMode.Auto);
            Follow();
        }

        public static void Follow()
        {
            if (can != null)
                can.position = ZInput.pointerPosition;
        }

        public static void Hide()
        {
            if (can != null)
                can.gameObject.SetActive(false);
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private static RectTransform Make(Transform screen, Sprite bin)
        {
            GameObject go = new GameObject(Name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(screen, false);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Image image = go.GetComponent<Image>();
            image.sprite = bin;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>The drawn size of the bin on the trash can's button (its icon child), or the fallback.</summary>
        private static float IconSize(InventoryGui gui)
        {
            RectTransform icon = gui.m_player.Find("Burrow_trashcan/icon") as RectTransform;
            float size = icon != null ? Mathf.Min(icon.rect.width, icon.rect.height) : 0f;
            return size > 1f ? size : FallbackSize;
        }

        /// <summary>A small fully transparent cursor image, made once (readable RGBA32, as a cursor needs).</summary>
        private static Texture2D Clear()
        {
            if (clear != null)
                return clear;
            clear = new Texture2D(8, 8, TextureFormat.RGBA32, false) { name = Name };
            Color32[] pixels = new Color32[64];
            clear.SetPixels32(pixels);
            clear.Apply(false, false);
            return clear;
        }
    }
}
