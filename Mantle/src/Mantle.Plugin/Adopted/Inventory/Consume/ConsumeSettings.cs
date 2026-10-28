using BepInEx.Configuration;
using ExtraPotions.Mantle.Embedded.Hotkeys;
using Mantle.Core;
using ExtraPotions.Mantle.Embedded.SyncedConfig;
using UnityEngine;

namespace Mantle.Consume
{
    /// <summary>
    /// The keys of section "2. Slots" that eat and drink from the slots (<see cref="ConsumeKeys"/>, <see cref="MeadSlotKeys"/>),
    /// and in "5. Look" the bar that shows them on the screen (<see cref="ConsumeBar"/>). Keys are
    /// each player's own, so they are not synced. Left Alt
    /// is free in the game; the game's hotbar keys ignore it, so <see cref="MeadSlotKeys"/> holds the hotbar back while it is held.
    /// Mantle, 2026-10-10 (parity choice #29): the Mead Slot keys replace the Mead Key (B, every mead at once), as in
    /// PackPanel 0.15. They default to PackPanel's Left Alt + 1 to 5 (the user, 2026-10-12 17:40: match Solo, an exception to
    /// the unbound-keys rule for these keys only).
    /// </summary>
    public static class ConsumeSettings
    {
        public static ConfigEntry<KeyboardShortcut> FoodKey { get; private set; }

        /// <summary>One key per Mead slot, left to right, as many as the most Mead slots there can be.</summary>
        public static ConfigEntry<KeyboardShortcut>[] MeadSlotKeys { get; } = new ConfigEntry<KeyboardShortcut>[InventorySettings.MaxGroup];

        public static ConfigEntry<bool> Bar { get; private set; }

        public static void Bind(SyncedConfiguration synced)
        {
            FoodKey = synced.Bind(InventorySettings.SlotsSection, "Food Key", KeyboardShortcut.Empty,
                "Outside the inventory, with no hammer, hoe or cultivator in hand: eats every food in the Food slots that can be eaten now, left to right. Unbound by default to prevent key conflicts. Per player.", synced: false);
            BindMeadSlotKeys(synced);
            Bar = synced.Bind(InventorySettings.LookSection, "Food And Mead Bar", true,
                "In the bottom-left corner of the screen, under your health: a food square with the Food Key over it, then a square per Mead slot that has a key, showing its mead with its Mead Slot key over it.", synced: false);
        }

        /// <summary>Mead Slot 1 Key to Mead Slot 5 Key, Left Alt + 1 to 5 by default.</summary>
        private static void BindMeadSlotKeys(SyncedConfiguration synced)
        {
            for (int i = 0; i < MeadSlotKeys.Length; i++)
            {
                int number = i + 1;
                MeadSlotKeys[i] = synced.Bind(InventorySettings.SlotsSection, $"Mead Slot {number} Key", new KeyboardShortcut(KeyCode.Alpha1 + i, KeyCode.LeftAlt),
                    $"Outside the inventory, with no hammer, hoe or cultivator in hand: drinks the mead in Mead slot {number} (counted left to right), if it can be drunk now. While this key's modifiers are held (Left Alt by default), the number keys use no hotbar item. Per player.", synced: false);
            }
        }
    }
}
