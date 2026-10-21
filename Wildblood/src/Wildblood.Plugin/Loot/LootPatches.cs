// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original patch point on CharacterDrop.GenerateDropList (design section 8, Loot row). This foundation applies only
// the drop multiplier cheats (all 1 unless the Drop Multipliers cheat is allowed and on) through the portable engine in
// Vanilla mode; the level-1 forcing (E7) and the loot modes are B10b, pending native verification.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Loot;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Wildblood.Runtime
{
    [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
    internal static class GenerateDropListPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result)
        {
            try
            {
                var settings = Plugin.Settings;
                if (settings == null || __instance == null || __result == null || !WildbloodModules.Active(WildbloodModule.Loot)) return;
                var multipliers = settings.Multipliers;
                if (multipliers.IsNone) return;
                var character = __instance.m_character;
                var isBoss = character != null && character.IsBoss();
                var table = __instance.m_drops.Where(d => d?.m_prefab != null)
                    .Select(d => new LootRow(d.m_prefab.name, d.m_chance, d.m_amountMin, d.m_amountMax, IsTrophy(d.m_prefab), d.m_onePerPlayer, d.m_levelMultiplier))
                    .ToList();
                var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                foreach (var pair in __result) if (pair.Key != null) prefabs[pair.Key.name] = pair.Key;
                var generated = __result.Where(p => p.Key != null).Select(p => new Drop(p.Key.name, p.Value));
                var ctx = new KillContext(LootMode.Vanilla, character != null ? Math.Max(0, character.GetLevel() - 1) : 0) { IsBoss = isBoss, Multipliers = multipliers };
                var applied = LootEngine.Apply(generated, table, ctx, UnityDice.Instance);
                __result.Clear();
                foreach (var drop in applied)
                    if (prefabs.TryGetValue(drop.Item, out var prefab)) __result.Add(new KeyValuePair<GameObject, int>(prefab, drop.Amount));
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Wildblood] drop list left unchanged: " + error.GetType().Name + ": " + error.Message);
            }
        }

        private static bool IsTrophy(GameObject prefab)
        {
            var item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            return item != null && item.m_itemData?.m_shared != null && item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy;
        }
    }
}
