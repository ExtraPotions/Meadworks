using System;
using System.Collections.Generic;
using System.Text;
using Mantle.Core;
using Mantle.Panels;
using Mantle.Slots;
using UnityEngine;
using UnityEngine.UI;

namespace Mantle.Consume
{
    /// <summary>
    /// The Mead Slot keys on the Food and Mead bar (the user, 2026-10-05: "mead drink hotkeys need to be next to the food
    /// eat key in that bottom left"): after the food and mead squares, one square per Mead slot whose key is set, left to
    /// right, with its key over the corner and the mead lying in that slot as its icon, so the player sees which key drinks
    /// which mead. An empty slot shows the mead slot icon, faded. The icons follow the slots on every check of the bar
    /// (<see cref="ConsumeBar"/>); the squares are built again only when a slot or a key comes or goes.
    /// </summary>
    public static class ConsumeBarSlots
    {
        private const float EmptyAlpha = 0.35f;

        /// <summary>Each Mead slot's icon on the bar, by slot; null for a slot without a key.</summary>
        private static readonly List<Image> icons = new List<Image>();

        /// <summary>The slots' part of the bar's plan: each drawn slot and its key.</summary>
        public static void Plan(StringBuilder plan)
        {
            int count = Count();
            for (int slot = 0; slot < count; slot++)
            {
                string key = Key(slot);
                if (key.Length > 0)
                    plan.Append("slot").Append(slot).Append(' ').Append(key).Append('|');
            }
        }

        /// <summary>A square for each slot with a key, the first at <paramref name="x"/>.</summary>
        public static void Build(RectTransform bar, GameObject template, float x, float gap)
        {
            icons.Clear();
            int count = Count();
            for (int slot = 0; slot < count; slot++)
            {
                string key = Key(slot);
                if (key.Length == 0)
                {
                    icons.Add(null);
                    continue;
                }
                icons.Add(ConsumeBarCell.Square(bar, template, "Mantle_meadslot" + (slot + 1), key, x));
                x += ConsumeBarCell.Size + gap;
            }
        }

        /// <summary>Each square shows what its slot holds now.</summary>
        public static void Refresh(Player player)
        {
            IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(SlotKind.Mead);
            Inventory inventory = player.GetInventory();
            for (int slot = 0; slot < icons.Count && slot < cells.Count; slot++)
            {
                if (icons[slot] != null)
                    Show(icons[slot], inventory.GetItemAt(cells[slot].x, cells[slot].y));
            }
        }

        private static void Show(Image icon, ItemDrop.ItemData item)
        {
            Sprite sprite = item != null ? item.GetIcon() : SlotIcons.For(SlotKind.Mead);
            if (icon.sprite != sprite)
                icon.sprite = sprite;
            icon.color = item != null ? Color.white : new Color(1f, 1f, 1f, EmptyAlpha);
            icon.enabled = sprite != null;
        }

        private static int Count() => Math.Min(InventoryState.CellsOf(SlotKind.Mead).Count, ConsumeSettings.MeadSlotKeys.Length);

        private static string Key(int slot) => ConsumeKeyNames.Mead(slot);
    }
}
