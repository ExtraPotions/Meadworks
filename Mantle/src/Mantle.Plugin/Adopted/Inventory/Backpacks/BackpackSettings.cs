using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using Mantle.Core;
using ExtraPotions.Mantle.Embedded.SyncedConfig;

namespace Mantle.Backpacks
{
    /// <summary>
    /// Section "4. Backpacks": the switch for all of them and the portal pass. What each pack
    /// gives and costs is in Mantle.Backpacks.yml (<see cref="BackpacksFile"/>). Those two keys change play, so they are
    /// synced and lockable; Show Worn Backpack (the user's request, 2026-09-28: some players want the pack invisible) is
    /// each player's own look and not synced. Read at use time: the recipes follow a change at once
    /// (<see cref="Crafting.CraftRecipes"/>), the worn pack's slots and its visibility on the next frame (<see cref="BackpackWear"/>).
    /// </summary>
    public static class BackpackSettings
    {
        public const string Section = "4. Backpacks";

        public static ConfigEntry<bool> Enabled { get; private set; }
        public static ConfigEntry<bool> PortalPass { get; private set; }

        /// <summary>Whether the local player's worn pack is drawn on their back; each player's own, never synced.</summary>
        public static ConfigEntry<bool> ShowWorn { get; private set; }

        /// <summary>Backpacks work: their switch, the master switch and the Backpack slot are all on.</summary>
        public static bool Active => Enabled.Value && InventorySettings.Enabled.Value && InventorySettings.BackpackSlot.Value;

        public static int Slots(BackpackKind kind) => Active && kind != null ? kind.Stats.Slots : 0;

        /// <summary>
        /// The slots one pack gives: its kind's, plus what its EliteCrafting Deep Pockets adds (<see cref="Elite.DeepPockets"/>).
        /// Every place that sizes the grid from a pack item asks this, so they all agree.
        /// </summary>
        public static int SlotsOf(ItemDrop.ItemData pack)
        {
            BackpackKind kind = BackpackCatalog.Of(pack);
            return Active && kind != null ? kind.Stats.Slots + Elite.DeepPockets.Extra(pack) : 0;
        }

        public static float Carry(BackpackKind kind) => Active && kind != null ? kind.Stats.Carry : 0f;

        public static bool Portal(BackpackKind kind) => Active && PortalPass.Value && kind != null && kind.Stats.Portal;

        public static void Bind(SyncedConfiguration synced)
        {
            Enabled = synced.Bind(Section, "Backpacks", true,
                "Mantle's backpacks, one per biome from the Deerhide Satchel to the Moosehide Pack (their slots, carry weight, stations and costs are in Mantle.Backpacks.yml). Put one in the Backpack slot, by dropping it there or right clicking it, to wear it: it hangs on your back and adds its slots to the bottom of the grid and its carry weight. Taking it off moves what its slots hold into free cells and drops what does not fit at your feet. Off: none can be crafted and a worn one adds nothing. Needs 2. Slots / Backpack Slot.");
            PortalPass = synced.Bind(Section, "Backpack Portal Pass", false,
                "Items in the slots of a backpack marked portal: true in Mantle.Backpacks.yml (the Moosehide Pack) go through portals even when the game forbids them (ores, metals, eggs...). Everything else in the inventory still follows the game's rule.", tags: new object[] { new ConfigDependency(() => Active, () => "Requires Inventory: Enabled, Backpack Slot and Backpacks.") });
            ShowWorn = synced.Bind(Section, "Show Worn Backpack", true,
                "Your worn backpack hangs on your back where every player sees it. Off: it is invisible, to you and to everyone else, and still gives its slots and carry weight. Your own choice, not synced from the server.", synced: false);
        }
    }
}
