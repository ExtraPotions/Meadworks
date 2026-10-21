using System.Collections.Generic;
using ExtraPotions.Mantle.Embedded.PlateColumn;
using Mantle.Core;
using TimberBackground = ExtraPotions.Meadworks.Crucible.UI.TimberBackground;
using Mantle.Look;
using Mantle.Panels;
using Mantle.Slots;
using UnityEngine.UI;
using UnityEngine;

namespace Mantle.Ring
{
    /// <summary>
    /// The key ring's pop-up (the user's idea: the keys round a ring): a small round brown panel hanging under the slot
    /// panel, centred under the ring button where the slot panel allows (<see cref="PopupPlace"/>). Right of the slot panel the crafting panel drew
    /// over it (tried in game 2026-09-28); under it the grid is clear, and an open chest's panel, which hangs under the
    /// inventory panel, is cleared by pushing the pop-up right when a wide chest reaches under the slot panel. A bronze
    /// wire round the ring's icon is always there, an empty ring while the player carries no key (the user's call); the
    /// cells of the keys carried hang on it (<see cref="KeyRingCells"/>, <see cref="KeyRingCircle"/>), smaller than the
    /// grid's. With Brown Style off the panel is the game's square one. A child of the player panel, so it moves and
    /// closes with it; its background takes clicks, so a dragged item let go on it is not dropped on the ground. Shown
    /// while <see cref="KeyRingState.Open"/>.
    /// </summary>
    public static class KeyRingPopup
    {
        public const string Name = "Mantle_keyring";
        private static readonly Color HubTint = new Color(1f, 1f, 1f, 0.35f);
        private static readonly List<int> found = new List<int>();
        private static readonly List<int> arranged = new List<int>();
        private static float slotLeft;
        private static float ringCentre;
        private static float top;
        private static float step = 70f;
        private static bool placed;
        private static bool fresh;

        public static RectTransform Find(InventoryGui gui) => gui != null ? gui.m_player.Find(Name) as RectTransform : null;

        /// <summary>
        /// Where the pop-up hangs, under the slot panel and the ring button, worked out when the grid is placed; it is
        /// sized and filled on the next frame. Hidden without ring cells.
        /// </summary>
        public static void Place(InventoryGui gui, RectTransform slotPanel, SlotPanelLayout plan, float gridStep)
        {
            RectTransform popup = Find(gui);
            placed = slotPanel != null && plan?.RingCell != null;
            fresh = true;
            if (!placed)
            {
                popup?.gameObject.SetActive(false);
                return;
            }
            if (popup == null)
                Make(gui);
            step = gridStep;
            slotLeft = slotPanel.anchoredPosition.x;
            ringCentre = slotLeft + SlotPanel.CellPosition(plan.RingCell.Value, step).x + Column.BoxSize / 2f;
            top = slotPanel.anchoredPosition.y - slotPanel.sizeDelta.y - PanelDress.Gap;
        }

        /// <summary>Every frame the grid is drawn: shown while open, laid out again when the keys carried change.</summary>
        public static void Refresh(InventoryGui gui)
        {
            RectTransform popup = Find(gui);
            if (popup == null)
                return;
            bool show = placed && KeyRingState.Open && KeyRing.Active && KeyRingCells.Count > 0;
            if (popup.gameObject.activeSelf != show)
                popup.gameObject.SetActive(show);
            if (!show)
                return;
            KeyRingCells.Found(found);
            if (fresh || !Same(found, arranged))
                Arrange(popup);
            PopupPlace.Under(gui, popup, slotLeft, ringCentre, top);
            KeyRingCells.Refresh(InventoryState.Player.GetInventory());
        }

        /// <summary>
        /// The look changed (Timber, Brown or off): the shared repaint (<see cref="GridSkin.RefreshLook"/>) dresses every game
        /// panel, the ring's too, as a square, so the ring is skinned again now and laid out again on its next frame.
        /// </summary>
        public static void Restyle()
        {
            fresh = true;
            RectTransform popup = Find(InventoryGui.instance);
            if (popup != null)
                Skin(popup.GetComponent<Image>());
        }

        private static void Arrange(RectTransform popup)
        {
            fresh = false;
            arranged.Clear();
            arranged.AddRange(found);
            float radius = KeyRingCircle.Radius(found.Count, step * KeyRingCircle.CellScale);
            float size = KeyRingCircle.Diameter(radius, Column.BoxSize * KeyRingCircle.CellScale);
            popup.sizeDelta = new Vector2(size, size);
            Skin(popup.GetComponent<Image>());
            Dress(popup, radius);
            for (int number = 1; number <= KeyRingCells.Count; number++)
                Hang(KeyRingCells.At(number), found.IndexOf(number), radius);
        }

        private static void Hang(InventoryElement element, int index, float radius)
        {
            if (element == null)
                return;
            element.gameObject.SetActive(index >= 0);
            if (index >= 0)
                ((RectTransform)element.transform).anchoredPosition = KeyRingCircle.Position(index, found.Count, radius);
        }

        /// <summary>The wire through the cells' centres and the ring's icon in the middle, with or without keys on it.</summary>
        private static void Dress(RectTransform popup, float radius)
        {
            Image wire = popup.Find("wire").GetComponent<Image>();
            wire.sprite = SkinArt.RingLine;
            wire.enabled = wire.sprite != null;
            wire.rectTransform.sizeDelta = Vector2.one * radius * KeyRingCircle.WireScale;
            Image hub = popup.Find("hub").GetComponent<Image>();
            hub.sprite = SlotIcons.For(SlotKind.Key);
            hub.enabled = hub.sprite != null;
        }

        /// <summary>
        /// Timber wears the gear panel's forged wood and iron frame (<see cref="GridSkin.GamePanel"/>, playtest 2026-10-20);
        /// Brown keeps its disc, vanilla its square. The game-panel call comes first in every look, so leaving Timber also
        /// takes the iron frame off before Brown's disc goes on.
        /// </summary>
        private static void Skin(Image image)
        {
            GridSkin.GamePanel(image);
            if (SkinArt.Timber)
                return;
            TimberBackground.Apply(image, false);
            Sprite disc = GridSkin.On ? SkinArt.RingPanel : null;
            if (disc == null)
                return;
            image.sprite = disc;
            image.type = Image.Type.Simple;
            image.material = null;
            image.color = Color.white;
        }

        private static void Make(InventoryGui gui)
        {
            RectTransform popup = SlotPanel.MakePanel(gui, Name);
            popup.pivot = new Vector2(0f, 1f);
            GridSkin.GamePanel(popup.GetComponent<Image>());   // remembers the game's sprite while it still shows it
            Child(popup, "wire", Vector2.zero);
            Image hub = Child(popup, "hub", Vector2.one * Column.BoxSize * 0.6f * KeyRingCircle.CellScale);
            hub.color = HubTint;
            hub.preserveAspect = true;
        }

        private static Image Child(RectTransform popup, string name, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(popup, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static bool Same(List<int> a, List<int> b)
        {
            if (a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }
    }
}
