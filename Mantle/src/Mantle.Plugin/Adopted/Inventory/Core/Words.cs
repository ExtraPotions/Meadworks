using Mantle.Backpacks;
using Mantle.Ring;
using Mantle.Slots;

namespace Mantle.Core
{
    /// <summary>The $mantle_ words: the slot captions, the slot panel's tabs, the key ring's words and the messages.</summary>
    public static class Words
    {
        public static string Head { get; private set; }
        public static string Chest { get; private set; }
        public static string Legs { get; private set; }
        public static string Feet { get; private set; }
        public static string Back { get; private set; }
        public static string Backpack { get; private set; }
        public static string Utility { get; private set; }
        public static string Trinket { get; private set; }
        public static string Ring { get; private set; }
        public static string Belt { get; private set; }
        public static string Food { get; private set; }
        public static string Mead { get; private set; }
        public static string Ammo { get; private set; }
        public static string Coins { get; private set; }
        public static string Keys { get; private set; }
        public static string KeyRing { get; private set; }
        public static string NoKeysHeld { get; private set; }
        public static string NotAKey { get; private set; }
        public static string KeyNew { get; private set; }
        public static string KeysNew { get; private set; }
        public static string WrongSlot { get; private set; }
        public static string Dropped { get; private set; }
        public static string NoRoomOff { get; private set; }
        public static string GearTab { get; private set; }
        public static string ConsumablesTab { get; private set; }
        public static string StatResistances { get; private set; }
        public static string StatGear { get; private set; }
        public static string StatEpicLoot { get; private set; }
        public static string StatEliteCrafting { get; private set; }
        public static string StatOffence { get; private set; }
        public static string StatDefence { get; private set; }
        public static string StatResources { get; private set; }
        public static string StatMovement { get; private set; }
        public static string StatSkills { get; private set; }
        public static string StatOther { get; private set; }

        public static void Register()
        {
            Head = Language.Add("mantle_head", "Head");
            Chest = Language.Add("mantle_chest", "Chest");
            Legs = Language.Add("mantle_legs", "Legs");
            Feet = Language.Add("mantle_feet", "Feet");
            Back = Language.Add("mantle_back", "Back");
            Backpack = Language.Add("mantle_backpack", "Backpack");
            Utility = Language.Add("mantle_utility", "Utility");
            Trinket = Language.Add("mantle_trinket", "Trinket");
            Ring = Language.Add("mantle_ring", "Ring");
            Belt = Language.Add("mantle_belt", "Belt");
            Food = Language.Add("mantle_food", "Food");
            Mead = Language.Add("mantle_mead", "Mead");
            Ammo = Language.Add("mantle_ammo", "Ammo");
            Coins = Language.Add("mantle_coins", "Coins");
            Keys = Language.Add("mantle_keys", "Keys");
            KeyRing = Language.Add("mantle_keyring", "Key ring");
            NoKeysHeld = Language.Add("mantle_nokeysheld", "You carry no keys");
            NotAKey = Language.Add("mantle_notakey", "Only keys go on the key ring");
            KeyNew = Language.Add("mantle_keynew", "{0} went onto your key ring");
            KeysNew = Language.Add("mantle_keysnew", "{0} new keys went onto your key ring");
            WrongSlot = Language.Add("mantle_wrongslot", "That does not go in that slot");
            Dropped = Language.Add("mantle_dropped", "No room for {0} items: dropped at your feet");
            NoRoomOff = Language.Add("mantle_noroomoff", "No room in your inventory to take that off");
            GearTab = Language.Add("mantle_tab_gear", "Gear");
            ConsumablesTab = Language.Add("mantle_tab_consumables", "Consumables");
            RegisterStats();
        }

        /// <summary>The stat sheet's headings.</summary>
        private static void RegisterStats()
        {
            StatResistances = Language.Add("mantle_stat_resistances", "Resistances");
            StatGear = Language.Add("mantle_stat_gear", "Gear bonuses");
            StatEpicLoot = Language.Add("mantle_stat_epicloot", "Epic Loot");
            StatEliteCrafting = Language.Add("mantle_stat_elitecrafting", "Inscribe");
            StatOffence = Language.Add("mantle_stat_offense", "Offense");
            StatDefence = Language.Add("mantle_stat_defense", "Defense");
            StatResources = Language.Add("mantle_stat_resources", "Health and stamina");
            StatMovement = Language.Add("mantle_stat_movement", "Movement");
            StatSkills = Language.Add("mantle_stat_skills", "Skills");
            StatOther = Language.Add("mantle_stat_other", "Other");
        }

        /// <summary>The caption of a slot kind.</summary>
        public static string Caption(SlotKind kind)
        {
            switch (kind)
            {
                case SlotKind.Head: return Head;
                case SlotKind.Chest: return Chest;
                case SlotKind.Legs: return Legs;
                case SlotKind.Feet: return Feet;
                case SlotKind.Back: return Back;
                case SlotKind.Backpack: return Backpack;
                case SlotKind.Utility: return Utility;
                case SlotKind.Trinket: return Trinket;
                case SlotKind.Ring: return Ring;
                case SlotKind.Belt: return Belt;
                case SlotKind.Food: return Food;
                case SlotKind.Mead: return Mead;
                case SlotKind.Ammo: return Ammo;
                case SlotKind.Key: return Keys;
                case SlotKind.Tacklebox: return Tackle.TackleboxWords.Tacklebox;
                case SlotKind.Tackle: return Tackle.TackleboxWords.Bait;
                default: return Coins;
            }
        }
    }
}
