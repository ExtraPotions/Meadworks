using BepInEx.Configuration;
using Burrow.Core;
using ExtraPotions.Burrow.Embedded.SyncedConfig;
using UnityEngine;

namespace Burrow.Stow
{
    /// <summary>
    /// Section "2. Stow". Ranges, the nearby switch and the ground pickup rules change gameplay and are synced and
    /// lockable; hotkeys, sort orders, confirmations and the overlay are per player. Values are read at use time.
    /// </summary>
    public static class StowSettings
    {
        public const string Section = "2. Stow";

        public static ConfigEntry<bool> Enabled { get; private set; }
        public static ConfigEntry<KeyboardShortcut> QuickStackKey { get; private set; }
        public static ConfigEntry<bool> QuickStackNearby { get; private set; }
        public static ConfigEntry<KeyboardShortcut> StoreAllKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> TakeAllKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> TopUpKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> SortInventoryKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> SortContainerKey { get; private set; }
        public static ConfigEntry<SortOrder> SortOrder { get; private set; }
        public static ConfigEntry<int> MainInventoryRows { get; private set; }
        public static ConfigEntry<bool> AutoSortContainers { get; private set; }
        public static ConfigEntry<bool> AutoSortInventory { get; private set; }
        public static ConfigEntry<KeyboardShortcut> FavouriteItemKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> FavouriteSlotKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> JunkKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> TrashKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> DestroyJunkKey { get; private set; }
        public static ConfigEntry<bool> ConfirmTrash { get; private set; }
        public static ConfigEntry<bool> TrashUsesSalvage { get; private set; }
        public static ConfigEntry<bool> TrashOnStatColumn { get; private set; }
        public static ConfigEntry<bool> AutoTidy { get; private set; }
        public static ConfigEntry<KeyboardShortcut> FeedSourceKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> RouteModifier { get; private set; }
        public static ConfigEntry<KeyboardShortcut> StoreOneKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> FindKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> DumpKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> CyclePreviousKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> CycleNextKey { get; private set; }
        public static ConfigEntry<bool> CycleWithWheel { get; private set; }
        public static ConfigEntry<float> NearbyRange { get; private set; }
        public static ConfigEntry<bool> ShowFavourites { get; private set; }
        public static ConfigEntry<bool> GroundPickup { get; private set; }
        public static ConfigEntry<float> PickupRange { get; private set; }
        public static ConfigEntry<float> PickupInterval { get; private set; }
        public static ConfigEntry<float> PickupDelay { get; private set; }
        public static ConfigEntry<bool> PickupOnlyHeldItems { get; private set; }

        public static void Bind(SyncedConfiguration synced)
        {
            BindMoves(synced);
            BindSorting(synced);
            BindFavourites(synced);
            BindTrash(synced);
            FeedSourceKey = synced.Bind(Section, "Feed Source Key", KeyboardShortcut.Empty,
                "With a chest open: toggle its Feed source tag, read by Shepherd's container feeding. Personal Chests cannot be feed sources.", synced: false);
            BindClicks(synced);
            BindCycling(synced);
            BindPickup(synced);
            BindTidy(synced);
        }

        private static void BindMoves(SyncedConfiguration synced)
        {
            Enabled = synced.Bind(Section, "Enabled", true,
                "Master switch of the Stow module: panel buttons, hotkeys, sorting, trashing, routing, chest cycling and ground pickup.");
            QuickStackKey = synced.Bind(Section, "Quick Stack Key", new KeyboardShortcut(KeyCode.Q),
                "Inventory open: every non-favorite stack whose item already exists in the open container (or, with no container open, in any nearby container) moves there.", synced: false);
            QuickStackNearby = synced.Bind(Section, "Quick Stack Nearby", true,
                "Quick stack also reaches every nearby container, not only the open one. Off also disables the Dump key.");
            StoreAllKey = synced.Bind(Section, "Store All Key", RenamedKeys.Carry(synced.Config, Section, "Stow All Key", "Store All Key", new KeyboardShortcut(KeyCode.G)),
                "Inventory open with a container: every non-favorite item moves into the open container as far as it fits.", synced: false);
            TakeAllKey = synced.Bind(Section, "Take All Key", KeyboardShortcut.Empty,
                "Inventory open with a container: the game's Take all - every stack in the open container moves into your inventory as far as it fits.", synced: false);
            TopUpKey = synced.Bind(Section, "Top Up Key", RenamedKeys.Carry(synced.Config, Section, "Restock Key", "Top Up Key", KeyboardShortcut.Empty),
                "Inventory open: every stack in the inventory that is not full is topped up from the open container or nearby containers. Favorite items included.", synced: false);
            NearbyRange = synced.Bind(Section, "Nearby Range", 30f,
                "Meters from the player for quick stack, top up, dump, route, find and cycle.",
                acceptableValues: new AcceptableValueRange<float>(0f, 50f));
        }

