using Mantle.Layout;
using UnityEngine;

namespace Mantle.Core
{
    /// <summary>
    /// When the local player's frame checks what is worn (<see cref="PlayerTick"/>: the backpack and the tacklebox in their
    /// slots, Auto Equip's gear cells): on the frame the inventory changed (<see cref="InventoryWatch"/>) or the layout
    /// was applied again, and otherwise every quarter second.
    /// The quarter second retries an equip the game refused (an attack, a swim) and picks up what no inventory change
    /// reports (a setting, an enchant, another mod taking a piece off). Those checks look up a dozen cells and walk the
    /// inventory, too much for every frame.
    /// </summary>
    public static class WearGate
    {
        private const float Interval = 0.25f;

        private static readonly InventoryWatch watch = new InventoryWatch();
        private static InventoryLayout checkedLayout;
        private static float next;

        public static bool Due(Player player)
        {
            bool changed = watch.Changed(player.GetInventory());   // asked every frame, so no change is missed
            InventoryLayout layout = InventoryState.Layout;
            if (!changed && ReferenceEquals(layout, checkedLayout) && Time.time < next)
                return false;
            checkedLayout = layout;
            next = Time.time + Interval;
            return true;
        }
    }
}
