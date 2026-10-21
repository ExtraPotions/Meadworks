using System.Collections.Generic;
using Mantle.Slots;
using UnityEngine.UI;
using UnityEngine;

namespace Mantle.Ring
{
    /// <summary>
    /// The ring cells' elements and what they show. When the grid is placed, <see cref="ElementPlacer"/> hands each ring
    /// cell's element over to the pop-up (<see cref="Adopt"/>), drawn at <see cref="KeyRingCircle.CellScale"/> of the
    /// grid's size: the game's grid still owns it, so clicks, drags, the game's tooltip and the gamepad work as on any
    /// cell. Only cells holding a key are on the ring (the user's call: a key shows up when the player gets it and goes
    /// when the last one is used or moved away); a key being dragged stays in its cell until it lands, so its cell stays.
    /// Every frame a key shows its count alone, and only above one, like the purse.
    /// </summary>
    public static class KeyRingCells
    {
        private static readonly List<InventoryElement> elements = new List<InventoryElement>();

        /// <summary>How many ring cells were handed over, the first at index 0.</summary>
        public static int Count => elements.Count;

        public static InventoryElement At(int number) => number >= 1 && number <= elements.Count ? elements[number - 1] : null;

        /// <summary>The grid is being placed again: the cells to keep are the ones handed over from now on.</summary>
        public static void Clear() => elements.Clear();

        /// <summary>Moves a ring cell's element into the pop-up; false when there is no pop-up to take it.</summary>
        public static bool Adopt(InventoryGui gui, InventoryElement element, Slot slot)
        {
            RectTransform popup = KeyRingPopup.Find(gui);
            if (popup == null || slot.Number < 1)
                return false;
            while (elements.Count < slot.Number)
                elements.Add(null);
            elements[slot.Number - 1] = element;
            RectTransform rect = (RectTransform)element.transform;
            rect.SetParent(popup, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one * KeyRingCircle.CellScale;
            Image background = element.GetComponent<Image>();
            if (background != null)
                background.color = Color.white;
            return true;
        }

        /// <summary>The ring numbers with a cell on the ring: the cells holding a key (the game marks them used each frame).</summary>
        public static void Found(List<int> found)
        {
            found.Clear();
            for (int number = 1; number <= elements.Count; number++)
            {
                if (elements[number - 1] != null && elements[number - 1].m_used)
                    found.Add(number);
            }
        }

        /// <summary>Whether ring cell <paramref name="number"/> is on the open pop-up.</summary>
        public static bool Shows(int number)
        {
            InventoryElement element = At(number);
            return element != null && element.gameObject.activeInHierarchy;
        }

        /// <summary>Every frame the ring shows: a key's count alone ("3"), only above one.</summary>
        public static void Refresh(Inventory inventory)
        {
            foreach (InventoryElement element in elements)
            {
                if (element == null || !element.m_used || !element.gameObject.activeSelf)
                    continue;
                ItemDrop.ItemData key = inventory.GetItemAt(element.Position.x, element.Position.y);
                element.m_amount.enabled = key != null && key.m_stack > 1;
                if (element.m_amount.enabled)
                    element.m_amount.text = key.m_stack.ToString();
            }
        }
    }
}