        private static void BindSorting(SyncedConfiguration synced)
        {
            SortInventoryKey = synced.Bind(Section, "Sort Inventory Key", new KeyboardShortcut(KeyCode.T),
                "Inventory open: sorts the player inventory by Sort Order. The hotbar, favorite slots, favorite items, equipped items and the rows other mods add below the game's (Main Inventory Rows) stay where they are.", synced: false);
            SortContainerKey = synced.Bind(Section, "Sort Container Key", KeyboardShortcut.Empty,
                "Inventory open with a container: sorts the open container by Sort Order.", synced: false);
            SortOrder = synced.Bind(Section, "Sort Order", Stow.SortOrder.Category,
                "Category (item type, then name), Name, Weight (heaviest first), Value (most valuable first) or Amount (the item you have the most of first, all its stacks counted together). Stacks of the same item merge while sorting.", synced: false);
            MainInventoryRows = synced.Bind(Section, "Main Inventory Rows", 0,
                "Rows of the player inventory, hotbar included, that quick stack, store all, dump and sort work on; the hotbar itself is always left alone. 0: the rows the game gives you (4, more once bought from the trader). Mods that add equipment, food or ammo slots keep them in rows below, which these actions leave alone; top up still fills the stacks there. Set a number when a mod adds ordinary inventory rows; 20 covers every row.",
                synced: false, acceptableValues: new AcceptableValueRange<int>(0, 20));
            AutoSortContainers = synced.Bind(Section, "Auto Sort Containers", false,
                "A container is sorted when it is opened. Does nothing while Quick Stack Store Sort Trash Restock, AzuAutoStore or Simple Sort is installed: Burrow's sort row steps aside with them, and the log says so once.", synced: false);
            AutoSortInventory = synced.Bind(Section, "Auto Sort Inventory", false,
                "The player inventory is sorted when the panel opens. Does nothing while Quick Stack Store Sort Trash Restock, AzuAutoStore or Simple Sort is installed: Burrow's sort row steps aside with them, and the log says so once.", synced: false);
        }

        private static void BindFavourites(SyncedConfiguration synced)
        {
            FavouriteItemKey = synced.Bind(Section, "Favorite Item Key", KeyboardShortcut.Empty,
                "Hovering an item: the item name becomes a favorite (never sorted, quick stacked, stored, dumped, trashed or salvaged). Press again to remove.", synced: false);
            FavouriteSlotKey = synced.Bind(Section, "Favorite Slot Key", KeyboardShortcut.Empty,
                "Hovering a slot of the player inventory: the slot becomes a favorite (its content never moves, never sorted). Press again to remove.", synced: false);
            ShowFavourites = synced.Bind(Section, "Show Favorites", true,
                "Favorite items get a small star in the corner of the slot, favorite slots a colored border, junk a small cross.", synced: false);
        }


        // Changed by expDARE, 2026-10-10 (parity choice #9): OpenKeep's trash settings are back. Confirm Trash defaults on in
        // Burrow (the popup is part of the agreed check); the junk mark also excludes an item from restocking as before.
        private static void BindTrash(SyncedConfiguration synced)
        {
            JunkKey = synced.Bind(Section, "Junk Key", KeyboardShortcut.Empty,
                "Hovering an item: the item name is marked as junk (never restocked; Destroy Junk destroys every junk stack). Press again to unmark it.", synced: false);
            TrashKey = synced.Bind(Section, "Trash Key", KeyboardShortcut.Empty,
                "Hovering an item in the inventory or the open chest: the stack is trashed (after the popup when Confirm Trash is on).", synced: false);
            DestroyJunkKey = synced.Bind(Section, "Destroy Junk Key", KeyboardShortcut.Empty,
                "Destroys every junk stack in the inventory (after the popup when Confirm Trash is on).", synced: false);
            ConfirmTrash = synced.Bind(Section, "Confirm Trash", true,
                "The game's popup asks before anything is destroyed; gamepad confirm and cancel work. Off: trashing is immediate.", synced: false);
            TrashUsesSalvage = synced.Bind(Section, "Trash Uses Salvage", true,
                "Trashing a whole stack from the inventory salvages it instead when Kindle is installed and can salvage the item.", synced: false);
            TrashOnStatColumn = synced.Bind(Section, "Trash Can On Stat Column", true,
                "The trash can on its own plate in the game's style under the armor, where trash can mods put theirs. Off, or while Mantle lays out the inventory, it sits at the end of the button row. Shift+click the can for trash mode. Takes effect the next time you enter a world.", synced: false);
        }

