using System.Collections.Generic;
using Mantle.Core;
using Mantle.Slots;

namespace Mantle.Consume
{
    /// <summary>
    /// Eating or drinking everything a slot group holds that can be taken now, left to right, through the game's own use of
    /// an item (<c>Humanoid.UseItem</c>, as a hotbar key does: the eat animation, the sound, the status effect, the food,
    /// one item gone). <c>fromInventoryGui</c> is true so a creature or station the player looks at is never fed instead.
    /// Each item is checked first without messages, with the game's own rules (<see cref="CanTakeNow"/>), so what cannot be
    /// taken is skipped silently; each check sees the foods and effects the ones before it gave.
    /// </summary>
    public static class SlotMeals
    {
        /// <summary>How many items were eaten or drunk from the slots of this kind.</summary>
        public static int TakeAll(Player player, SlotKind kind)
        {
            if (!InventoryState.Active || !InventoryState.IsLocal(player) || (kind != SlotKind.Food && kind != SlotKind.Mead))
                return 0;
            Inventory inventory = player.GetInventory();
            int taken = 0;
            foreach (Vector2i cell in new List<Vector2i>(InventoryState.CellsOf(kind)))
            {
                ItemDrop.ItemData item = inventory.GetItemAt(cell.x, cell.y);
                if (item == null || !CanTakeNow(player, item))
                    continue;
                player.UseItem(inventory, item, true);
                taken++;
            }
            return taken;
        }

        /// <summary>
        /// The game's <c>Player.CanConsumeItem(item, checkWorldLevel: true)</c> without its messages: a consumable, not below
        /// the world's level, a food the player can eat now (<c>CanEat</c>: a free food place, or the same food about to run
        /// out), and no effect of the same kind or category already running. The Food and Mead bar dims what fails it
        /// (<see cref="ConsumeBar"/>).
        /// </summary>
        public static bool CanTakeNow(Player player, ItemDrop.ItemData item)
        {
            if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable)
                return false;
            if (Game.m_worldLevel > 0 && item.m_worldLevel < Game.m_worldLevel)
                return false;
            if (item.m_shared.m_food > 0f && !player.CanEat(item, false))
                return false;
            StatusEffect effect = item.m_shared.m_consumeStatusEffect;
            if (effect == null)
                return true;
            SEMan effects = player.GetSEMan();
            return !effects.HaveStatusEffect(effect.NameHash()) && !effects.HaveStatusEffectCategory(effect.m_category);
        }
    }
}
