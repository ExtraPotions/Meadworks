using System;
using System.Collections.Generic;
using ExtraPotions.Mantle.Embedded.PlateColumn;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mantle.Panels
{
    /// <summary>
    /// Vanilla gray squares behind compact status icons, with small centered value overlays. Dressed every frame, so
    /// each box's parts are looked up once and kept (<see cref="BoxParts"/>): no names read, no searches and no garbage per frame.
    /// </summary>
    public static class StatIconFrames
    {
        private const string Name = "Mantle_stat_icon_frame";
        private static readonly Dictionary<Image, Color> colours = new Dictionary<Image, Color>();
        private static readonly Dictionary<RectTransform, BoxParts> parts = new Dictionary<RectTransform, BoxParts>();
        private static readonly List<RectTransform> column = new List<RectTransform>();
        private static readonly List<RectTransform> gone = new List<RectTransform>();
        private static Transform owner;

        /// <summary>
        /// Every box of the column, the game's armour and weight among them: those stay on the player panel, pinned over
        /// seats in the container, so the boxes come from the library rather than from the container's children.
        /// </summary>
        public static void Apply(InventoryGui gui, RectTransform boxes, bool on)
        {
            if (owner != boxes) { colours.Clear(); StatIconLayout.Restore(); owner = boxes; Prune(); }
            if (!on) StatIconLayout.Restore();
            Column.BoxesIn(boxes, column);
            foreach (RectTransform box in column)
            {
                BoxParts bits = PartsOf(box);
                bool target = bits.Value == gui.m_armor || bits.Value == gui.m_weight || bits.WorldTier || box.name == "Trash";
                Dress(gui, box, on && target, true);
            }
        }

        /// <summary>A HUD column box this dresses: the armour and weight copies and the world tier.</summary>
        public static bool IsHudStat(RectTransform box) => PartsOf(box).HudStat;

        /// <summary>The same frame and centered value are used by the HUD copies, which stay dressed (<paramref name="restorable"/> false).</summary>
        public static void Dress(InventoryGui gui, RectTransform box, bool on, bool restorable)
        {
            BoxParts bits = PartsOf(box);
            Image background = bits.Background != null ? bits.Background : bits.Background = Background(box);
            if (background == null) return;
            if (bits.Frame == null) bits.Frame = background.transform.Find(Name);
            if (on && bits.Frame == null) bits.Frame = Create(background.transform, gui);
            if (bits.Frame == null) return;
            // A new plate can be copied from the already styled armor plate.
            if (!colours.TryGetValue(background, out Color colour))
                colours[background] = colour = background.color.a == 0f ? Color.white : background.color;
            background.color = on ? Color.clear : colour;
            if (bits.Frame.gameObject.activeSelf != on) bits.Frame.gameObject.SetActive(on);
            if (bits.Value == null) bits.Value = box.GetComponentInChildren<TMP_Text>(true);
            if (on) StatIconLayout.Apply(box, background, bits.Value, bits.Value == gui.m_weight || bits.Weight, restorable);
        }

        private static BoxParts PartsOf(RectTransform box)
        {
            if (!parts.TryGetValue(box, out BoxParts found))
            {
                found = new BoxParts(box.name) { Value = box.GetComponentInChildren<TMP_Text>(true) };
                parts[box] = found;
            }
            return found;
        }

        // Boxes go with their HUD or inventory (a new world builds new ones): their parts are dropped with them.
        private static void Prune()
        {
            gone.Clear();
            foreach (RectTransform box in parts.Keys)
                if (box == null) gone.Add(box);
            foreach (RectTransform box in gone) parts.Remove(box);
        }

        private static Image Background(Transform box)
        {
            for (int i = 0; i < box.childCount; i++)
            {
                Image image = box.GetChild(i).GetComponent<Image>();
                if (image != null && image.sprite != null && image.sprite.name == Skin.Box.name) return image;
            }
            return box.name == "Trash" ? box.Find("bkg")?.GetComponent<Image>() : null;
        }

        private static Transform Create(Transform parent, InventoryGui gui)
        {
            GameObject go = new GameObject(Name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            Image vanilla = gui.m_takeAllButton.GetComponent<Image>();
            image.sprite = vanilla.sprite;
            image.material = vanilla.material;
            image.color = vanilla.color;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>One box's pieces, found once: its name's kind, the gray square, our frame on it and its value text.</summary>
        private sealed class BoxParts
        {
            public BoxParts(string name)
            {
                Weight = name.EndsWith("_mantle_weight", StringComparison.Ordinal);
                WorldTier = name.EndsWith("_world_tier", StringComparison.Ordinal);
                HudStat = Weight || WorldTier || name.EndsWith("_mantle_armor", StringComparison.Ordinal);
            }

            public bool Weight { get; }
            public bool WorldTier { get; }
            public bool HudStat { get; }
            public Image Background;
            public Transform Frame;
            public TMP_Text Value;
        }
    }
}
