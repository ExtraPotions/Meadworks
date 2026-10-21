// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// Horde adoption adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543.
// Changed by expDARE, 2026: Wildblood authority, lifecycle and existing stat/loot seams.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Loot
{
    /// <summary>
    /// Scales a kill's own loot by a factor, after the loot rules have made the list: a raider's by its raid's drops
    /// (<see cref="Raids.RaidLoot"/>), a horde creature's by 1 / the spawn multiplier (<see cref="Horde.HordeLoot"/>).
    /// It touches the rows the loot engine treats as the creature's own (its table and the rows its <c>creatures:</c>
    /// entry names), trophies only when the trophy switch says so, and never a row another mod put in the list. Below 1
    /// each stack is rounded up or down at random by its fraction, so on average the share is right and a factor of a
    /// tenth mostly drops nothing. Held to the game's per-row cap.
    /// </summary>
    internal static class OwnDrops
    {
        /// <summary>The creature's own drop prefabs for the kill being scaled; filled and emptied in one call.</summary>
        private static readonly HashSet<GameObject> Own = new HashSet<GameObject>();

        public static void Scale(CharacterDrop drop, List<KeyValuePair<GameObject, int>> result, float factor)
        {
            CreatureLootRule? rule = RuleFor(drop);
            bool trophies = rule?.MultiplyTrophies ?? RuleState.Active.Loot.MultiplyTrophies;
            ClaimOwn(drop, rule);
            for (int i = result.Count - 1; i >= 0; i--)
            {
                GameObject item = result[i].Key;
                if (item == null || !Own.Contains(item) || (!trophies && DropRoller.IsTrophy(item)))
                {
                    continue;
                }
                int amount = Mathf.Min(Dice.Round(result[i].Value * factor), DropRoller.AmountCap);
                if (amount > 0)
                {
                    result[i] = new KeyValuePair<GameObject, int>(item, amount);
                }
                else
                {
                    result.RemoveAt(i);
                }
            }
            Own.Clear();
        }

        // The rows the loot engine counts as this creature's own: its table, and every item its rule file entry names.
        private static void ClaimOwn(CharacterDrop drop, CreatureLootRule? rule)
        {
            Own.Clear();
            foreach (CharacterDrop.Drop row in drop.m_drops)
            {
                if (row.m_prefab != null)
                {
                    Own.Add(row.m_prefab);
                }
            }
            if (rule != null)
            {
                ClaimNamed(rule.Overrides);
                ClaimNamed(rule.Extras);
            }
        }

        private static void ClaimNamed(List<DropRule> rows)
        {
            foreach (DropRule row in rows)
            {
                GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(row.Item) : null;
                if (prefab != null)
                {
                    Own.Add(prefab);
                }
            }
        }

        private static CreatureLootRule? RuleFor(CharacterDrop drop) =>
            RuleState.Active.CreatureLoot.TryGetValue(Utils.GetPrefabName(drop.gameObject), out CreatureLootRule rule)
                ? rule : null;
    }
}
