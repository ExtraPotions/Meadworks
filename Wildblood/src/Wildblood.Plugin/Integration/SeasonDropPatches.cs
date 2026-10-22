// Wildblood - ExtraPotions Meadworks creature difficulty.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ExtraPotions.Wildblood.Runtime
{
    /// <summary>
    /// Meat and hide on the finished drop list, after Wildblood loot and raid loot. The prefab table is not changed.
    /// Runs on the dying creature's owner. A missing view leaves the list as the game built it.
    /// </summary>
    [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
    public static class SeasonMeatPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last - 2)]
        static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result)
        {
            if (__instance == null || __result == null) return;
            float rate = WildbloodSeasons.Meat(Want(WildbloodSettings.Loot.SeasonalMeat));
            if (rate == 1f) return;
            var character = __instance.m_character;
            var view = character != null ? character.m_nview : null;
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            for (int i = __result.Count - 1; i >= 0; i--)
            {
                var pair = __result[i];
                if (pair.Key == null || !SeasonDrops.IsMeatOrHide(pair.Key.name)) continue;
                int scaled = SeasonDrops.Stack(pair.Key.name, pair.Value, rate, true, rate < 1f ? Random.value : 0d);
                if (scaled == 0 && pair.Value > 0) { __result.RemoveAt(i); continue; }
                if (scaled != pair.Value) __result[i] = new KeyValuePair<GameObject, int>(pair.Key, scaled);
            }
        }

        internal static bool Want(WildbloodSetting<bool> setting)
        {
            var settings = Plugin.Settings;
            return settings != null && settings.Get(WildbloodSettings.General.Enabled) && settings.Get(setting);
        }
    }

    /// <summary>
    /// A disposable table identifies the owner's tree drop call without changing its rolls or shared prefab data.
    /// </summary>
    [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Destroy))]
    public static class SeasonWoodLogPatch
    {
        static void Prefix(ZNetView ___m_nview, ref DropTable ___m_dropWhenDestroyed, ref DropTable __state)
        {
            if (___m_nview == null || !___m_nview.IsValid() || !___m_nview.IsOwner()) return;
            SeasonWood.Scale(ref ___m_dropWhenDestroyed, ref __state);
        }

        static void Finalizer(ref DropTable ___m_dropWhenDestroyed, DropTable __state)
        {
            SeasonWood.Release(ref ___m_dropWhenDestroyed, __state);
        }
    }

    [HarmonyPatch(typeof(DropOnDestroyed), nameof(DropOnDestroyed.OnDestroyed))]
    public static class SeasonWoodDestroyPatch
    {
        static void Prefix(DropOnDestroyed __instance, ref DropTable ___m_dropWhenDestroyed, ref DropTable __state)
        {
            if (__instance == null || !__instance.TryGetComponent(out Destructible destructible)) return;
            if ((destructible.GetDestructibleType() & DestructibleType.Tree) == 0) return;
            var view = destructible.m_nview;
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            SeasonWood.Scale(ref ___m_dropWhenDestroyed, ref __state);
        }

        static void Finalizer(ref DropTable ___m_dropWhenDestroyed, DropTable __state)
        {
            SeasonWood.Release(ref ___m_dropWhenDestroyed, __state);
        }
    }

    [HarmonyPatch(typeof(DropTable), nameof(DropTable.GetDropList), new System.Type[0])]
    public static class SeasonWoodListPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last - 2)]
        static void Postfix(DropTable __instance, List<GameObject> __result) => SeasonWood.ScaleList(__instance, __result);
    }

    static class SeasonWood
    {
        static readonly Dictionary<DropTable, float> active = new Dictionary<DropTable, float>();

        internal static void Scale(ref DropTable table, ref DropTable original)
        {
            if (table == null || table.m_drops == null) return;
            float rate = WildbloodSeasons.Wood(SeasonMeatPatch.Want(WildbloodSettings.Loot.SeasonalWood));
            if (rate == 1f || !HasWood(table)) return;
            var copy = table.Clone();
            if (copy == null) return;
            original = table;
            table = copy;
            active.Add(copy, rate);
        }

        internal static void Release(ref DropTable table, DropTable original)
        {
            if (original == null) return;
            active.Remove(table);
            table = original;
        }

        internal static void ScaleList(DropTable table, List<GameObject> list)
        {
            if (list == null || !active.TryGetValue(table, out float rate)) return;
            var counts = new Dictionary<GameObject, int>();
            foreach (var item in list)
                if (item != null && SeasonDrops.IsWood(item.name))
                    counts[item] = counts.TryGetValue(item, out int count) ? count + 1 : 1;
            foreach (var pair in counts)
            {
                int scaled = SeasonDrops.ScaleAmount(pair.Value, rate, rate < 1f ? Random.value : 0d);
                int remove = pair.Value - scaled;
                for (int i = list.Count - 1; i >= 0 && remove > 0; i--)
                    if (list[i] == pair.Key) { list.RemoveAt(i); remove--; }
                for (int i = pair.Value; i < scaled; i++) list.Add(pair.Key);
            }
        }

        static bool HasWood(DropTable table)
        {
            var drops = table.m_drops;
            for (int i = 0; i < drops.Count; i++)
            {
                var item = drops[i].m_item;
                if (item != null && SeasonDrops.IsWood(item.name)) return true;
            }
            return false;
        }
    }
}
