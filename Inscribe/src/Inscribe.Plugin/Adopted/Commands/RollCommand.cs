// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rolling;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// <c>inscribe roll &lt;rarity&gt; &lt;prefab|class&gt; [level]</c> (console-commands.md section 3): creates a rolled magic
    /// item in the caller's own inventory (dropped at the feet when full). The item is built as a gear drop builds it
    /// (drops.md section 8: upgrade level 1, full durability, the world's world level, no crafter, a random variant)
    /// and rolled with <see cref="ItemRoller.RollFresh"/>, the drop's own procedure, so it is a true sample.
    /// <c>level</c> (1-8) overrides the base's item level for this roll; a class id picks a random base of that class.
    /// Runs on the caller's machine; the item is client-owned like any other.
    /// </summary>
    internal static class RollCommand
    {
        public const string Grammar = "inscribe roll <rarity> <prefab|class> [level]";

        public static void Run(CommandCall call)
        {
            Player? player = Player.m_localPlayer;
            if (player == null)
            {
                call.Reply("needs a player in the world.");
                return;
            }
            RarityDef? rarity = ParseRarity(call);
            if (rarity == null || !Counts.TryParse(call, 2, 1, TierResult.MaxLevel, 0, out int level, Grammar))
            {
                return;
            }
            System.Random random = RollRandom.Create();
            GameObject? prefab = MagicBases.Resolve(call.Arg(1), random, out string? problem);
            if (prefab == null)
            {
                call.Fail(problem!, Grammar);
                return;
            }
            RollInto(call, player, prefab, rarity, level, random);
        }

        private static RarityDef? ParseRarity(CommandCall call)
        {
            string id = call.Lower(0);
            EconomyRules economy = ActiveRules.Current.Economy;
            RarityDef? rarity = economy.Rarity(id);
            if (rarity == null)
            {
                call.Fail(id.Length == 0 ? "which rarity?" : $"unknown rarity '{id}'. Closest: {Closest.To(id, RarityIds(economy))}", Grammar);
                return null;
            }
            if (rarity.IsBase)
            {
                call.Fail($"{id} carries no inscriptions; roll a higher rarity.", Grammar);
                return null;
            }
            return rarity;
        }

        private static string[] RarityIds(EconomyRules economy)
        {
            string[] ids = new string[economy.Rarities.Count];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = economy.Rarities[i].Id;
            }
            return ids;
        }

        private static void RollInto(CommandCall call, Player player, GameObject prefab, RarityDef rarity, int level, System.Random random)
        {
            ItemDrop.ItemData item = InventorySpawn.NewItem(prefab);
            int variants = item.m_shared.m_variants;
            item.m_variant = variants > 1 ? random.Next(variants) : 0;
            RollContext context = RollContext.For(item);
            context.Random = random;
            context.Level = level > 0 ? level : context.Level;
            ItemState? state = RollerCall.Roll(call, () => ItemRoller.RollFresh(ItemState.Empty, rarity, context));
            if (state == null || !RollerCall.Commit(call, item, state))
            {
                return;
            }
            item.m_durability = item.GetMaxDurability();
            bool inInventory = InventorySpawn.GiveItem(player, item);
            ItemReport.Write(call, item, inInventory ? "your inventory" : "the ground at your feet (inventory full)");
            call.Detail($"rolled as class {context.Class.ClassId ?? "-"} at item level {context.Level}" + (level > 0 ? " (overridden)" : " (the base's own)"));
        }
    }
}
