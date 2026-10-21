using System;
using System.Collections.Generic;

namespace ExtraPotions.Mantle.Embedded.EliteCraftingLink
{
    /// <summary>
    /// Hooks into EliteCrafting (api.md section 5). Every callback runs guarded on EliteCrafting's side: an exception is
    /// logged once and counts as no answer. Delegates are passed through unchanged. Without EliteCrafting every call
    /// answers false and nothing is ever called back.
    /// </summary>
    public static class CraftingHooks
    {
        private static readonly Endpoint<Func<string, Func<Player, List<ItemDrop.ItemData>>, bool>> registerProvider =
            new Endpoint<Func<string, Func<Player, List<ItemDrop.ItemData>>, bool>>("RegisterEquipmentProvider");
        private static readonly Endpoint<Func<string, bool>> unregisterProvider = new Endpoint<Func<string, bool>>("UnregisterEquipmentProvider");
        private static readonly Endpoint<Action<Player>> invalidatePlayer = new Endpoint<Action<Player>>("InvalidatePlayer");
        private static readonly Endpoint<Func<string, Func<ItemDrop.ItemData, bool>, bool>> registerFilter =
            new Endpoint<Func<string, Func<ItemDrop.ItemData, bool>, bool>>("RegisterMagicBaseFilter");
        private static readonly Endpoint<Func<string, bool>> unregisterFilter = new Endpoint<Func<string, bool>>("UnregisterMagicBaseFilter");
        private static readonly Endpoint<Func<Action<ItemDrop.ItemData, string>, bool>> addChanged =
            new Endpoint<Func<Action<ItemDrop.ItemData, string>, bool>>("AddItemChangedListener");
        private static readonly Endpoint<Func<Action<ItemDrop.ItemData, string>, bool>> removeChanged =
            new Endpoint<Func<Action<ItemDrop.ItemData, string>, bool>>("RemoveItemChangedListener");
        private static readonly Endpoint<Func<Action<ItemDrop.ItemData>, bool>> addLoot =
            new Endpoint<Func<Action<ItemDrop.ItemData>, bool>>("AddLootGeneratedListener");
        private static readonly Endpoint<Func<Action<ItemDrop.ItemData>, bool>> removeLoot =
            new Endpoint<Func<Action<ItemDrop.ItemData>, bool>>("RemoveLootGeneratedListener");
        private static readonly Endpoint<Func<string, bool>> setCreatureLoot = new Endpoint<Func<string, bool>>("SetCreatureLoot");

        /// <summary>Items a player wears outside the game's slots; their inscriptions count like equipped items.</summary>
        public static bool RegisterEquipmentProvider(string id, Func<Player, List<ItemDrop.ItemData>> extraEquipped) =>
            Safe.Call(registerProvider.Call, id, extraEquipped, false);

        public static bool UnregisterEquipmentProvider(string id) => Safe.Call(unregisterProvider.Call, id, false);

        /// <summary>Rebuild the player's totals (the local player, on the next frame): call after a provider's answer changed.</summary>
        public static void InvalidatePlayer(Player player)
        {
            Action<Player>? call = invalidatePlayer.Call;
            try
            {
                call?.Invoke(player);
            }
            catch (Exception e)
            {
                ApiBinding.Failed(e);
            }
        }

        /// <summary>A veto: false keeps an item type from ever becoming magic (drops, runes, the API).</summary>
        public static bool RegisterMagicBaseFilter(string id, Func<ItemDrop.ItemData, bool> allow) => Safe.Call(registerFilter.Call, id, allow, false);

        public static bool UnregisterMagicBaseFilter(string id) => Safe.Call(unregisterFilter.Call, id, false);

        /// <summary>After every write of EliteCrafting's item state: the item and why (<c>rune:&lt;id&gt;</c>, <c>drop</c>, <c>command</c>, <c>api</c>, <c>migration</c>).</summary>
        public static bool AddItemChangedListener(Action<ItemDrop.ItemData, string> listener) => Safe.Call(addChanged.Call, listener, false);

        public static bool RemoveItemChangedListener(Action<ItemDrop.ItemData, string> listener) => Safe.Call(removeChanged.Call, listener, false);

        /// <summary>After a pre-rolled magic item is made for a drop.</summary>
        public static bool AddLootGeneratedListener(Action<ItemDrop.ItemData> listener) => Safe.Call(addLoot.Call, listener, false);

        public static bool RemoveLootGeneratedListener(Action<ItemDrop.ItemData> listener) => Safe.Call(removeLoot.Call, listener, false);

        /// <summary>
        /// A modded creature's loot profile: <c>{ "prefab", "boss", "tier", "multiplier", "rune_multiplier",
        /// "gear_multiplier", "rune_rolls", "gear_rolls", "bonus" }</c> (the economy YAML's <c>drops.creatures</c> and
        /// <c>drops.bosses</c> fields).
        /// </summary>
        public static bool SetCreatureLoot(string json) => Safe.Call(setCreatureLoot.Call, json, false);
    }
}
