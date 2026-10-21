// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gerald Glasgow
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Epic Loot reflection hook adopted from OpenKeep 3.0.0 SalvageRarity; optional Inscribe capability added.
// Upstream GPL-3.0-or-later notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: lifecycle, optional capability and Kindle settings.
using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Kindle.Config;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Kindle.Runtime.Salvage
{
    internal static class SalvageRarity
    {
        static MethodInfo apply;
        static ConfigEntry<bool> shown;
        static object instance;
        static readonly object[] args = new object[2];
        internal static void Apply(GameObject icon, ItemDrop.ItemData item)
        {
            if (icon == null || Plugin.Settings == null || !Plugin.Settings.Get(KindleSettings.Salvage.ShowRarityBackgrounds)) return;
            try
            {
                if (CruciblePlugin.IsReady && CruciblePlugin.Services.Capabilities.TryResolve<Action<object,object>>("meadworks.items.rarity_background",new System.Version(1,0),out var hook))
                    hook?.Invoke(icon,item);
            }
            catch (Exception) { } // optional provider failure must not skip the independent Epic Loot hook
            ApplyEpicLoot(icon,item);
        }
        static void ApplyEpicLoot(GameObject icon,ItemDrop.ItemData item)
        {
            try
            {
                if (!Chainloader.PluginInfos.TryGetValue("randyknapp.mods.epicloot",out var info) || info.Instance == null) return;
                if (!ReferenceEquals(instance,info.Instance))
                {
                    instance = info.Instance;
                    apply = info.Instance.GetType().Assembly.GetType("EpicLoot.API")?.GetMethod("ApplyMagicItemBackgroundToIcon",BindingFlags.Public|BindingFlags.Static,
                        null,new[]{typeof(GameObject),typeof(ItemDrop.ItemData)},null);
                    info.Instance.Config.TryGetEntry("Interface","Show Rarity In Recipe List",out shown);
                }
                if (apply == null || (shown != null && !shown.Value)) return;
                args[0]=icon;args[1]=item;apply.Invoke(null,args);
            }
            catch (Exception) { apply=null; } // optional unpublished/changed API is inert
            finally { args[0]=args[1]=null; }
        }
        internal static void Shutdown() { instance=null;apply=null;shown=null;args[0]=args[1]=null; }
    }
}
