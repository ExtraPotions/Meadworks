// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates; give reports the measured counts.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Runtime;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// <c>inscribe give &lt;rune_id&gt;|all [count]</c> (console-commands.md section 3): runes into the caller's own
    /// inventory, the rest dropped at their feet. <c>count</c> 1-9999, default 1. <c>all</c> gives <c>count</c> of every
    /// enabled rune. A single id may name a disabled rune or a rune missing from the YAML (its prefab is still
    /// registered, RC-3); the reply says so. The prefab comes from the Items area's registry. Local player only.
    /// </summary>
    internal static class GiveCommand
    {
        public const string Grammar = "inscribe give <rune_id>|all [count]";

        public static void Run(CommandCall call)
        {
            Player? player = Player.m_localPlayer;
            string id = call.Lower(0);
            if (player == null || id.Length == 0)
            {
                call.Fail(player == null ? "needs a player in the world." : "which rune?", Grammar);
                return;
            }
            if (!Counts.TryParse(call, 1, 1, 9999, 1, out int count, Grammar))
            {
                return;
            }
            if (id == "all")
            {
                GiveAll(call, player, count);
            }
            else
            {
                GiveOne(call, player, id, count);
            }
        }

        private static void GiveOne(CommandCall call, Player player, string id, int count)
        {
            GameObject? prefab = StonePrefab(id, out string? problem);
            if (prefab == null)
            {
                call.Fail(problem!, Grammar);
                return;
            }
            StoneDef? def = ActiveRules.Current.Stone(id);
            call.Reply(Placed(prefab, InventorySpawn.GiveStack(player, prefab, count)));
            if (def == null || !def.Enabled)
            {
                call.Detail($"{id} is {(def == null ? "not in the configuration" : "disabled")}: applying it refuses with stone_disabled.");
            }
        }

        private static void GiveAll(CommandCall call, Player player, int count)
        {
            List<string> lines = new List<string>();
            int given = 0;
            foreach (StoneDef stone in ActiveRules.Current.Economy.Stones)
            {
                if (!stone.Enabled)
                {
                    continue;
                }
                GameObject? prefab = StonePrefab(stone.Id, out string? problem);
                lines.Add(prefab == null ? problem! : Placed(prefab, InventorySpawn.GiveStack(player, prefab, count)));
                given += prefab == null ? 0 : 1;
            }
            call.Reply($"{count} of each of {given} enabled runes");
            lines.ForEach(call.Detail);
        }

        private static string Placed(GameObject prefab, SafeInventory.Delivery delivery)
        {
            string name = ItemText.Name(prefab.GetComponent<ItemDrop>().m_itemData);
            string dropped = delivery.Dropped > 0 ? $", {delivery.Dropped} dropped at your feet (inventory full or refused the add)" : "";
            string missing = delivery.Missing > 0 ? $", {delivery.Missing} not created" : "";
            return $"{delivery.Arrived} {name} ({prefab.name}) into the inventory{dropped}{missing}";
        }

        /// <summary>
        /// The prefab of a rune id from the Items area's registry (<see cref="StonePrefabs.Get"/>). It must also be in
        /// the object database, or the runes would be dropped from the inventory on the next load (game notes pitfall 6).
        /// </summary>
        public static GameObject? StonePrefab(string id, out string? problem)
        {
            GameObject? prefab = StonePrefabs.Get(id);
            bool known = ActiveRules.Current.Stone(id) != null || StoneCatalog.IsBuiltIn(id);
            bool inDatabase = prefab != null && ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(prefab.name) == prefab;
            problem = !known ? $"unknown rune '{id}'. Closest: {Closest.To(id, StoneCatalog.BuiltInIds)}"
                : prefab == null ? $"{id}: its rune prefab has not been built (see the log)."
                : !inDatabase ? $"{id}: its prefab {prefab.name} is not registered in the object database."
                : null;
            return problem == null ? prefab : null;
        }
    }

    /// <summary>Numeric argument parsing with the error reply.</summary>
    internal static class Counts
    {
        /// <summary>Argument <paramref name="index"/> as an integer in [min, max]; absent = <paramref name="fallback"/>.</summary>
        public static bool TryParse(CommandCall call, int index, int min, int max, int fallback, out int value, string grammar)
        {
            value = fallback;
            string text = call.Arg(index);
            if (text.Length == 0)
            {
                return true;
            }
            if (Numbers.TryInt(text, out value) && value >= min && value <= max)
            {
                return true;
            }
            call.Fail($"'{text}' should be a whole number from {min} to {max}.", grammar);
            return false;
        }
    }
}
