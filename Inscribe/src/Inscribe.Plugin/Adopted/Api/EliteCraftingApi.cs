// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Api
{
    /// <summary>
    /// ExtraPotions.Inscribe.Adopted's public API for other mods (features/api.md). Bind to it by reflection, never by reference: the
    /// <c>ValheimModLibs/ExtraPotions.Inscribe.AdoptedLink</c> library does that and does nothing when ExtraPotions.Inscribe.Adopted is absent or older.
    /// <para>
    /// Rules every endpoint keeps: it never throws (an internal failure is logged once and answers false, null or 0);
    /// a callback handed in (classifier, provider, filter, listener) runs guarded, so its exception is logged once per
    /// source and counts as no answer; definitions are JSON objects with the YAML format 2 field names, read by the
    /// same parsers as the rule files; registrations are code, so every peer must make the same ones (they sit under the
    /// YAML layers, which a server's synced files still override); registrations made after the rules loaded rebuild
    /// the rules once at the end of the frame. Main thread only. An endpoint is added, never changed; a removed one stays
    /// as a no-op.
    /// </para>
    /// </summary>
    public static class InscribeApi
    {
        /// <summary>This API's version; a caller needing newer endpoints checks <see cref="GetApiVersion"/> or <see cref="HasEndpoint"/>.</summary>
        public const int ApiVersion = 1;

        // ---- 1. shape

        public static int GetApiVersion() => ApiVersion;

        public static string GetPluginVersion() => global::ExtraPotions.Inscribe.Runtime.Plugin.Version;

        /// <summary>Whether a public endpoint of this name exists (any overload).</summary>
        public static bool HasEndpoint(string name) => ApiGuard.Run("HasEndpoint", ApiEndpoints.Has, name, false);

        /// <summary>Every endpoint name, sorted.</summary>
        public static string[] GetEndpointNames() => ApiGuard.Run("GetEndpointNames", ApiEndpoints.All, Array.Empty<string>());

        // ---- 2. item classes and levels

        /// <summary>Adds or replaces a class: <c>{ id, group, name, rolls, damage_scale, drop_weight, match, items }</c>.</summary>
        public static bool RegisterItemClass(string json) => ApiGuard.Run("RegisterItemClass", ApiClasses.Register, json, false);

        /// <summary>Puts these prefabs in the class (step 1 of classification; the class may be registered later).</summary>
        public static bool ClaimItems(string classId, string[] prefabs) =>
            ApiGuard.Run("ClaimItems", ApiClasses.Claim, classId, prefabs, false);

        /// <summary>A callback naming a class id for the item types it knows, null for the rest (step 2); asked once per item type.</summary>
        public static bool RegisterClassifier(string id, Func<ItemDrop.ItemData, string> classify) =>
            ApiGuard.Run("RegisterClassifier", ApiClasses.AddClassifier, id, classify, false);

        public static bool UnregisterClassifier(string id) => ApiGuard.Run("UnregisterClassifier", ApiClasses.RemoveClassifier, id, false);

        /// <summary>A prefab's item level, 1-8, under the YAML's <c>item_tiers.items</c> and above the material derivation.</summary>
        public static bool SetItemLevel(string prefab, int level) => ApiGuard.Run("SetItemLevel", ApiClasses.SetLevel, prefab, level, false);

        /// <summary>The item's class id; null when it has none.</summary>
        public static string? GetItemClass(ItemDrop.ItemData item) => ApiGuard.Run("GetItemClass", ApiClasses.ClassOf, item, null);

        /// <summary>The item's level, 1-8 (0 for no item).</summary>
        public static int GetItemLevel(ItemDrop.ItemData item) => ApiGuard.Run("GetItemLevel", ApiClasses.LevelOf, item, 0);

        // ---- 3. inscriptions and effects

        /// <summary>Adds or replaces an inscription (YAML format 2 fields, complete; its effect must be registered).</summary>
        public static bool RegisterInscription(string json) => ApiGuard.Run("RegisterInscription", ApiInscriptions.Register, json, false);

        /// <summary>Lets an inscription roll on a class, with every tier open (<paramref name="bestFit"/>) or its top tiers closed.</summary>
        public static bool AddToPool(string classId, string inscriptionId, bool bestFit) =>
            ApiGuard.Run("AddToPool", ApiInscriptions.AddToPool, classId, inscriptionId, bestFit, false);

        /// <summary>An effect ExtraPotions.Inscribe.Adopted rolls, shows and sums but does not apply: <c>{ id, scope, value, polarity, cap, param }</c>.</summary>
        public static bool RegisterExternalEffect(string json) =>
            ApiGuard.Run("RegisterExternalEffect", ApiInscriptions.RegisterEffect, json, false);

        /// <summary>The capped total of a player-global effect channel on the player (inscription units).</summary>
        public static float GetPlayerTotal(Player player, string effect, string? param = null) =>
            ApiGuard.Run("GetPlayerTotal", ApiTotals.Player, player, effect, param, 0f);

        /// <summary>The item's own capped sum of an effect (inscription units).</summary>
        public static float GetItemTotal(ItemDrop.ItemData item, string effect, string? param = null) =>
            ApiGuard.Run("GetItemTotal", ApiTotals.Item, item, effect, param, 0f);

        /// <summary>
        /// The local player's active inscriptions on everything that counts, one entry per inscription, values summed and
        /// worded as the tooltip words a line: <c>[{ "id", "category", "line", "total", "capped", "item_local",
        /// "sources": [{ "item", "value" }] }]</c>. Another player: an empty list.
        /// </summary>
        public static string? GetPlayerInscriptionsJson(Player player) =>
            ApiGuard.Run("GetPlayerInscriptionsJson", ApiPlayerLines.Json, player, null);

        // ---- 4. items

        public static bool IsMagic(ItemDrop.ItemData item) => ApiGuard.Run("IsMagic", ApiItems.IsMagic, item, false);

        /// <summary>The rarity id: <c>normal</c>, <c>magic</c>, <c>rare</c> (or what the YAML defines).</summary>
        public static string? GetRarity(ItemDrop.ItemData item) => ApiGuard.Run("GetRarity", ApiItems.Rarity, item, null);

        /// <summary>The rarity's colour, <c>#RRGGBB</c>.</summary>
        public static string? GetRarityColor(ItemDrop.ItemData item) => ApiGuard.Run("GetRarityColor", ApiItems.RarityColor, item, null);

        /// <summary><c>[{ "id", "tier", "value", "affix", "active" }]</c>, tier as shown (1 strongest).</summary>
        public static string? GetInscriptionsJson(ItemDrop.ItemData item) =>
            ApiGuard.Run("GetInscriptionsJson", ApiItems.InscriptionsJson, item, null);

        /// <summary>The localized item name as the tooltip titles it, rarity colour included.</summary>
        public static string? GetDecoratedName(ItemDrop.ItemData item) => ApiGuard.Run("GetDecoratedName", ApiItems.DecoratedName, item, null);

        /// <summary>A magic base under the current rules and filters.</summary>
        public static bool CanBeMagic(ItemDrop.ItemData item) => ApiGuard.Run("CanBeMagic", ApiItems.CanBeMagic, item, false);

        /// <summary>A fresh roll at the rarity (a magic base, not sealed); written through the item-state writer.</summary>
        public static bool RollMagic(ItemDrop.ItemData item, string rarity) => ApiGuard.Run("RollMagic", ApiItems.RollMagic, item, rarity, false);

        /// <summary>Back to Normal (not on a sealed item).</summary>
        public static bool Cleanse(ItemDrop.ItemData item) => ApiGuard.Run("Cleanse", ApiItems.Cleanse, item, false);

        /// <summary>
        /// The rune prefab (<c>ECF_Ascension</c>) salvaging the item may give back: the Ascension Rune from an Uncommon,
        /// Rare or Epic item; null for none (a Common or Legendary item, or <c>Runes from salvage</c> off). Added in 0.8.0.
        /// </summary>
        public static string? GetSalvageRune(ItemDrop.ItemData item) => ApiGuard.Run("GetSalvageRune", ApiItems.SalvageRune, item, null);

        /// <summary>The chance, 0-1, that salvaging the item gives <see cref="GetSalvageRune"/> back (0 for none); the salvaging mod rolls it.</summary>
        public static float GetSalvageRuneChance(ItemDrop.ItemData item) =>
            ApiGuard.Run("GetSalvageRuneChance", ApiItems.SalvageRuneChance, item, 0f);

        /// <summary>
        /// Draws what EliteCrafting draws on its own icons onto another mod's icon of the item (0.8.0): the rarity backdrop,
        /// the red seal mark and the socket marks, or hides them for an item without them. <paramref name="icon"/> is the
        /// GameObject holding the icon's Image (the backdrop is made as its sibling). Call it whenever the icon shows an item.
        /// </summary>
        public static bool DecorateIcon(UnityEngine.GameObject icon, ItemDrop.ItemData? item) =>
            ApiGuard.Run("DecorateIcon", ApiItems.DecorateIcon, icon, item, false);

        // ---- 5. hooks

        /// <summary>Items a player wears outside the game's slots; their inscriptions count like equipped items.</summary>
        public static bool RegisterEquipmentProvider(string id, Func<Player, List<ItemDrop.ItemData>> extraEquipped) =>
            ApiGuard.Run("RegisterEquipmentProvider", ApiHooks.AddProvider, id, extraEquipped, false);

        public static bool UnregisterEquipmentProvider(string id) =>
            ApiGuard.Run("UnregisterEquipmentProvider", ApiHooks.RemoveProvider, id, false);

        /// <summary>The player's totals are rebuilt (local player, next frame): call after a provider's answer changed.</summary>
        public static void InvalidatePlayer(Player player) => ApiGuard.Run("InvalidatePlayer", ApiHooks.InvalidatePlayer, player, false);

        /// <summary>A veto: false keeps an item type from ever becoming magic (drops, runes, the API).</summary>
        public static bool RegisterMagicBaseFilter(string id, Func<ItemDrop.ItemData, bool> allow) =>
            ApiGuard.Run("RegisterMagicBaseFilter", ApiHooks.AddFilter, id, allow, false);

        public static bool UnregisterMagicBaseFilter(string id) =>
            ApiGuard.Run("UnregisterMagicBaseFilter", ApiHooks.RemoveFilter, id, false);

        /// <summary>After every item-state write: the item and the reason (<c>rune:&lt;id&gt;</c>, <c>drop</c>, <c>command</c>, <c>api</c>, <c>migration</c>).</summary>
        public static bool AddItemChangedListener(Action<ItemDrop.ItemData, string> listener) =>
            ApiGuard.Run("AddItemChangedListener", ApiHooks.AddChangedListener, listener, false);

        public static bool RemoveItemChangedListener(Action<ItemDrop.ItemData, string> listener) =>
            ApiGuard.Run("RemoveItemChangedListener", ApiHooks.RemoveChangedListener, listener, false);

        /// <summary>After a pre-rolled magic item is made for a drop.</summary>
        public static bool AddLootGeneratedListener(Action<ItemDrop.ItemData> listener) =>
            ApiGuard.Run("AddLootGeneratedListener", ApiHooks.AddLootListener, listener, false);

        public static bool RemoveLootGeneratedListener(Action<ItemDrop.ItemData> listener) =>
            ApiGuard.Run("RemoveLootGeneratedListener", ApiHooks.RemoveLootListener, listener, false);

        /// <summary>A creature's loot profile: <c>{ prefab, boss, tier, multiplier, rune_multiplier, rune_rolls, gear_rolls, bonus }</c>.</summary>
        public static bool SetCreatureLoot(string json) => ApiGuard.Run("SetCreatureLoot", ApiLoot.Set, json, false);
    }
}
