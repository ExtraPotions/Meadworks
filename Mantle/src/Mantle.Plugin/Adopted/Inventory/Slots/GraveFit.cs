using HarmonyLib;
using Mantle.Backpacks;
using Mantle.Core;
using Mantle.Layout;

namespace Mantle.Slots
{
    /// <summary>
    /// Whether pressing Use on the player's own grave takes everything at once (<c>TombStone.Interact</c> takes all when
    /// <c>EasyFitInInventory</c> says yes, else opens the grave). The game counts every grave item against the free
    /// cells and the grave's weight against the carry limit of a player who just woke with no backpack, so a full pack
    /// (with Inventory Rows 0, the pack is nearly the whole inventory) never fit and Use opened the grave. Here the
    /// question is asked as the take all will run it (<see cref="GravePatches"/>): the pack that was worn goes back into
    /// its slot first and brings its slots and its carry weight; every item that lay in a slot row of the grave goes
    /// back into its own free slot (armour, utilities, food, the tacklebox and its bait); only the items of the grid
    /// need free grid cells, less those that stack onto what the player carries. Signature verified against the
    /// decompiled game: <c>private bool EasyFitInInventory(Player player)</c>. Another player's grave keeps the
    /// game's check.
    /// </summary>
    [HarmonyPatch(typeof(TombStone), nameof(TombStone.EasyFitInInventory))]
    public static class GraveFit
    {
        [HarmonyPrefix]
        public static bool Prefix(TombStone __instance, Player player, ref bool __result)
        {
            Container container = __instance.GetComponent<Container>();
            if (!InventoryState.IsLocal(player) || !InventoryState.Manages(player.GetInventory()) || container == null)
                return true;
            __result = Fits(player, container.GetInventory());
            return false;
        }

        private static bool Fits(Player player, Inventory grave)
        {
            Inventory inventory = player.GetInventory();
            InventoryLayout layout = InventoryState.Layout;
            ItemDrop.ItemData pack = BackpackGrave.WaitingPack(grave);
            BackpackKind kind = BackpackCatalog.Of(pack);
            int slots = BackpackSettings.SlotsOf(pack);
            int shift = BackpackGrave.RowsAdded(layout, slots);
            int free = MainCells.CountEmpty(inventory, layout) + slots;
            if (GridItems(inventory, grave, pack, layout.MainRows + shift, shift) > free)
                return false;
            float carry = player.GetMaxCarryWeight() + BackpackSettings.Carry(kind) * Game.m_carryWeightRate;
            return inventory.GetTotalWeight() + grave.GetTotalWeight() <= carry;
        }

        /// <summary>
        /// The grave items that need a free grid cell: not the pack, not one going back into its own free slot (a slot
        /// row of the grave is <paramref name="shift"/> rows lower than in the layout in use), and not one that stacks
        /// onto what the player already carries.
        /// </summary>
        private static int GridItems(Inventory inventory, Inventory grave, ItemDrop.ItemData pack, int mainRows, int shift)
        {
            int need = 0;
            foreach (ItemDrop.ItemData item in grave.GetAllItems())
            {
                if (item == pack)
                    continue;
                Vector2i at = item.m_gridPos;
                if (at.y >= mainRows && inventory.GetItemAt(at.x, at.y - shift) == null)
                    continue;
                if (inventory.FindFreeStackSpace(item.m_shared.m_name, item.m_worldLevel) >= item.m_stack)
                    continue;
                need++;
            }
            return need;
        }
    }
}
