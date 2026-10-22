// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using ExtraPotions.Bestiary.Config;
using ExtraPotions.Bestiary.Conversion;
using ExtraPotions.Bestiary.Interop;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Bestiary.Runtime.Integration
{
    /// <summary>
    /// Step-aside (design 11.1, C09) and optional sibling interop (design 9) through Crucible capability ids only.
    /// EliteCreaturesPack installed: the whole product steps aside with one notice. CreatureManager with creature files:
    /// the workshop module steps aside (the source module is gated independently). Wildblood and Inscribe are optional: their absence is
    /// the standalone behavior, and a failing provider is logged once and never stops Bestiary.
    /// </summary>
    internal static class BestiaryIntegration
    {
        private static MeadworksLog? log;
        private static ProductRegistration? registration;

        /// <summary>EliteCreaturesPack is installed: Bestiary registers no prefabs and runs nothing this session.</summary>
        public static bool ProductYields { get; private set; }

        /// <summary>The workshop module stays off (CreatureManager owns creature files, or the product yields).</summary>
        public static bool WorkshopYields { get; private set; }

        public static InteropOutcome Wildblood { get; private set; } = InteropOutcome.Absent;
        public static InteropOutcome Inscribe { get; private set; } = InteropOutcome.Absent;

        public static void Initialize(ProductRegistration product, MeadworksLog logger)
        {
            registration = product;
            log = logger;
            product.AddCapability<Func<object, bool>>(CapabilityIds.Disguise, CapabilityIds.Version1, IsDisguised);
            product.AddCapability<Func<int>>(CapabilityIds.BestiaryWorld, CapabilityIds.Version1, ResetMountainClaims);
        }

        /// <summary>After every plugin has loaded (Start): ask Crucible whether Bestiary or its workshop steps aside.</summary>
        public static void DecideStepAside()
        {
            if (!CruciblePlugin.Services.StepAside.Declared(Plugin.Guid).Any())
                MeadworksStepAside.Declare(Plugin.Guid, BestiaryCore.ProductName, new[] { new SourceMod(StepAsideRules.EcpGuid, "Elite Creatures Pack") });
            ProductYields = MeadworksStepAside.ShouldYield(Plugin.Guid);
            var cmEmpty = WorkshopImport.DomainsEmpty(ReadCreatureManagerDomains());
            var decision = StepAsideRules.Decide(BepInEx.Bootstrap.Chainloader.PluginInfos.Keys, cmEmpty);
            if (!ProductYields && decision.WorkshopYields)
            {
                if (!CruciblePlugin.Services.StepAside.Declared(StepAsideRules.WorkshopId).Any())
                    MeadworksStepAside.Declare(StepAsideRules.WorkshopId, BestiaryCore.ProductName + " workshop",
                        new[] { new SourceMod(StepAsideRules.CreatureManagerGuid, "CreatureManager") });
                MeadworksStepAside.ShouldYield(StepAsideRules.WorkshopId);
            }
            WorkshopYields = ProductYields || decision.WorkshopYields;
        }

        /// <summary>When the scene wakes (every plugin loaded): send the Wildblood defaults and Inscribe tables to whoever provides them.</summary>
        public static void RegisterWithSiblings()
        {
            if (ProductYields || registration == null) return;
            var capabilities = CruciblePlugin.Services.Capabilities;
            capabilities.TryResolve<Func<string, string, bool>>(CapabilityIds.CreatureRules, CapabilityIds.Version1, out var rules);
            var reveal = BestiarySettings.Find(BestiarySettings.MarrowLantern + ".Reveal");
            Wildblood = InteropCalls.RegisterDefaults(rules, reveal != null && Plugin.Settings != null ? Plugin.Settings.Get<int>(reveal) : 12);
            capabilities.TryResolve<Tuple<Func<string, string, bool>, Func<string, int, bool>, Func<string, bool>>>(CapabilityIds.ItemClasses, CapabilityIds.Version1, out var classes);
            var settings = Plugin.Settings;
            Inscribe = settings == null ? InteropOutcome.Absent : InteropCalls.RegisterInscribe(classes,
                settings.Biomes(BestiarySettings.RimeGiant + ".Biomes"), settings.Biomes(BestiarySettings.Slinger + ".Biomes"));
            if (Wildblood == InteropOutcome.Failed || Wildblood == InteropOutcome.Partial) log?.Warning("Wildblood creature defaults: " + Wildblood + "; Bestiary carries on with the game's levels for the rest.");
            if (Inscribe == InteropOutcome.Failed || Inscribe == InteropOutcome.Partial) log?.Warning("Inscribe item classes and loot: " + Inscribe + "; Bestiary items keep the game's rules for the rest.");
            log?.Verbose("Interop: Wildblood " + Wildblood + ", Inscribe " + Inscribe + ".");
        }

        public static void Shutdown()
        {
            registration = null;
            ProductYields = false;
            WorkshopYields = false;
        }

        /// <summary><c>meadworks.creatures.disguise</c> v1: a ZDO of a dormant mimic or a loaded sleeping giant. Never throws.</summary>
        private static bool IsDisguised(object zdo)
        {
            try
            {
                if (!(zdo is ZDO data) || ZNetScene.instance == null) return false;
                var loaded = ZNetScene.instance.FindInstance(data.m_uid);
                if (loaded != null && Native.Core.Disguises.Holding(loaded.GetComponent<Character>()) != null) return true;
                var prefab = ZNetScene.instance.GetPrefab(data.GetPrefab());
                return prefab != null && Disguise.IsDisguised(prefab.name, data.GetBool(BestiaryKeys.Disguised, false));
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary><c>meadworks.bestiary.world</c> v1 (Delve): clears Rime Giant and Slag Charred region claims on the server; the number cleared.</summary>
        private static int ResetMountainClaims()
        {
            if (ZoneSystem.instance == null || ZNet.instance == null || !ZNet.instance.IsServer()) return 0;
            var claims = ZoneSystem.instance.GetGlobalKeys().Where(BestiaryKeys.IsRegionClaim).ToList();
            foreach (var key in claims) ZoneSystem.instance.RemoveGlobalKey(key);
            if (claims.Count > 0) log?.Info("Region claims reset: " + claims.Count + ".");
            return claims.Count;
        }

        private static IReadOnlyDictionary<string, string> ReadCreatureManagerDomains()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                var folder = Path.Combine(Paths.ConfigPath, "CreatureManager");
                if (!Directory.Exists(folder)) return result;
                foreach (var file in Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly).Where(path =>
                    Path.GetExtension(path).Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(path).Equals(".yaml", StringComparison.OrdinalIgnoreCase)))
                    result[Path.GetFileName(file)] = File.ReadAllText(file);
            }
            catch (Exception error)
            {
                log?.Warning("CreatureManager files could not be read; its creature files count as present: " + error.Message);
                result["creatures.yml"] = "unreadable";
            }
            return result;
        }
    }
}
