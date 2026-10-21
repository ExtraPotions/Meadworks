// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using Mantle.Slots;

namespace Mantle.Layout
{
    /// <summary>What the settings and the box in the Tacklebox slot ask of the slot list: the counts and switches only, no game types.</summary>
    public sealed class SlotPlanInput
    {
        public bool Equipment { get; set; }

        /// <summary>A Feet slot after Legs (EliteEquipment's boots); only with <see cref="Equipment"/>.</summary>
        public bool Feet { get; set; }

        public bool Backpack { get; set; }
        public int Utility { get; set; }
        public bool Trinket { get; set; }

        /// <summary>Ring cells, 0 to 2.</summary>
        public int Rings { get; set; }

        public bool Belt { get; set; }
        public int Food { get; set; }
        public int Mead { get; set; }
        public int Ammo { get; set; }
        public bool Purse { get; set; }

        /// <summary>One ring cell per key on the key ring; 0 without the ring.</summary>
        public int Keys { get; set; }

        /// <summary>The Tacklebox slot is on.</summary>
        public bool Tacklebox { get; set; }

        /// <summary>The cells the box in the slot gives; ignored without the Tacklebox slot.</summary>
        public int TackleCells { get; set; }
    }

    /// <summary>
    /// The order of the slots in the player's inventory (see <see cref="LayoutBuilder"/>): Head, Chest, Legs, Feet (with EliteEquipment's boots), Back, Backpack,
    /// Utility, Trinket, Ring, Belt, Food, Mead, Ammo, Purse, one ring cell per key, the Tacklebox slot and last the box's cells, because
    /// their count changes with the box, so no other slot moves when it does.
    /// </summary>
    public static class SlotPlan
    {
        public static List<Slot> Build(SlotPlanInput input)
        {
            List<Slot> slots = new List<Slot>();
            if (input.Equipment)
            {
                Add(slots, 1, SlotKind.Head, SlotKind.Chest, SlotKind.Legs);
                if (input.Feet)
                    Add(slots, 1, SlotKind.Feet);
                Add(slots, 1, SlotKind.Back);
            }
            if (input.Backpack)
                Add(slots, 1, SlotKind.Backpack);
            Add(slots, input.Utility, SlotKind.Utility);
            if (input.Trinket)
                Add(slots, 1, SlotKind.Trinket);
            Add(slots, input.Rings, SlotKind.Ring);
            if (input.Belt)
                Add(slots, 1, SlotKind.Belt);
            Add(slots, input.Food, SlotKind.Food);
            Add(slots, input.Mead, SlotKind.Mead);
            Add(slots, input.Ammo, SlotKind.Ammo);
            if (input.Purse)
                Add(slots, 1, SlotKind.Purse);
            Add(slots, input.Keys, SlotKind.Key);
            if (input.Tacklebox)
            {
                Add(slots, 1, SlotKind.Tacklebox);
                Add(slots, input.TackleCells, SlotKind.Tackle);
            }
            return slots;
        }

        /// <summary>Numbers 1 to <paramref name="count"/> of every kind given.</summary>
        private static void Add(List<Slot> slots, int count, params SlotKind[] kinds)
        {
            foreach (SlotKind kind in kinds)
            {
                for (int number = 1; number <= count; number++)
                    slots.Add(new Slot(kind, number));
            }
        }
    }
}