        // Changed by expDARE, 2026-10-10 (parity choice #54): Auto Tidy from OpenKeep 3.0.0.
        private static void BindTidy(SyncedConfiguration synced)
        {
            AutoTidy = synced.Bind(Section, "Auto Tidy", false,
                "Nearby chests sort items out between themselves: an item with no place in its chest (anything in a junk chest of mixed items, or a stray among other things) moves to the nearest chest within 15 m that is its home - one that mostly holds that item or things like it, or held it until it ran out. Chests learn from what players put where by hand, and an item a player puts back after it was moved stays there. Ships, carts and Personal Chests take no part.");
        }

        private static void BindClicks(SyncedConfiguration synced)
        {
            RouteModifier = synced.Bind(Section, "Route Modifier", KeyboardShortcut.Empty,
                "Held with a left click on an inventory item: the stack is sent to the nearest nearby container that already holds the item; when none does, to the open container; else to one holding an item of its group or accepting it in Burrow.Stow.yml. What does not fit there goes on to the next nearest container holding the item.", synced: false);
            StoreOneKey = synced.Bind(Section, "Store One Key", KeyboardShortcut.Empty,
                "On a hovered inventory item: one item of the stack goes to the open container, or to the nearest container holding it; when that one is full, to the next nearest container holding it.", synced: false);
            FindKey = synced.Bind(Section, "Find Key", KeyboardShortcut.Empty,
                "Hovering an item: every nearby container holding it is marked for Link Seconds (the Reach setting) with a line from the player and a floating count.", synced: false);
            DumpKey = synced.Bind(Section, "Dump Key", KeyboardShortcut.Empty,
                "Outside the inventory: quick stack to every nearby container.", synced: false);
        }

        private static void BindCycling(SyncedConfiguration synced)
        {
            CyclePreviousKey = synced.Bind(Section, "Cycle Previous Key", KeyboardShortcut.Empty,
                "With a container open: closes it and opens the previous nearby container, ordered by angle around the player. Only containers the game would keep open (within its auto close distance) take part.", synced: false);
            CycleNextKey = synced.Bind(Section, "Cycle Next Key", KeyboardShortcut.Empty,
                "With a container open: closes it and opens the next nearby container.", synced: false);
            CycleWithWheel = synced.Bind(Section, "Cycle With Wheel", true,
                "The mouse wheel over the container grid cycles too.", synced: false);
        }

        private static void BindPickup(SyncedConfiguration synced)
        {
            GroundPickup = synced.Bind(Section, "Ground Pickup", false,
                "Containers whose prefab has pickup: true in Burrow.Stow.yml pull dropped items from the ground by rule.");
            PickupRange = synced.Bind(Section, "Pickup Range", 2f,
                "Radius in meters around a pickup container within which it collects dropped items.", acceptableValues: new AcceptableValueRange<float>(1f, 50f));
            PickupInterval = synced.Bind(Section, "Pickup Interval", 5f,
                "Seconds between sweeps of one container.", acceptableValues: new AcceptableValueRange<float>(1f, 120f));
            PickupDelay = synced.Bind(Section, "Pickup Delay", 30f,
                "Seconds an item must lie on the ground before a container takes it, measured from the item's spawn time, so players can pick up their own drops.",
                acceptableValues: new AcceptableValueRange<float>(0f, 3600f));
            PickupOnlyHeldItems = synced.Bind(Section, "Pickup Only Held Items", true,
                "A container only collects items it already holds, plus items its accept list in Burrow.Stow.yml allows. Off: it collects anything its refuse list allows.");
        }
    }
}
