using System;
using Mantle.Core;

namespace Mantle.Slots
{
    /// <summary>Independent consumable storage counts, with the legacy Utility override retained.</summary>
    public static class SlotCounts
    {
        public static int Utility => Group(InventorySettings.UtilitySlots.Value);
        public static int Food => FoodCount.Slots();
        public static int Mead => Math.Max(0, Math.Min(5, InventorySettings.MeadSlots.Value));
        public static int Ammo => Math.Max(0, Math.Min(5, InventorySettings.AmmoSlots.Value));

        /// <summary>True while Slots Per Group overrides Utility.</summary>
        public static bool Shared => InventorySettings.SlotsPerGroup.Value > 0;

        private static int Group(int own)
        {
            int count = Shared ? InventorySettings.SlotsPerGroup.Value : own;
            return Math.Max(0, Math.Min(InventorySettings.MaxGroup, count));
        }
    }
}
