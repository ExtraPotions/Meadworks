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
    /// The game's crafting path is left whole, so every mod that takes part in it keeps working (a reach pull can move
    /// a shortfall out of nearby chests before the game consumes it; GrindstoneSkills splits kitchen crafts):
    /// <c>DoCrafting</c> opens a window and remembers what the player is holding, notes removals from every other
    /// inventory while the craft runs, and notes which ingredients the game charged. After a craft that went through,
    /// a free craft returns each inventory's share to that inventory, at the quality that left: the player's own
    /// materials to the player, and anything that left a chest back to that chest. An extra craft adds copies of what
    /// was made. Nothing is refunded when nothing was charged (no-cost worlds and cheats). The crafting player's own
    /// client: the crafting panel is local.
    /// </para>
    /// </summary>
    internal static class CraftBonus
    {
        private readonly struct Source
        {
            public Source(GameObject prefab, int amount, int quality, Inventory? chest)
            {
                Prefab = prefab;
                Amount = amount;
                Quality = quality;
                Chest = chest;
            }

            public GameObject Prefab { get; }
            public int Amount { get; }
            public int Quality { get; }

            /// <summary>The inventory the materials left; null when they left the crafter.</summary>
            public Inventory? Chest { get; }
        }

        private sealed class ChestTake
        {
            public Inventory Inventory = null!;
            public GameObject Prefab = null!;
            public string Name = "";
            public int Amount;
            public int Quality;
        }

        private readonly struct Charge
        {
            public Charge(string name, int quality)
            {
                Name = name;
                Quality = quality;
            }

            public string Name { get; }
            public int Quality { get; }
        }

        private readonly struct HeldStack
        {
            public HeldStack(ItemDrop.ItemData item, string name, int quality, int amount)
            {
                Item = item;
                Name = name;
                Quality = quality;
                Amount = amount;
            }

            public ItemDrop.ItemData Item { get; }
            public string Name { get; }
            public int Quality { get; }
            public int Amount { get; }
        }

        private static readonly List<Charge> Charges = new List<Charge>();
        private static readonly List<ChestTake> Chests = new List<ChestTake>();
        private static readonly List<HeldStack> StartStacks = new List<HeldStack>();
        private static readonly Dictionary<string, int> Stock = new Dictionary<string, int>();
        private static Recipe? _recipe;
        private static Inventory? _inventory;
        private static int _crafts;
        private static ItemDrop.ItemData? _made;
        private static bool _open;

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
            _open = true;
            RememberStock();
        }

        /// <summary>Inventory.RemoveItem by name inside the window: one of the recipe's ingredients was charged.</summary>
        public static void NotePayment(Inventory inventory, string name, int amount, int quality)
        {
            if (!_open || _recipe == null || _made == null || amount <= 0 || !ReferenceEquals(inventory, _inventory))
            {
                return;
            }
            if (Ingredient(_recipe, name) == null)
            {
                return;
            }
            foreach (Charge charge in Charges)
            {
                if (charge.Name == name)
                {
                    return;
                }
            }
            Charges.Add(new Charge(name, quality));
        }

        /// <summary>A stack left another inventory during the open craft. The player's loss is the earlier stock snapshot.</summary>
        public static void NoteStackChange(Inventory inventory, ItemDrop.ItemData? item, int before)
        {
            if (!_open || _recipe == null || item?.m_shared == null || item.m_dropPrefab == null
                || ReferenceEquals(inventory, _inventory))
            {
                return;
            }
            int left = inventory.ContainsItem(item) ? item.m_stack : 0;
            int removed = System.Math.Max(0, before - left);
            if (removed <= 0 || Ingredient(_recipe, item.m_shared.m_name) == null)
            {
                return;
            }
            foreach (ChestTake take in Chests)
            {
                if (ReferenceEquals(take.Inventory, inventory) && take.Name == item.m_shared.m_name && take.Quality == item.m_quality)
                {
                    take.Amount += removed;
                    return;
                }
            }
            Chests.Add(new ChestTake
            {
                Inventory = inventory,
                Prefab = item.m_dropPrefab,
                Name = item.m_shared.m_name,
                Amount = removed,
                Quality = item.m_quality,
            });
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
            int crafts = _crafts;
            List<List<Source>> groups = Collect();
            // Closes the window first: nothing given back below is noted as a payment or a crafted item.
            _open = false;
            _recipe = null;
            if (recipe != null && made != null)
            {
                AggregateValues v = AggregateHost.Current;
                Refund(player, groups, Rolls(crafts, v[EffectKind.CraftSave]), crafts);
                MakeMore(player, made, Rolls(crafts, v[EffectKind.CraftExtra]) * Mathf.Max(1, recipe.m_amount));
            }
            Clear();
        }

        public static void Clear()
        {
            _open = false;
            _recipe = null;
            _inventory = null;
            _made = null;
            _crafts = 0;
            Charges.Clear();
            Chests.Clear();
            StartStacks.Clear();
            Stock.Clear();
        }

        // What the player held when the craft opened, before a reach pull moves chest materials in.
        private static void RememberStock()
        {
            Stock.Clear();
            StartStacks.Clear();
            if (_inventory == null)
            {
                return;
            }
            foreach (ItemDrop.ItemData item in _inventory.GetAllItems())
            {
                string name = item.m_shared.m_name;
                if (!Stock.ContainsKey(name))
                {
                    Stock[name] = _inventory.CountItems(name, -1, false);
                }
                // The name-based payment is quality -1 ("any") and does not say which stack left.
                StartStacks.Add(new HeldStack(item, name, item.m_quality, item.m_stack));
            }
        }

        private static int Held(string name) => _inventory != null ? _inventory.CountItems(name, -1, false) : 0;

        // One group per charged ingredient: the player's net loss, then each chest that lost some.
        private static List<List<Source>> Collect()
        {
            var groups = new List<List<Source>>();
            if (_recipe == null)
            {
                return groups;
            }
            foreach (Charge charge in Charges)
            {
                GameObject? prefab = Ingredient(_recipe, charge.Name);
                if (prefab == null)
                {
                    continue;
                }
                int before = Stock.TryGetValue(charge.Name, out int stock) ? stock : 0;
                int playerNet = System.Math.Max(0, before - Held(charge.Name));
                var sources = new List<Source>();
                if (playerNet > 0)
                {
                    AddPlayerSources(sources, prefab, charge.Name, playerNet, charge.Quality);
                }
                foreach (ChestTake take in Chests)
                {
                    if (take.Name == charge.Name && take.Amount > 0 && take.Prefab != null)
                    {
                        sources.Add(new Source(take.Prefab, take.Amount, take.Quality, take.Inventory));
                    }
                }
                if (sources.Count > 0)
                {
                    groups.Add(sources);
                }
            }
            return groups;
        }

        // The player's net loss, split by the quality those stacks had. Anything the snapshot does not cover keeps the
        // payment's quality, and quality -1 ("any") stays quality 1 only for that uncovered remainder.
        private static void AddPlayerSources(List<Source> sources, GameObject prefab, string name, int playerNet, int chargeQuality)
        {
            int left = playerNet;
            foreach (HeldStack held in StartStacks)
            {
                if (left <= 0 || held.Name != name)
                {
                    continue;
                }
                int now = _inventory != null && _inventory.ContainsItem(held.Item) ? System.Math.Max(0, held.Item.m_stack) : 0;
                int lost = System.Math.Min(left, System.Math.Max(0, held.Amount - now));
                if (lost <= 0)
                {
                    continue;
                }
                AddQuality(sources, prefab, System.Math.Max(1, held.Quality), lost);
                left -= lost;
            }
            if (left > 0)
            {
                AddQuality(sources, prefab, System.Math.Max(1, chargeQuality), left);
            }
        }

        private static void AddQuality(List<Source> sources, GameObject prefab, int quality, int amount)
        {
            for (int i = 0; i < sources.Count; i++)
            {
                Source source = sources[i];
                if (source.Chest == null && source.Quality == quality && ReferenceEquals(source.Prefab, prefab))
                {
                    sources[i] = new Source(prefab, source.Amount + amount, quality, null);
                    return;
                }
            }
            sources.Add(new Source(prefab, amount, quality, null));
        }

        // Each source's share of the free crafts goes back where it left, at the quality that source lost.
        private static void Refund(Player player, List<List<Source>> groups, int free, int crafts)
        {
            if (free <= 0 || crafts <= 0 || groups.Count == 0)
            {
                return;
            }
            bool any = false;
            foreach (List<Source> sources in groups)
            {
                int total = 0;
                foreach (Source source in sources)
                {
                    total += source.Amount;
                }
                int back = total * free / crafts;
                if (back <= 0 || total <= 0)
                {
                    continue;
                }
                int[] share = Shares(sources, back, total);
                for (int i = 0; i < sources.Count; i++)
                {
                    if (share[i] <= 0)
                    {
                        continue;
                    }
                    any = true;
                    Return(player, sources[i], share[i]);
                }
            }
            if (any)
            {
                player.Message(MessageHud.MessageType.TopLeft, Words.Localize("$ecf_fx_materials_kept"));
            }
        }

        // Several of the player's qualities take the one share a single player total would take, starting from the stacks consumed first.
        private static int[] Shares(List<Source> sources, int back, int total)
        {
            int players = 0;
            while (players < sources.Count && sources[players].Chest == null)
            {
                players++;
            }
            if (players <= 1)
            {
                return Split(sources, back, total);
            }
            var block = new List<Source>(sources.Count - players + 1);
            int playerTotal = 0;
            for (int i = 0; i < players; i++)
            {
                playerTotal += sources[i].Amount;
            }
            block.Add(new Source(sources[0].Prefab, playerTotal, sources[0].Quality, null));
            for (int i = players; i < sources.Count; i++)
            {
                block.Add(sources[i]);
            }
            int[] blockShare = Split(block, back, total);
            var share = new int[sources.Count];
            int left = blockShare[0];
            for (int i = 0; i < players; i++)
            {
                int n = System.Math.Min(sources[i].Amount, left);
                share[i] = n;
                left -= n;
            }
            for (int i = players; i < sources.Count; i++)
            {
                share[i] = blockShare[i - players + 1];
            }
            return share;
        }

        // The last source takes the remainder so the shares add up to the free amount, and none exceeds what that source lost.
        private static int[] Split(List<Source> sources, int back, int total)
        {
            var share = new int[sources.Count];
            int used = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                int part = i == sources.Count - 1 ? back - used : sources[i].Amount * back / total;
                if (part > sources[i].Amount)
                {
                    part = sources[i].Amount;
                }
                if (part > back - used)
                {
                    part = back - used;
                }
                if (part < 0)
                {
                    part = 0;
                }
                share[i] = part;
                used += part;
            }
            for (int i = sources.Count - 1; i >= 0 && used < back; i--)
            {
                int room = sources[i].Amount - share[i];
                int add = System.Math.Min(room, back - used);
                share[i] += add;
                used += add;
            }
            return share;
        }

        private static void Return(Player player, Source source, int count)
        {
            ItemDrop.ItemData? item = ItemGiver.FromPrefab(source.Prefab, source.Quality);
            if (item == null)
            {
                return;
            }
            if (source.Chest == null)
            {
                ItemGiver.Give(player, item, count);
                return;
            }
            int max = Mathf.Max(1, item.m_shared.m_maxStackSize);
            int left = count;
            while (left > 0)
            {
                int stack = Mathf.Min(left, max);
                left -= stack;
                ItemDrop.ItemData copy = item.Clone();
                copy.m_stack = stack;
                copy.m_equipped = false;
                if (source.Chest.AddItem(copy))
                {
                    continue;
                }
                if (copy.m_stack > 0)
                {
                    ItemGiver.Give(player, copy, copy.m_stack);
                }
            }
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
        // First, so the stock snapshot is taken before a reach pull (Burrow's craft prefix is Priority.Last).
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new[] { typeof(ItemDrop.ItemData), typeof(int) })]
        private static void StackBefore(ItemDrop.ItemData item, out int __state) => __state = item != null ? item.m_stack : 0;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new[] { typeof(ItemDrop.ItemData), typeof(int) })]
        private static void StackAfter(Inventory __instance, ItemDrop.ItemData item, int __state) =>
            CraftBonus.NoteStackChange(__instance, item, __state);

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
