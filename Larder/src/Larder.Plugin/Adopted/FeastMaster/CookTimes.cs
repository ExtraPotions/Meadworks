// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/CookTimes.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Larder.Foods;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// Per-recipe cook times: one section per prefab with a CookingStation component in ZNetScene (the game's
    /// cooking stations and oven, and modded ones), one entry per recipe named after the raw item, defaulting to
    /// the game's time. Bound from <see cref="ScenePrefabs"/>, in the same scene load as the food sections, so the
    /// entries exist before the server's first push. In memory the entries are keyed by the raw item's prefab, which every live
    /// station's conversions reference as well.
    /// </summary>
    public static class CookTimes
    {
        private static readonly Dictionary<int, Dictionary<ItemDrop, ConfigEntry<float>>> byStation
            = new Dictionary<int, Dictionary<ItemDrop, ConfigEntry<float>>>();

        internal static void Reset() => byStation.Clear();

        /// <summary>The entries of a station by its prefab hash (the ZDO's prefab), keyed by raw item.</summary>
        public static bool TryGet(int prefabHash, out Dictionary<ItemDrop, ConfigEntry<float>> entries)
        {
            return byStation.TryGetValue(prefabHash, out entries);
        }

        /// <summary>Whether any station's recipe time differs from the game's.</summary>
        public static bool AnyChanged()
        {
            foreach (Dictionary<ItemDrop, ConfigEntry<float>> entries in byStation.Values)
            {
                foreach (ConfigEntry<float> entry in entries.Values)
                {
                    if (Customized.Any(entry))
                        return true;
                }
            }
            return false;
        }

        /// <summary>Binds one station's section unless it has one already. Returns whether a section was added.</summary>
        public static bool Bind(string prefabName, CookingStation station)
        {
            int hash = prefabName.GetStableHashCode();
            if (byStation.ContainsKey(hash))
                return false;
            try
            {
                Piece piece = station.GetComponent<Piece>();
                string token = piece != null ? piece.m_name : "";
                byStation[hash] = BindRecipes(prefabName, () => FeastMasterData.DisplayName(token, prefabName), station.m_conversion);
                return true;
            }
            catch (ArgumentException e)
            {
                NativeFeastMaster.Log.Warning($"Cook times of {prefabName} cannot be configured: {e.Message}");
                return false;
            }
        }

        private static Dictionary<ItemDrop, ConfigEntry<float>> BindRecipes(string station, Func<string> stationName, List<CookingStation.ItemConversion> recipes)
        {
            Dictionary<ItemDrop, ConfigEntry<float>> entries = new Dictionary<ItemDrop, ConfigEntry<float>>();
            foreach (CookingStation.ItemConversion recipe in recipes)
            {
                if (recipe == null || recipe.m_from == null || recipe.m_to == null || entries.ContainsKey(recipe.m_from))
                    continue;
                ItemDrop from = recipe.m_from, to = recipe.m_to;
                entries[from] = NativeFeastMaster.Bindings.Bind(SettingTemplates.SectionPrefix[SettingTemplates.Station] + station, from.name, recipe.m_cookTime,
                    SettingTemplates.Help(SettingTemplates.Station, SettingTemplates.Recipe, FeastMasterData.DisplayName(from), FeastMasterData.DisplayName(to)),
                    acceptableValues: new AcceptableValueRange<float>(0.1f, 86400f),
                    presentation: LarderSettingPresentation.For(SettingTemplates.Station, SettingTemplates.Recipe, stationName,
                        () => new object[] { FeastMasterData.DisplayName(from), FeastMasterData.DisplayName(to) }, () => FeastMasterData.DisplayName(from)));
            }
            return entries;
        }
    }
}
