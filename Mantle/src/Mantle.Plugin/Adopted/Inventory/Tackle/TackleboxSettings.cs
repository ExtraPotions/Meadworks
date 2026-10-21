using BepInEx.Configuration;
using Mantle.Core;
using ExtraPotions.Mantle.Embedded.SyncedConfig;

namespace Mantle.Tackle
{
    /// <summary>
    /// Section "6. Tacklebox": the switch for the slot and the boxes, and what a box takes besides bait. What each box
    /// gives and costs is in Mantle.Tackleboxes.yml (<see cref="TackleboxesFile"/>). Both keys change play, so they are
    /// synced and lockable, and both lay the inventory out again (<see cref="InventoryModule"/>).
    /// </summary>
    public static class TackleboxSettings
    {
        public const string Section = "6. Tacklebox";

        public static ConfigEntry<bool> Enabled { get; private set; }
        public static ConfigEntry<string> TackleItems { get; private set; }

        /// <summary>The Tacklebox slot is part of the layout the settings ask for: its switch and the master switch on.</summary>
        public static bool Active => Enabled.Value && InventorySettings.Enabled.Value;

        public static int Cells(TackleboxKind kind) => Active && kind != null ? kind.Stats.Cells : 0;

        public static void Bind(SyncedConfiguration synced)
        {
            Enabled = synced.Bind(Section, "Tacklebox", true,
                "A Tacklebox slot right of the key ring button, and Mantle's five tackleboxes, from the Driftwood Tacklebox to the Timberwood Tacklebox (their cells, stations and costs are in Mantle.Tackleboxes.yml). Put a box in the slot, by dropping it there or right clicking it, then click it there to open it (Shift click picks it up to move it): a pop-up under the slot panel with its cells. Bait you pick up or take from a chest goes into it first, the fishing rod takes its bait from it first, and right clicking a bait in it fishes with that bait. Taking the box out moves what its cells hold into free cells and drops what does not fit at your feet. Off: no slot, none can be crafted.");
            TackleItems = synced.Bind(Section, "Tackle Items", "",
                "Items a tacklebox takes besides fishing bait, as prefab names, comma separated (chum, another mod's lures). Empty: bait only.");
        }
    }
}
