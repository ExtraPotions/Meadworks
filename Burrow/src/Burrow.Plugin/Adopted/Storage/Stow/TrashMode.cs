using HarmonyLib;
using Burrow.Core;
using UnityEngine;

namespace Burrow.Stow
{
    /// <summary>
    /// Trash mode (the user's idea): Shift + click on the trash can turns the pointer into the trash can, and every
    /// click on a stack of the inventory or the open chest destroys it (the trash's own rules: favourites and worn items
    /// are kept, a viewed chest refuses, <c>Trash Uses Salvage</c> salvages); no confirmation, the held Shift is the
    /// confirmation. Letting go of Shift, closing the inventory, a popup or starting a drag ends it and gives the pointer
    /// back. A click on the can without Shift, or with a stack dragged, is the can's old use: destroy the dragged stack.
    /// </summary>
    public static class TrashMode
    {
        public static bool Active { get; private set; }

        /// <summary>The trash can's click: trash mode with Shift held and nothing dragged, the dragged stack otherwise.</summary>
        public static void CanClicked(InventoryGui gui)
        {
            if (ShiftHeld && gui.m_dragGo == null)
                Start(gui);
            else
                Trash.TrashDragged(gui);
        }

        /// <summary>Every frame of the inventory (<see cref="StowHotkeys"/>): ends the mode, or moves the pointer's can.</summary>
        public static void Tick(InventoryGui gui)
        {
            if (!Active)
                return;
            if (!ShiftHeld || !Keys.InventoryOpen || gui.m_dragGo != null || UnifiedPopup.IsVisible())
                Stop();
            else
                TrashCursor.Follow();
        }

        public static void Stop()
        {
            Active = false;
            TrashCursor.Hide();
        }

        private static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private static void Start(InventoryGui gui)
        {
            Active = true;
            // Changed by expDARE, 2026-10-20: the pointer shows the can's mode, the hammer while the trash salvages.
            TrashCursor.Show(gui, Burrow.Integration.TrashCanMode.Icon);
            Messages.TopLeft(StowWords.TrashModeOn);
        }

        /// <summary>
        /// A click on a grid while the mode is on: the stack under the pointer is destroyed and the game's click (a split,
        /// with Shift held) does not happen. Runs before every other click handler.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
        public static class ClickPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First + 1)]
            public static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item)
            {
                if (!Active)
                    return true;
                if (item != null)
                    Trash.TrashClicked(__instance, grid.GetInventory(), item);
                return false;
            }
        }
    }
}
