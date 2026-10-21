// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Uses existing Crucible module notices and the actual pinned source configuration/rule members.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Conversion;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Shepherd.Runtime.Integration
{
    public static class ShepherdSources
    {
        static bool initialized, warned;
        static float next;
        internal static bool EcrPresent => Chainloader.PluginInfos.ContainsKey(SourceModules.EliteCreaturesReborn);
        public static bool FeedingOwnedElsewhere => initialized && (Chainloader.PluginInfos.ContainsKey(SourceModules.AutoFeedRedux) || NativeHusbandry.SteppedAside);
        public static bool ContainersOwnedElsewhere => initialized && Chainloader.PluginInfos.ContainsKey(SourceModules.OpenKeep)
            && ConfigSwitch(SourceModules.OpenKeep, "8. Homestead", "Pets Eat From Chests") != false;
        public static bool InheritanceOwnedElsewhere => initialized && Chainloader.PluginInfos.ContainsKey(SourceModules.EliteCreaturesReborn) && EcrBreeding() != false;
        internal static SourceMod Source(ImportSource source) => source == ImportSource.AutoFeedRedux ? new SourceMod(SourceModules.AutoFeedRedux, "AutoFeedRedux")
            : source == ImportSource.GrindstoneSkills ? new SourceMod(SourceModules.GrindstoneSkills, "GrindstoneSkills")
            : new SourceMod(SourceModules.EliteCreaturesReborn, "Elite Creatures Reborn");
        public static void Initialize()
        {
            initialized = true; next = 0; warned = false;
            if (CruciblePlugin.Services.StepAside.Declared(Plugin.Guid).Count == 0)
                MeadworksStepAside.Declare(Plugin.Guid, Plugin.Name, Enum.GetValues(typeof(ImportSource)).Cast<ImportSource>().Select(Source)
                    .Append(new SourceMod(SourceModules.OpenKeep, "OpenKeep")));
        }
        public static void Tick()
        {
            if (!initialized || !Plugin.Ready || Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            var decision = SourceModules.Decide(Chainloader.PluginInfos.Keys, NativeHusbandry.SteppedAside, EcrBreeding(),
                ConfigSwitch(SourceModules.OpenKeep, "8. Homestead", "Pets Eat From Chests"));
            if (decision.Feeding)
            {
                if (Chainloader.PluginInfos.ContainsKey(SourceModules.AutoFeedRedux)) Notice("feeding", Source(ImportSource.AutoFeedRedux));
                if (decision.Husbandry) Notice("feeding", Source(ImportSource.GrindstoneSkills));
            }
            else MeadworksStepAside.WithdrawModule(Plugin.Guid, "feeding");
            if (decision.Husbandry) Notice("husbandry", Source(ImportSource.GrindstoneSkills)); else MeadworksStepAside.WithdrawModule(Plugin.Guid, "husbandry");
            if (decision.Inheritance) Notice("inheritance", Source(ImportSource.EliteCreaturesReborn)); else MeadworksStepAside.WithdrawModule(Plugin.Guid, "inheritance");
            if (decision.Containers) Notice("containers", new SourceMod(SourceModules.OpenKeep, "OpenKeep")); else MeadworksStepAside.WithdrawModule(Plugin.Guid, "containers");
        }
        static void Notice(string module, SourceMod source)
        {
            if (Player.m_localPlayer != null) MeadworksStepAside.ReportModule(Plugin.Guid, module, source, "shepherd", "step_aside_" + module);
        }
        static bool? ConfigSwitch(string id, string section, string key)
        {
            if (!Chainloader.PluginInfos.TryGetValue(id, out var plugin)) return false;
            return plugin.Instance != null && plugin.Instance.Config.TryGetEntry<bool>(section, key, out var value) ? value.Value : (bool?)null;
        }
        static bool? EcrBreeding()
        {
            if (!Chainloader.PluginInfos.TryGetValue(SourceModules.EliteCreaturesReborn, out var plugin)) return false;
            try
            {
                // RuleState.Active is the source's synchronized effective rule set, including remote server overlays.
                var type = plugin.Instance?.GetType().Assembly.GetType("EliteCreaturesReborn.Rules.RuleState");
                var active = type?.GetProperty("Active", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                var breeding = active?.GetType().GetField("Breeding", BindingFlags.Public | BindingFlags.Instance)?.GetValue(active);
                return breeding?.GetType().GetField("Enabled", BindingFlags.Public | BindingFlags.Instance)?.GetValue(breeding) as bool?;
            }
            catch (Exception error)
            {
                if (!warned) NativeHusbandry.Report(error, "source breeding rules unavailable; inheritance yields");
                warned = true; return null;
            }
        }
        public static void Shutdown()
        {
            if (initialized) foreach (var module in new[] { "feeding", "husbandry", "inheritance", "containers" }) MeadworksStepAside.WithdrawModule(Plugin.Guid, module);
            initialized = false; next = 0; warned = false;
        }
    }
}
