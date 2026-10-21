// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Integration;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Inscribe.Runtime
{
    internal static class NativeGameplay
    {
        private static Harmony? harmony;
        private static ConfigFile? config;
        private static bool initialized, rarityStarted;
        private static readonly List<GameObject> owned = new List<GameObject>();
        internal static void ReloadConfig() => config?.Reload();
        internal static void Own(GameObject value) => owned.Add(value);
        internal static int AppliedPatchClasses { get; private set; }
        internal static int PatchFailures { get; private set; }
        public static void Start(ConfigFile file, ManualLogSource logger, MeadworksLog log)
        {
            initialized = true;
            config = file;
            Adopted.Core.Log.Bind(logger);
            ActiveRules.Initialise(file);
            Adopted.Commands.CommandsFeature.Init();
            if (InscribeModules.StepAside.SteppedAside(InscribeModule.Rarity))
            {
                harmony = new Harmony(Plugin.Guid + ".rarity");
                try { harmony.CreateClassProcessor(typeof(Adopted.Commands.TerminalInitPatch)).Patch(); }
                catch (Exception error) { PatchFailures++; log.Error("Inscribe command patch refused: " + error.Message); }
                return;
            }
            rarityStarted = true;
            Adopted.Text.Words.Initialise();
            Adopted.Items.ItemsFeature.Init();
            Adopted.Stones.StonesFeature.Init();
            Adopted.Effects.EffectsFeature.Init();
            Adopted.Loot.LootFeature.Init();
            if (MeadworksHeadless.AllowsUi) Adopted.Display.DisplayFeature.Init();
            harmony = new Harmony(Plugin.Guid + ".rarity");
            AppliedPatchClasses = 0; PatchFailures = 0;
            foreach (Type type in typeof(NativeGameplay).Assembly.GetTypes().Where(t => t.Namespace?.StartsWith("ExtraPotions.Inscribe.Adopted.", StringComparison.Ordinal) == true))
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
                    try { harmony.CreateClassProcessor(type).Patch(); AppliedPatchClasses++; }
                    catch (Exception e) { PatchFailures++; log.Error("Inscribe patch " + type.Name + " refused: " + e.Message); }
            Adopted.Text.Words.InstallNow();
            file.SettingChanged += Changed;
            log.Info("Pinned native rarity engine applied: " + ActiveRules.Current.Affixes.Affixes.Count + " inscriptions, " + ActiveRules.Current.Economy.Classes.Count + " classes and " + ActiveRules.Current.Economy.Stones.Count + " runes.");
        }
        private static void Changed(object sender, SettingChangedEventArgs e) => Adopted.Config.ModSettings.Changed();
        public static void Tick()
        {
            if (harmony == null) return;
            Adopted.Api.RuleRebuild.RunPending();
        }
        public static void Shutdown()
        {
            if (!initialized) return;
            initialized = false;
            harmony?.UnpatchSelf(); harmony = null;
            if (config != null) config.SettingChanged -= Changed;
            config = null;
            Adopted.Rules.RuleReload.Reset();
            if (rarityStarted)
            {
            rarityStarted = false;
            Adopted.Effects.EffectRuntime.Shutdown();
            Adopted.Items.ItemsFeature.Shutdown();
            Adopted.Loot.LootFeature.Shutdown();
            if (MeadworksHeadless.AllowsUi) Adopted.Display.DisplayFeature.Shutdown();
            }
            Adopted.Commands.EcraftCommand.Shutdown();
            Adopted.Rules.ActiveRules.Shutdown();
            Adopted.Affixes.ItemStateCache.Shutdown();
            Adopted.Text.Words.Shutdown();
            foreach (var value in owned) if (value != null) UnityEngine.Object.Destroy(value);
            owned.Clear();
            ExtraPotions.Meadworks.OriginalArt.OriginalItemArt.Shutdown();
        }
    }
}
