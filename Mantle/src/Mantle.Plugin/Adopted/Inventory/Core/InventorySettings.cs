using BepInEx.Configuration;
using Mantle.Backpacks;
using Mantle.Ring;
using Mantle.Slots;
using ExtraPotions.Mantle.Embedded.SyncedConfig;

namespace Mantle.Core
{
    /// <summary>
    /// Sections "1. Inventory" (the master switch, the grid, carry weight, death), "2. Slots" (every slot group) and
    /// "5. Look" (each player's own display keys); the key ring binds "3. Key Ring" (<see cref="KeyRingSettings"/>), the
    /// backpacks "4. Backpacks" (<see cref="BackpackSettings"/>). The grid's size, every slot count and the carry weight
    /// change what a player can carry, so they are synced and lockable; the labels and the look are each player's own.
    /// Values are read at use time; a change of the layout keys re-lays the inventory out at once (<see cref="InventoryModule"/>).
    /// </summary>
    public static class InventorySettings
    {
        public const string Section = "1. Inventory";
        public const string SlotsSection = "2. Slots";
        public const string LookSection = "5. Look";
        public const int GameWidth = 8;
        public const int GameRows = 4;

        /// <summary>The main cells left with Inventory Rows at 0: the two hands, hotbar keys 1 and 2.</summary>
        public const int HandCells = 2;
        public const float GameCarryWeight = 300f;
        public const int MaxWidth = 12;
        public const int MaxRows = 10;
        public const int MaxGroup = 5;

        /// <summary>Ring cells, at most.</summary>
        public const int MaxRings = 2;

        public static ConfigEntry<bool> Enabled { get; private set; }
        public static ConfigEntry<int> InventoryWidth { get; private set; }
        public static ConfigEntry<int> InventoryRows { get; private set; }
        public static ConfigEntry<bool> EquipmentSlots { get; private set; }
        public static ConfigEntry<bool> InstantEquip { get; private set; }
        public static ConfigEntry<bool> AutoEquip { get; private set; }
        public static ConfigEntry<int> SlotsPerGroup { get; private set; }
        public static ConfigEntry<int> UtilitySlots { get; private set; }
        public static ConfigEntry<bool> TrinketSlot { get; private set; }
        public static ConfigEntry<bool> BackpackSlot { get; private set; }
        public static ConfigEntry<int> RingSlots { get; private set; }
        public static ConfigEntry<bool> BeltSlot { get; private set; }
        public static ConfigEntry<string> BackpackItems { get; private set; }
        public static ConfigEntry<int> FoodSlots { get; private set; }
        public static ConfigEntry<bool> FoodSlotsFollowEating { get; private set; }
        public static ConfigEntry<int> MeadSlots { get; private set; }
        public static ConfigEntry<int> AmmoSlots { get; private set; }
        public static ConfigEntry<bool> CoinPurse { get; private set; }
        public static ConfigEntry<KeptGroups> KeepOnDeath { get; private set; }
        public static ConfigEntry<float> BaseCarryWeight { get; private set; }
        public static ConfigEntry<bool> SlotLabels { get; private set; }
        public static ConfigEntry<bool> SlotIcons { get; private set; }
        public static ConfigEntry<bool> BrownStyle { get; private set; }
        public static ConfigEntry<bool> WeightUnderMinimap { get; private set; }
        public static ConfigEntry<float> CraftingWidth { get; private set; }
        public static ConfigEntry<float> CraftingHeight { get; private set; }

        public static void Bind(SyncedConfiguration synced)
        {
            BindGrid(synced);
            BindWorn(synced);
            BackpackSettings.Bind(synced);
            BindGroups(synced);
            BindCarried(synced);
            Consume.ConsumeSettings.Bind(synced);
            KeyRingSettings.Bind(synced);
            Tackle.TackleboxSettings.Bind(synced);
            BindDisplay(synced);
        }

