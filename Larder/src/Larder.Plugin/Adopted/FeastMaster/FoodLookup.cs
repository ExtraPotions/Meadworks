// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/FoodLookup.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// The food config of each item kind, kept beside its shared data object, so a hot lookup (the regen hooks every
    /// physics step, item spawns) never reads the prefab's name: a Unity name allocates a new string on every read.
    /// The answer, food or not, is worked out again after more foods are bound (<see cref="Forget"/>), and is not kept
    /// for an item that has no prefab yet.
    /// </summary>
    internal static class FoodLookup
    {
        private sealed class Answer
        {
            public int Generation;
            public Dictionary<string, ConfigEntry<float>> Configs;
        }

        private static readonly ConditionalWeakTable<ItemDrop.ItemData.SharedData, Answer> answers =
            new ConditionalWeakTable<ItemDrop.ItemData.SharedData, Answer>();
        private static int generation;

        /// <summary>Called when a food is bound: every kept answer is looked up again on its next use.</summary>
        public static void Forget() => generation++;

        public static bool TryGet(ItemDrop.ItemData item, out Dictionary<string, ConfigEntry<float>> configs)
        {
            ItemDrop.ItemData.SharedData shared = item.m_shared;
            if (shared == null)
                return FeastMasterData.FindFood(item, out configs);
            if (answers.TryGetValue(shared, out Answer kept) && kept.Generation == generation)
            {
                configs = kept.Configs;
                return configs != null;
            }
            bool found = FeastMasterData.FindFood(item, out configs);
            if (item.m_dropPrefab == null)
                return found;
            if (kept == null)
                answers.Add(shared, kept = new Answer());
            kept.Generation = generation;
            kept.Configs = configs;
            return found;
        }
    }
}
