using System.Collections.Generic;
using Mantle.Slots;
using UnityEngine;
using UnityEngine.UI;

namespace Mantle.Tackle
{
    /// <summary>
    /// The tacklebox's cells' elements and what they show. When the grid is placed, <see cref="Panels.ElementPlacer"/> hands
    /// each tackle cell's element over to the pop-up (<see cref="Adopt"/>), drawn at <see cref="CellScale"/> of the grid's
    /// size in rows of <see cref="Columns"/>: the game's grid still owns it, so clicks, drags, the game's tooltip, its
    /// equipped mark on the bait in use and the gamepad work as on any cell. Unlike the key ring every cell shows, empty
    /// ones with their caption, so a bait can be dropped into any of them. Every frame a stack shows its count alone, and
    /// only above one, like the ring and the purse.
    /// </summary>
    public static class TackleCells
    {
        public const float CellScale = 0.8f;
        public const int Columns = 4;

        private static readonly List<InventoryElement> elements = new List<InventoryElement>();

        /// <summary>How many cells were handed over, the first at index 0.</summary>
        public static int Count => elements.Count;

        public static InventoryElement At(int number) => number >= 1 && number <= elements.Count ? elements[number - 1] : null;

        /// <summary>The grid is being placed again: the cells to keep are the ones handed over from now on.</summary>
        public static void Clear() => elements.Clear();

        /// <summary>Moves a tackle cell's element into the pop-up; false when there is no pop-up to take it.</summary>
        public static bool Adopt(InventoryGui gui, InventoryElement element, Slot slot)
        {
            RectTransform popup = TacklePopup.Find(gui);
            if (popup == null || slot.Number < 1)
                return false;
            while (elements.Count < slot.Number)
                elements.Add(null);
            elements[slot.Number - 1] = element;
            RectTransform rect = (RectTransform)element.transform;
            rect.SetParent(popup, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.localScale = Vector3.one * CellScale;
            Image background = element.GetComponent<Image>();
            if (background != null)
                background.color = Color.white;
            return true;
        }

        /// <summary>Each cell at its place, in number order, rows of <see cref="Columns"/> from the top left; pad and step already scaled.</summary>
        public static void Arrange(float pad, float step)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i] != null)
                    ((RectTransform)elements[i].transform).anchoredPosition = new Vector2(pad + i % Columns * step, -(pad + i / Columns * step));
            }
        }

        /// <summary>Whether tackle cell <paramref name="number"/> is on the open pop-up.</summary>
        public static bool Shows(int number)
        {
            InventoryElement element = At(number);
            return element != null && element.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Every frame the pop-up shows: a stack's count alone ("37"), only above one. The game's own writes in
        /// <c>InventoryGrid.UpdateGui</c> carry this (<see cref="Panels.SlotElements.PurseAmount"/>), so the label gets one
        /// text and one state per frame instead of the game's "37/100" and then Mantle's "37".
        /// </summary>
        public static void Refresh(Inventory inventory)
        {
            if (Panels.SlotElements.PurseAmount.AmountRoutes != 0)
                return;
            foreach (InventoryElement element in elements)
            {
                if (element == null || !element.m_used)
                    continue;
                ItemDrop.ItemData item = inventory.GetItemAt(element.Position.x, element.Position.y);
                element.m_amount.enabled = item != null && item.m_stack > 1;
                if (element.m_amount.enabled)
                    element.m_amount.text = StackCounts.Of(item.m_stack);
            }
        }

        /// <summary>When <paramref name="label"/> is a used tackle cell's count: whether it shows, and its text.</summary>
        public static bool Amount(TMPro.TMP_Text label, Inventory inventory, out bool show, out string text)
        {
            show = false;
            text = null;
            for (int i = 0; i < elements.Count; i++)
            {
                InventoryElement element = elements[i];
                if (element == null || !ReferenceEquals(element.m_amount, label))
                    continue;
                if (!element.m_used)
                    return true;
                ItemDrop.ItemData item = inventory.GetItemAt(element.Position.x, element.Position.y);
                show = item != null && item.m_stack > 1;
                text = show ? StackCounts.Of(item.m_stack) : null;
                return true;
            }
            return false;
        }
    }

    /// <summary>Stack counts as text, made once per number (the pop-ups write them every frame).</summary>
    internal static class StackCounts
    {
        private static readonly string[] made = new string[1000];

        public static string Of(int count)
        {
            if (count < 0 || count >= made.Length)
                return count.ToString();
            return made[count] ?? (made[count] = count.ToString());
        }
    }
}
