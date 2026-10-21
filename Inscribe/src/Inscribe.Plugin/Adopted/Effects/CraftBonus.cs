// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Text;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Thrifty Hands (<c>craft_save</c>) and Bountiful Forge (<c>craft_extra</c>): each craft (not an upgrade) has X%
    /// chance to use no materials, and Y% chance to make one more of the recipe's output. A multi-craft rolls once per
    /// craft in it, as the game's own crafting bonus does.
    /// <para>
    /// The game's crafting path is left whole, so every mod that takes part in it keeps working (OpenKeep pays a
    /// shortfall from nearby chests inside <c>Inventory.RemoveItem</c>; GrindstoneSkills splits kitchen crafts):
    /// <c>DoCrafting</c> opens a window, the window notes what the game charged (each
    /// <c>Inventory.RemoveItem(name, amount, quality, worldLevel)</c> on the player's inventory for one of the
    /// recipe's ingredients, whoever ended up paying it) and whether the crafted item arrived (the game's
    /// <c>AddItem</c> for the recipe's item). After a craft that went through, a free craft gives that payment back
    /// into the inventory (judgement call: "uses no materials" as a refund, so it never fights another mod over who
    /// pays) and an extra craft adds copies of what was made. Nothing is refunded when nothing was charged (no-cost
    /// worlds and cheats). The crafting player's own client: the crafting panel is local.
    /// </para>
    /// </summary>
    internal static class CraftBonus
    {
        private readonly struct Payment
        {
            public Payment(GameObject prefab, int amount, int quality)
            {
                Prefab = prefab;
                Amount = amount;
                Quality = quality;
            }

            public GameObject Prefab { get; }
            public int Amount { get; }
            public int Quality { get; }
        }

        private static readonly List<Payment> Paid = new List<Payment>();
        private static Recipe? _recipe;
        private static Inventory? _inventory;
        private static int _crafts;
        private static ItemDrop.ItemData? _made;

        /// <summary>DoCrafting is about to run: open the window for a plain craft when either effect is on.</summary>
        public static void Begin(InventoryGui gui, Player player)
        {
            Clear();
            AggregateValues v = AggregateHost.Current;
            bool any = v[EffectKind.CraftSave] > 0f || v[EffectKind.CraftExtra] > 0f;
            if (!any || !ReferenceEquals(player, Player.m_localPlayer) || gui.m_craftRecipe == null
                || gui.m_craftRecipe.m_item == null || gui.m_craftUpgradeItem != null || AtUpgrader(player))
            {
                return;
            }
            _recipe = gui.m_craftRecipe;
            _inventory = player.GetInventory();
            _crafts = gui.m_multiCrafting ? Mathf.Max(1, gui.m_multiCraftAmount) : 1;
        }

        /// <summary>Inventory.RemoveItem by name inside the window: one of the recipe's ingredients charged.</summary>
        public static void NotePayment(Inventory inventory, string name, int amount, int quality)
        {
            if (_recipe == null || _made == null || amount <= 0 || !ReferenceEquals(inventory, _inventory))
            {
                return;
            }
            GameObject? prefab = Ingredient(_recipe, name);
            if (prefab != null)
            {
                Paid.Add(new Payment(prefab, amount, quality));
            }
        }

        /// <summary>The game's AddItem inside the window: the recipe's item arrived, so the craft went through.</summary>
        public static void NoteMade(Inventory inventory, string name, ItemDrop.ItemData? made)
        {
            if (_recipe != null && _made == null && made != null && ReferenceEquals(inventory, _inventory)
                && name == _recipe.m_item.gameObject.name)
            {
                _made = made.Clone();
            }
        }

        /// <summary>DoCrafting finished normally: roll the two effects for a craft that went through.</summary>
        public static void Finish(Player player)
        {
            Recipe? recipe = _recipe;
            ItemDrop.ItemData? made = _made;
            // Closes the window first: nothing given back below is noted as a payment or a crafted item.
            _recipe = null;
            if (recipe != null && made != null)
            {
                AggregateValues v = AggregateHost.Current;
                Refund(player, Rolls(_crafts, v[EffectKind.CraftSave]), _crafts);
                MakeMore(player, made, Rolls(_crafts, v[EffectKind.CraftExtra]) * Mathf.Max(1, recipe.m_amount));
            }
            Clear();
        }

        public static void Clear()
        {
            _recipe = null;
            _inventory = null;
            _made = null;
            _crafts = 0;
            Paid.Clear();
        }

        // Each payment in the share of the crafts that came free (a quality of -1, "any", gives back quality 1).
        private static void Refund(Player player, int free, int crafts)
        {
            if (free <= 0 || crafts <= 0 || Paid.Count == 0)
            {
                return;
            }
            foreach (Payment payment in Paid)
            {
                int back = payment.Amount * free / crafts;
                ItemDrop.ItemData? item = back > 0 ? ItemGiver.FromPrefab(payment.Prefab, payment.Quality) : null;
                if (item != null)
                {
                    ItemGiver.Give(player, item, back);
                }
            }
            player.Message(MessageHud.MessageType.TopLeft, Words.Localize("$ecf_fx_materials_kept"));
        }

        private static void MakeMore(Player player, ItemDrop.ItemData made, int count)
        {
            if (count > 0)
            {
                ItemGiver.Give(player, made, count);
                player.Message(MessageHud.MessageType.TopLeft, Words.Localize("$ecf_fx_crafted_more", count.ToString()));
            }
        }

        private static int Rolls(int crafts, float chance)
        {
            int hits = 0;
            for (int i = 0; i < crafts && chance > 0f; i++)
            {
                hits += Random.value < chance ? 1 : 0;
            }
            return hits;
        }

        private static GameObject? Ingredient(Recipe recipe, string sharedName)
        {
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (requirement?.m_resItem != null && requirement.m_resItem.m_itemData.m_shared.m_name == sharedName)
                {
                    return requirement.m_resItem.gameObject;
                }
            }
            return null;
        }

        // An upgrader station (the game's chance-based upgrade bench) only ever upgrades.
        private static bool AtUpgrader(Player player)
        {
            CraftingStation? station = player.GetCurrentCraftingStation();
            return station != null && station.m_upgrader;
        }
    }

    /// <summary>The window around the game's crafting and the two calls it watches (see <see cref="CraftBonus"/>).</summary>
    [HarmonyPatch]
    internal static class CraftBonusPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        private static void Open(InventoryGui __instance, Player player) => CraftBonus.Begin(__instance, player);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        private static void Done(Player player) => CraftBonus.Finish(player);

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        private static void Close() => CraftBonus.Clear();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new[] { typeof(string), typeof(int), typeof(int), typeof(bool) })]
        private static void Charged(Inventory __instance, string name, int amount, int itemQuality) =>
            CraftBonus.NotePayment(__instance, name, amount, itemQuality);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[]
        {
            typeof(string), typeof(int), typeof(int), typeof(int), typeof(long), typeof(string), typeof(Vector2i),
            typeof(bool), typeof(bool), typeof(bool),
        })]
        private static void Arrived(Inventory __instance, string name, ItemDrop.ItemData? __result) =>
            CraftBonus.NoteMade(__instance, name, __result);
    }
}
