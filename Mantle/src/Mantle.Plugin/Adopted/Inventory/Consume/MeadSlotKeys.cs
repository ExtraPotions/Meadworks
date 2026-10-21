using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using ExtraPotions.Mantle.Embedded.Hotkeys;
using Mantle.Core;
using Mantle.Slots;

namespace Mantle.Consume
{
    /// <summary>
    /// The Mead Slot keys (issue #16, 2026-10-05: a player wanted to choose which mead to drink; the user: "per slot keys
    /// ... up to 5 meads (default 3), maybe like ALT+key"): one key per Mead slot, counted left to right, Left Alt + 1 to 5
    /// by default, each drinking the mead in its own slot the way the hotbar's number keys use their item, through
    /// <c>Humanoid.UseItem(inventory, item, fromInventoryGui: true)</c>, so the game's own message tells why a mead cannot be
    /// drunk now. Read with the Food Key (<see cref="ConsumeKeys"/>), under the same rules. The game reads
    /// its hotbar keys without modifiers, so Alt + 1 is also its Hotbar 1 (the user, 2026-10-05: "while holding alt we need
    /// to make sure that doesn't happen"): the <c>Player.UseHotbarItem</c> prefix uses nothing while Alt is held
    /// (<see cref="HoldsHotbar"/>), whatever the press does for the meads.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.UseHotbarItem))]
    public static class MeadSlotKeys
    {
        /// <summary>The Mead slot (0 is the leftmost) whose key went down this frame, or -1; only a slot that exists counts.</summary>
        public static int Pressed()
        {
            IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(SlotKind.Mead);
            int count = Math.Min(cells.Count, ConsumeSettings.MeadSlotKeys.Length);
            for (int slot = 0; slot < count; slot++)
            {
                if (Hotkey.Pressed(ConsumeSettings.MeadSlotKeys[slot]))
                    return slot;
            }
            return -1;
        }

        /// <summary>The mead in this Mead slot drunk, as the hotbar would; an empty slot says so.</summary>
        public static void Drink(Player player, int slot)
        {
            Vector2i cell = InventoryState.CellsOf(SlotKind.Mead)[slot];
            Inventory inventory = player.GetInventory();
            ItemDrop.ItemData item = inventory.GetItemAt(cell.x, cell.y);
            if (item == null)
                Messages.Center(ConsumeWords.SlotEmpty);
            else
                player.UseItem(inventory, item, true);
        }

        /// <summary>
        /// A Mead Slot key half pressed: its modifiers held (Left Alt by default), or a key without modifiers held itself.
        /// While so, the hotbar uses nothing, so Alt + 1 to 8 never equip, take off or use a hotbar item, whether or not
        /// the slot exists, a build tool is in hand or the game sees the number in another frame than Mantle.
        /// </summary>
        public static bool HoldsHotbar()
        {
            foreach (ConfigEntry<KeyboardShortcut> key in ConsumeSettings.MeadSlotKeys)
            {
                if (Hotkey.ModifiersHeld(key) || Hotkey.Held(key))
                    return true;
            }
            return false;
        }

        [HarmonyPrefix]
        private static bool Prefix(Player __instance) => !(ConsumeKeys.Works(__instance) && HoldsHotbar());
    }
}
