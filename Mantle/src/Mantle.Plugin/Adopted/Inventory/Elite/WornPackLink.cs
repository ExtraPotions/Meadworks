using System.Collections.Generic;
using ExtraPotions.Mantle.Embedded.EliteCraftingLink;
using Mantle.Backpacks;
using Mantle.Core;
using Mantle.Worn;

namespace Mantle.Elite
{
    /// <summary>
    /// The worn backpack and the extra worn utilities as EliteCrafting equipment. EliteCrafting counts the game's
    /// equipment fields only, and a pack is worn in Mantle's Backpack slot, never as the game's utility, while the
    /// utilities beyond the game's one are Mantle's (<see cref="ExtraUtilities"/>), so an equipment provider names them:
    /// the pack in the local player's Backpack slot (<see cref="Backpack.WornItem"/>) and each extra utility still worn
    /// (since 2026-10-05: before, an inscription on the second or third utility did nothing); none for any other player,
    /// whose totals EliteCrafting does not compute here. Their inscriptions then count like worn gear (Broad Back's carry
    /// weight, Pack Mule, Magpie, Harvester). An extra utility worn or taken off runs the game's SetupEquipment, which
    /// makes EliteCrafting rebuild. EliteCrafting is told to rebuild the player's totals when the worn pack changes (checked each frame by
    /// <see cref="BackpackWear"/>) and when a write changes the worn pack's inscriptions (its item-changed listener).
    /// The provider answers from one reused list: EliteCrafting reads it at once and keeps nothing.
    /// </summary>
    public static class WornPackLink
    {
        private static readonly List<ItemDrop.ItemData> answer = new List<ItemDrop.ItemData>(5);
        private static ItemDrop.ItemData lastWorn;

        internal static void Register(List<string> refused)
        {
            EliteSetup.Expect(CraftingHooks.RegisterEquipmentProvider(EliteDefinitions.ProviderId, Provide), "the worn pack as equipment", refused);
            EliteSetup.Expect(CraftingHooks.AddItemChangedListener(OnItemChanged), "the item-changed listener", refused);
        }

        /// <summary>Mantle: the provider and the listener back out of Inscribe when the plugin goes.</summary>
        internal static void Unregister()
        {
            if (!CraftingLink.Present)
                return;
            CraftingHooks.UnregisterEquipmentProvider(EliteDefinitions.ProviderId);
            CraftingHooks.RemoveItemChangedListener(OnItemChanged);
            lastWorn = null;
        }

        /// <summary>The local player's frame, with the pack it wears now (null for none).</summary>
        public static void Follow(Player player, ItemDrop.ItemData worn)
        {
            if (worn == lastWorn)
                return;
            lastWorn = worn;
            CraftingHooks.InvalidatePlayer(player);
        }

        private static List<ItemDrop.ItemData> Provide(Player player)
        {
            answer.Clear();
            ItemDrop.ItemData pack = Backpack.WornItem(player);
            if (pack != null)
                answer.Add(pack);
            if (!InventoryState.IsLocal(player))
                return answer;
            foreach (ItemDrop.ItemData utility in ExtraUtilities.Worn)
                if (utility.m_equipped && utility != pack)
                    answer.Add(utility);
            return answer;
        }

        private static void OnItemChanged(ItemDrop.ItemData item, string reason)
        {
            if (item != null && item == lastWorn && Player.m_localPlayer != null)
                CraftingHooks.InvalidatePlayer(Player.m_localPlayer);
        }
    }
}
