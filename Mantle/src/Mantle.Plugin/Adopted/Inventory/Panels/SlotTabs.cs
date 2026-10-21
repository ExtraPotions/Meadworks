using Mantle.Core;
using Mantle.Slots;

namespace Mantle.Panels
{
    /// <summary>
    /// Which tab of the slot panel shows (the user's call, 2026-09-28). Gear: the worn slots around the stat sheet
    /// (<see cref="GearStats"/>); Consumables: food, mead and ammo. Coins, Keys and Tackle share the Gear grid. The hidden tab's cells stay cells of the inventory, so items, weight, worn effects and graves do not
    /// change; only their elements are hidden. Kept for the session, never saved, Gear first. The gamepad moves over the
    /// inventory's cells rather than the picture, so a move onto a cell of the hidden tab turns to that tab.
    /// </summary>
    public static class SlotTabs
    {
        public static SlotTab Shown { get; private set; } = SlotTab.Gear;

        /// <summary>Turns to a tab; the elements are placed again on the next frame.</summary>
        public static void Show(SlotTab tab)
        {
            if (tab == Shown)
                return;
            Shown = tab;
            SlotElements.Invalidate();
        }

        /// <summary>The tab a slot kind is drawn in; retired cells stay hidden.</summary>
        public static SlotTab? Of(SlotKind kind)
        {
            switch (kind)
            {
                case SlotKind.Food:
                case SlotKind.Mead:
                case SlotKind.Ammo:
                    return SlotTab.Consumables;
                case SlotKind.Purse:
                case SlotKind.Key:
                case SlotKind.Tacklebox:
                case SlotKind.Tackle:
                case SlotKind.Ring:
                case SlotKind.Belt:
                    return SlotTab.Gear;
                case SlotKind.Retired:
                    return null;
                default:
                    return SlotTab.Gear;
            }
        }

        /// <summary>Every frame the grid is drawn: the gamepad's selection on a cell of the hidden tab turns to it.</summary>
        public static void FollowGamepad(InventoryGrid grid)
        {
            if (!grid.m_uiGroup.IsActive || !ZInput.IsExclusiveGamepadActive())
                return;
            Slot slot = InventoryState.Layout.SlotAt(grid.m_selected);
            SlotTab? tab = slot != null ? Of(slot.Kind) : null;
            if (tab.HasValue)
                Show(tab.Value);
        }
    }
}