        private static void BindGrid(SyncedConfiguration synced)
        {
            Enabled = synced.Bind(Section, "Enabled", true,
                "Master switch for Mantle: the bigger grid, the labeled slots, the coin purse, the key ring, the backpacks and the brown look. Off: the game's own inventory, its 8 x 4 grid (plus rows bought from the trader) and its wood look; every other Mantle setting is ignored. Items in the slots move into the grid, what does not fit is dropped at your feet.");
            InventoryWidth = synced.Bind(Section, "Inventory Width", 8,
                "Columns of the inventory grid. The game has 8; the hotbar keys stay 1 to 8. Warning: items in columns beyond 8 are lost if Mantle is removed, so empty them first.",
                acceptableValues: new AcceptableValueRange<int>(GameWidth, MaxWidth));
            InventoryRows = synced.Bind(Section, "Inventory Rows", 4,
                "Rows of the inventory grid. The game has 4; rows bought from the trader come on top of this. 0 leaves only two cells, your hands (hotbar keys 1 and 2); a worn backpack's cells continue after them. Lowering it drops what no longer fits at your feet. Items in rows beyond the game's are dropped at your feet if Mantle is removed.",
                acceptableValues: new AcceptableValueRange<int>(0, MaxRows));
            BaseCarryWeight = synced.Bind(Section, "Base Carry Weight", GameCarryWeight,
                "How much a player carries before being over-encumbered, before Megingjord and other effects add to it. The game has 300. The world's carry weight modifier still scales it.",
                acceptableValues: new AcceptableValueRange<float>(50f, 10000f));
            KeepOnDeath = synced.Bind(Section, "Keep On Death", KeptGroups.None,
                "The slot groups whose items stay with you when you die instead of going into your grave, comma separated (e.g. Food, Ammo): Gear (head, chest, legs, back; feet too while laid out), Backpack, Utility, Trinket, Ring, Belt, Food, Mead, Ammo, Tacklebox. What you wore of them is worn again when you wake. A backpack's or tacklebox's own cells, the coin purse, the key ring and the grid follow the game's rules. None: everything goes to the grave. Replaces Keep Slots On Death: an old setting that was on is carried over once, as every group.");
        }

        private static void BindWorn(SyncedConfiguration synced)
        {
            EquipmentSlots = synced.Bind(SlotsSection, "Equipment Slots", true,
                "Head, Chest, Legs and Back slots, and a Feet slot for EliteEquipment's boots while its Separate Boots is on. Armor you wear sits in its slot; drop a piece on its slot to wear it, drag it out to take it off.");
            AutoEquip = synced.Bind(SlotsSection, "Auto Equip", true,
                "Gear in the Gear tab is worn. Right click armor, a cape, a utility or a trinket in an open chest to wear it (the piece in its slot moves to a free cell of your inventory first; with none, nothing changes); a piece that lands in its slot any other way is put on too. A worn piece taken off moves into a free cell of your inventory; with none it comes off in its slot and stays off until you put it on again; it is never dropped. A piece that breaks while worn stays in its slot and goes back on once repaired. Off: a piece that lands in its slot unworn stays unworn, and a right click in a chest does nothing.");
            InstantEquip = synced.Bind(SlotsSection, "Instant Equip", false,
                "Weapons, tools, shields and armor go on and come off at once, with no equipping bar. Off: the game's equip time.");
            UtilitySlots = synced.Bind(SlotsSection, "Utility Slots", 5,
                "Utility slots (belts, the wishbone, the wisplight...) when Slots Per Group is 0. Every utility item in a slot is worn at once, so up to this many work together. 0: none, the game's single utility item.",
                acceptableValues: new AcceptableValueRange<int>(0, MaxGroup));
            TrinketSlot = synced.Bind(SlotsSection, "Trinket Slot", true,
                "A Trinket slot under the utilities. The trinket in it is the one you wear (the game wears one at a time); drop a trinket on it to wear it, drag it out to take it off.");
            BackpackSlot = synced.Bind(SlotsSection, "Backpack Slot", true,
                "A Backpack slot. Mantle's backpacks are worn in it (see 4. Backpacks); it also holds a backpack from another mod (any item whose prefab name contains \"backpack\", or one listed in Backpack Items), but does not equip those.");
            RingSlots = synced.Bind(SlotsSection, "Ring Slots", 0,
                "Ring slots in the Gear tab, 0 to 2. A ring slot takes only a ring (an item the accessory registry, Mantle.Accessories.yml, tags as one); the rings in the slots give their bonuses together, and two identical rings give double. 0: no ring slot.",
                acceptableValues: new AcceptableValueRange<int>(0, MaxRings));
            BeltSlot = synced.Bind(SlotsSection, "Belt Slot", false,
                "A Belt slot in the Gear tab, taking only a belt (an item Mantle.Accessories.yml tags as one: Mantle's belts and the Megingjord). The belt in it gives its bonus. Off: no slot, and the Megingjord is a utility item as in the game.");
            BackpackItems = synced.Bind(SlotsSection, "Backpack Items", "",
                "More prefab names the Backpack slot accepts, comma separated.");
        }

