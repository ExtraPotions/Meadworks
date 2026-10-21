using BepInEx.Configuration;
using Mantle.Core;
using ExtraPotions.Mantle.Embedded.SyncedConfig;

namespace Mantle.Ring
{
    /// <summary>
    /// Section "3. Key Ring": its switch, the keys it holds and how many of one key stack. All three change play, so
    /// they are synced and lockable. Key Ring and Key Items lay the inventory out again (<see cref="InventoryModule"/>);
    /// Key Stack is written into the keys' stack size (<see cref="KeyStacks"/>).
    /// </summary>
    public static class KeyRingSettings
    {
        /// <summary>The game's six keys, in the order of the biomes they come from.</summary>
        public const string GameKeys = "HildirKey_forestcrypt,CryptKey,HildirKey_mountaincave,HildirKey_plainsfortress,DvergrKey,BloodGoldKey";
        public const string Section = "3. Key Ring";
        public const int MaxStack = 100;

        public static ConfigEntry<bool> KeyRing { get; private set; }
        public static ConfigEntry<string> KeyItems { get; private set; }
        public static ConfigEntry<int> KeyStack { get; private set; }

        public static void Bind(SyncedConfiguration synced)
        {
            KeyRing = synced.Bind(Section, "Key Ring", true,
                "A key ring button right of the coin purse. Click it for a round pop-up with a cell for every key in Key Items you have found: keys you pick up or take from a chest, grave or trader go there first, and doors still find them. Off: keys go to the grid; keys on the ring move to the grid, what does not fit is dropped at your feet.");
            KeyItems = synced.Bind(Section, "Key Items", GameKeys,
                "The keys the ring holds, as prefab names, comma separated, one ring cell each in this order. The default is the game's six keys, from the Black Forest to the Ashlands.");
            KeyStack = synced.Bind(Section, "Key Stack", 10,
                "How many of one key stack in one cell, on the ring and everywhere else (chests too), for every key in Key Items. The game has 1; keys of different world levels never stack. With OpenKeep installed, an OpenKeep.Stacks.yml entry for a key wins. Applies even with Key Ring or Mantle's master switch off, so turning those off never cuts a stack of keys. Warning: lowering it, or removing Mantle, cuts bigger stacks of keys when they load, and the rest is lost.",
                acceptableValues: new AcceptableValueRange<int>(1, MaxStack));
        }
    }
}
