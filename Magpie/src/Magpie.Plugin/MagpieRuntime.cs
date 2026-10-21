// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using BepInEx;
using ExtraPotions.Magpie.Config;
using ExtraPotions.Magpie.Runtime.Settings;
using ExtraPotions.Magpie.StepAside;
using ExtraPotions.Magpie.Text;
using ExtraPotions.Meadworks.Crucible;
using ModuleStepAside = ExtraPotions.Magpie.StepAside.ModuleStepAside;

namespace ExtraPotions.Magpie.Runtime
{
    /// <summary>
    /// Shared runtime state: settings, log, and whether each module may act right now. A module acts only while Magpie is
    /// registered, the product is active for this session (C02/C04: off for the session on a server without Magpie or a
    /// vanilla server), Enabled and the module switch are on, and the module has not stepped aside for a source mod (C09).
    /// </summary>
    internal static class MagpieRuntime
    {
        public static MagpieConfig? Settings { get; private set; }
        public static MeadworksLog? Log { get; private set; }

        /// <summary>Modules yielding to an installed source mod; decided once after the chainloader finished.</summary>
        public static MagpieModule SteppedAside { get; private set; }

        private static bool stepAsideDecided;

        public static void Initialize(MagpieConfig settings, MeadworksLog log)
        {
            Settings = settings;
            Log = log;
        }

        public static void Shutdown()
        {
            Settings = null;
            stepAsideDecided = false;
            SteppedAside = MagpieModule.None;
        }

        /// <summary>
        /// Decides which modules yield to an installed source mod and reports each one to Crucible under the Magpie plugin
        /// GUID (C09 per-module step-aside: one notice per module and source, Magpie's <c>step_aside_&lt;module&gt;</c>
        /// text). Runs once in the plugin's Start, after every plugin's Awake, so every installed plugin is known (Larder
        /// pattern). SwiftPickup's merge setting is read from its file.
        /// </summary>
        public static void DecideStepAside()
        {
            if (stepAsideDecided) return;
            stepAsideDecided = true;
            var merge = ModuleStepAside.ReadMergeNearbyDrops(ReadSwiftPickupConfig());
            var installed = BepInEx.Bootstrap.Chainloader.PluginInfos.Keys;
            var yielding = MagpieModule.None;
            foreach (var module in ModuleStepAside.Modules)
            {
                var source = ModuleStepAside.InstalledSource(module, installed, merge);
                if (!source.HasValue) continue;
                yielding |= module;
                MeadworksStepAside.ReportModule(Plugin.Guid, ModuleStepAside.ModuleId(module), new SourceMod(source.Value.Guid, source.Value.Name),
                    MagpieTextKeys.Namespace, ModuleStepAside.TextKey(module));
            }
            SteppedAside = yielding;
            if (yielding != MagpieModule.None)
                Log?.Info("Stepping aside for installed source mods: "
                    + string.Join(", ", ModuleStepAside.Modules.Where(m => (yielding & m) != 0).Select(m => MagpieText.Text(NameKey(m)))) + ".");
        }

        /// <summary>Magpie may act in this session at all.</summary>
        public static bool ProductOn =>
            Plugin.Ready && Settings != null && Settings.Flag(MagpieSettings.Enabled) && MeadworksAuthority.ProductActive(Plugin.Guid, out _);

        public static bool ModuleOn(MagpieModule module)
        {
            if (!ProductOn || (SteppedAside & module) != 0) return false;
            switch (module)
            {
                case MagpieModule.Pickup: return Settings!.Flag(MagpieSettings.PickupEnabled);
                case MagpieModule.Merge: return Settings!.Flag(MagpieSettings.MergeEnabled);
                case MagpieModule.Float: return Settings!.Flag(MagpieSettings.FloatEnabled);
                case MagpieModule.Loot: return true;
                default: return false;
            }
        }

        private static string NameKey(MagpieModule module)
        {
            switch (module)
            {
                case MagpieModule.Pickup: return MagpieTextKeys.ModulePickup;
                case MagpieModule.Merge: return MagpieTextKeys.ModuleMerge;
                case MagpieModule.Float: return MagpieTextKeys.ModuleFloat;
                default: return MagpieTextKeys.ModuleLoot;
            }
        }

        private static string? ReadSwiftPickupConfig()
        {
            try
            {
                var path = System.IO.Path.Combine(Paths.ConfigPath, ModuleStepAside.SwiftPickupConfigFile);
                return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
            }
            catch (System.Exception error)
            {
                Log?.Debug("SwiftPickup config not readable: " + error.Message);
                return null;
            }
        }
    }
}
