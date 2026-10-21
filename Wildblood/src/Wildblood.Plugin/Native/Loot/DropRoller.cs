// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Loot
{
    /// <summary>
    /// Rolls single drop rows and a kill's extra rolls, and answers the small questions the engine asks per row. A
    /// game row is rolled exactly the way GenerateDropList rolls it at vanilla level 1 (chance 0-1, the world's
    /// resource rate through Game.ScaleDrops, one-per-player, the game's own 100 cap) minus its pseudo-random counter,
    /// which balances repeated kills and would skew if extra rolls also advanced it. A file row uses the file's
    /// conventions instead: chance 0-100 and an inclusive [min, max] amount.
    /// </summary>
    internal static class DropRoller
    {
        /// <summary>The game's own per-row quantity cap in GenerateDropList.</summary>
        public const int AmountCap = 100;

        private static readonly HashSet<string> _warnedMissing = new HashSet<string>();

        /// <summary>One fresh roll of a creature's own table row; 0 when the chance fails.</summary>
        public static int RollGameRow(CharacterDrop.Drop row)
        {
            if (row.m_prefab == null || Random.value > row.m_chance)
            {
                return 0;
            }
            int amount = row.m_dontScale
                ? Random.Range(row.m_amountMin, row.m_amountMax)
                : Game.instance.ScaleDrops(row.m_prefab, row.m_amountMin, row.m_amountMax);
            if (row.m_onePerPlayer)
            {
                amount = ZNet.instance.GetNrOfPlayers();
            }
            return Mathf.Clamp(amount, 0, AmountCap);
        }

        /// <summary>The extra rolls a kill earns under Rolled: one gate per star, in order, so `max extra rolls` caps
        /// the successes and not the attempts.</summary>
        public static int ExtraRolls(LootRules loot, int stars)
        {
            int rolls = 0;
            for (int star = 1; star <= stars; star++)
            {
                if (loot.MaxExtraRolls > 0 && rolls >= loot.MaxExtraRolls)
                {
                    break;
                }
                if (Random.value * 100f <= loot.ExtraRollChanceAt(star))
                {
                    rolls++;
                }
            }
            return rolls;
        }

        /// <summary>One fresh roll of a rule-file row; 0 when the chance fails.</summary>
        public static int RollRuleRow(DropRule row)
        {
            if (Random.value * 100f > row.Chance)
            {
                return 0;
            }
            return Mathf.Clamp(Random.Range(row.AmountMin, row.AmountMax + 1), 0, AmountCap);
        }

        /// <summary>The item prefab a rule row names, warning once per unknown name rather than once per kill.</summary>
        public static GameObject? FindPrefab(string name)
        {
            GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (prefab == null && _warnedMissing.Add(name))
            {
                Log.Warn($"loot rule names '{name}', which is not a prefab this game knows - row skipped");
            }
            return prefab;
        }

        public static bool IsTrophy(GameObject prefab)
        {
            ItemDrop item = prefab != null ? prefab.GetComponent<ItemDrop>() : null!;
            return item != null && item.m_itemData?.m_shared != null
                && item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy;
        }

        /// <summary>Adds a quantity into the drop list, merging with an existing entry for the same prefab.</summary>
        public static void Add(List<KeyValuePair<GameObject, int>> result, GameObject prefab, int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i].Key == prefab)
                {
                    result[i] = new KeyValuePair<GameObject, int>(prefab, Mathf.Min(result[i].Value + amount, AmountCap));
                    return;
                }
            }
            result.Add(new KeyValuePair<GameObject, int>(prefab, amount));
        }

        /// <summary>
        /// Scales the creature's own table rows by a factor: a multiplier's whole treatment when the loot engine stands
        /// aside (Vanilla mode). Trophies only when <paramref name="trophies"/> says so; another mod's rows never.
        /// </summary>
        public static void ScaleOwnRows(CharacterDrop drop, List<KeyValuePair<GameObject, int>> result, float factor,
            bool trophies)
        {
            if (Mathf.Approximately(factor, 1f))
            {
                return;
            }
            HashSet<GameObject> own = new HashSet<GameObject>();
            foreach (CharacterDrop.Drop row in drop.m_drops)
            {
                if (row.m_prefab != null)
                {
                    own.Add(row.m_prefab);
                }
            }
            for (int i = 0; i < result.Count; i++)
            {
                if (own.Contains(result[i].Key) && (trophies || !IsTrophy(result[i].Key)))
                {
                    result[i] = new KeyValuePair<GameObject, int>(result[i].Key, Scaled(result[i].Value, factor));
                }
            }
        }

        /// <summary>A quantity scaled by a multiplier, never below one: to remove a drop, remove the row instead.</summary>
        public static int Scaled(int amount, float multiplier) =>
            Mathf.Clamp(Mathf.Max(1, Mathf.RoundToInt(amount * multiplier)), 1, AmountCap);
    }
}