        private static void BindGroups(SyncedConfiguration synced)
        {
            SlotsPerGroup = synced.Bind(SlotsSection, "Slots Per Group", 0,
                "Older setting kept for existing profiles: 1 to 5 sets the number of Utility slots; 0 uses Utility Slots instead. Food, Mead and Ammo have their own settings.",
                acceptableValues: new AcceptableValueRange<int>(0, MaxGroup));
        }

        private static void BindCarried(SyncedConfiguration synced)
        {
            FoodSlots = synced.Bind(SlotsSection, "Food Slots", 3, "Food slots (0 to 5) when Food Slots Follow Eating is off. 0: no Food slots, no Food row and no food square on the bar.",
                acceptableValues: new AcceptableValueRange<int>(0, MaxGroup));
            FoodSlotsFollowEating = synced.Bind(SlotsSection, "Food Slots Follow Eating", false,
                "As many food slots as foods a player can eat at once: the Food Slots setting of the FeastMaster mod when that mod is installed, otherwise the game's 3 (at most 5). Slots Per Group does not change food slots.");
            MeadSlots = synced.Bind(SlotsSection, "Mead Slots", 3, "Slots for meads and other drinks and potions (0 to 5). 0: no Mead slots, no Mead row and no mead squares on the bar.",
                acceptableValues: new AcceptableValueRange<int>(0, MaxGroup));
            AmmoSlots = synced.Bind(SlotsSection, "Ammo Slots", 3, "Slots for arrows, bolts, missiles and bait (0 to 5). 0: no Ammo slots and no Ammo row.",
                acceptableValues: new AcceptableValueRange<int>(0, MaxGroup));
            CoinPurse = synced.Bind(SlotsSection, "Coin Purse", true,
                "A purse slot on the Gear tab: coins you pick up go into it, traders take coins from it as from anywhere in the inventory.");
        }

        private static void BindDisplay(SyncedConfiguration synced)
        {
            SlotLabels = synced.Bind(LookSection, "Slot Labels", true, "The name of each slot on the slot while it is empty.", synced: false);
            SlotIcons = synced.Bind(LookSection, "Slot Icons", true,
                "A drawing of what belongs in each slot on the slot while it is empty, centered when Slot Labels is off.", synced: false);
            BrownStyle = synced.Bind(LookSection, "Panel Look", true,
                "The Meadworks wood look of the inventory panels, their cells, the equipment panel and the crafting panel (art and Timber settings are in the Crucible.UI config, section Look). Off: the game's own wood.", synced: false);
            WeightUnderMinimap = synced.Bind(LookSection, "Weight Under Minimap", true,
                "Armor and carry weight in boxes in a column right of the minimap, followed by Elite Creatures Reborn's world tier when it is installed. The minimap and the status effects move a little left to make room.", synced: false);
            CraftingWidth = synced.Bind(LookSection, "Crafting Panel Width", 100f,
                "Extra width for the crafting panel and the panel above it (your name, skills, trophies, PvP), in interface units (the game's panels are 570): a wider recipe list and description. They grow to the left, never into the inventory's panels (so on a narrow screen they get what fits). 0: the game's width. Default 100, as in PackPanel.",
                acceptableValues: new AcceptableValueRange<float>(0f, 600f), synced: false);
            CraftingHeight = synced.Bind(LookSection, "Crafting Panel Height", 90f,
                "Extra height for the crafting panel, in interface units (the game's panel is 650): a longer recipe list and more room for the description. It grows downward, never past the bottom of the screen. 0: the game's height.",
                acceptableValues: new AcceptableValueRange<float>(0f, 400f), synced: false);
        }

    }
}
