using System;

namespace ExtraPotions.Mantle.Embedded.EliteCraftingLink
{
    /// <summary>
    /// Inscriptions, pools, external effects and totals (api.md section 3). An inscription is a JSON object with the
    /// inscription YAML's format 2 fields (<c>id</c>, <c>name</c>, <c>effect</c>, <c>param</c>, <c>value</c>,
    /// <c>affix</c>, <c>family</c>, <c>category</c>, <c>classes</c>, <c>tiers</c>, ...), complete on its own; its effect
    /// must be registered first. An external effect (<c>id</c>, <c>scope</c> player/item, <c>value</c>
    /// percent/flat/flag, <c>polarity</c> raise/lower, <c>cap</c>, <c>param</c>) is rolled, shown and summed by
    /// EliteCrafting and applied by the mod that registered it, which reads <see cref="GetPlayerTotal"/> or
    /// <see cref="GetItemTotal"/>. Totals are in the inscriptions' units (percent points or flat numbers). Without
    /// EliteCrafting every call answers false or 0.
    /// </summary>
    public static class CraftingInscriptions
    {
        private static readonly Endpoint<Func<string, bool>> registerInscription = new Endpoint<Func<string, bool>>("RegisterInscription");
        private static readonly Endpoint<Func<string, string, bool, bool>> addToPool = new Endpoint<Func<string, string, bool, bool>>("AddToPool");
        private static readonly Endpoint<Func<string, bool>> registerExternalEffect = new Endpoint<Func<string, bool>>("RegisterExternalEffect");
        private static readonly Endpoint<Func<Player, string, string?, float>> getPlayerTotal =
            new Endpoint<Func<Player, string, string?, float>>("GetPlayerTotal");
        private static readonly Endpoint<Func<ItemDrop.ItemData, string, string?, float>> getItemTotal =
            new Endpoint<Func<ItemDrop.ItemData, string, string?, float>>("GetItemTotal");

        private static readonly Endpoint<Func<Player, string?>> getPlayerInscriptionsJson =
            new Endpoint<Func<Player, string?>>("GetPlayerInscriptionsJson");

        /// <summary>Adds or replaces an inscription (under the YAML: a YAML entry with the id changes only the fields it names).</summary>
        public static bool RegisterInscription(string json) => Safe.Call(registerInscription.Call, json, false);

        /// <summary>Lets an inscription roll on a class: <paramref name="bestFit"/> every tier open, else its top tiers closed.</summary>
        public static bool AddToPool(string classId, string inscriptionId, bool bestFit) =>
            Safe.Call(addToPool.Call, classId, inscriptionId, bestFit, false);

        /// <summary>An effect EliteCrafting rolls, shows and sums but does not apply.</summary>
        public static bool RegisterExternalEffect(string json) => Safe.Call(registerExternalEffect.Call, json, false);

        /// <summary>
        /// The capped total of a player-global effect on the player: live for the local player, the published value for
        /// another player (the effects that publish one), else 0.
        /// </summary>
        public static float GetPlayerTotal(Player player, string effect, string? param = null) =>
            Safe.Call(getPlayerTotal.Call, player, effect, param, 0f);

        /// <summary>The item's own capped sum of an effect.</summary>
        public static float GetItemTotal(ItemDrop.ItemData item, string effect, string? param = null) =>
            Safe.Call(getItemTotal.Call, item, effect, param, 0f);

        /// <summary>
        /// The local player's active inscriptions, one entry per inscription with its values summed and worded as the
        /// tooltip words a line: <c>[{ "id", "category", "line", "total", "capped", "item_local", "sources": [{ "item",
        /// "value" }] }]</c>. Null without EliteCrafting or with one before 0.7.0, which lacks it: ask
        /// <see cref="CraftingLink.HasEndpoint"/> first to keep the missing-endpoint line out of the log.
        /// </summary>
        public static string? GetPlayerInscriptionsJson(Player player) => Safe.Call(getPlayerInscriptionsJson.Call, player, null);
    }
}
