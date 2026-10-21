// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Magpie.Text;

namespace ExtraPotions.Magpie.StepAside
{
    /// <summary>Magpie's four modules (design 1.2), each with its own switch and step-aside rule.</summary>
    [Flags]
    public enum MagpieModule
    {
        None = 0,
        Pickup = 1,
        Merge = 2,
        Float = 4,
        Loot = 8,
        All = Pickup | Merge | Float | Loot,
    }

    public readonly struct ModuleSource
    {
        public ModuleSource(string guid, string name)
        {
            Guid = guid;
            Name = name;
        }

        public string Guid { get; }
        public string Name { get; }
    }

    /// <summary>
    /// Design section 9.1 (Crucible C09, per module as in the Shepherd decision): SwiftPickup always takes pickup and
    /// its loot multiplier (both live in its own pickup path) and takes merging only while its <c>MergeNearbyDrops</c>
    /// is on; FloatingItems (current or legacy GUID) takes floating. Each yielding module is reported to Crucible under
    /// the Magpie plugin GUID with its own module id and <c>step_aside_&lt;module&gt;</c> key, so Crucible writes one
    /// notice per module in Magpie's language.
    /// </summary>
    public static class ModuleStepAside
    {
        public const string SwiftPickupGuid = "user.swiftpickup";
        public const string FloatingItemsGuid = "com.ab5olutezer0.valheim.floatingitems";
        public const string LegacyFloatingItemsGuid = "com.michal.valheim.floatingitems";

        /// <summary>SwiftPickup's config file name in <c>BepInEx/config</c>.</summary>
        public const string SwiftPickupConfigFile = SwiftPickupGuid + ".cfg";

        private static readonly ModuleSource Swift = new ModuleSource(SwiftPickupGuid, "SwiftPickup");
        private static readonly ModuleSource Floating = new ModuleSource(FloatingItemsGuid, "FloatingItems");
        private static readonly ModuleSource FloatingLegacy = new ModuleSource(LegacyFloatingItemsGuid, "FloatingItems");

        public static readonly MagpieModule[] Modules = { MagpieModule.Pickup, MagpieModule.Merge, MagpieModule.Float, MagpieModule.Loot };

        /// <summary>Crucible module id of one module (reported under <see cref="MagpieCore.ProductId"/>).</summary>
        public static string ModuleId(MagpieModule module)
        {
            switch (module)
            {
                case MagpieModule.Pickup: return "pickup";
                case MagpieModule.Merge: return "merge";
                case MagpieModule.Float: return "float";
                case MagpieModule.Loot: return "loot";
                default: throw new ArgumentException("Expected exactly one Magpie module.", nameof(module));
            }
        }

        /// <summary>Catalog key of a module's step-aside notice (Crucible's default <c>step_aside_&lt;module&gt;</c>).</summary>
        public static string TextKey(MagpieModule module)
        {
            switch (module)
            {
                case MagpieModule.Pickup: return MagpieTextKeys.StepAsidePickup;
                case MagpieModule.Merge: return MagpieTextKeys.StepAsideMerge;
                case MagpieModule.Float: return MagpieTextKeys.StepAsideFloat;
                case MagpieModule.Loot: return MagpieTextKeys.StepAsideLoot;
                default: throw new ArgumentException("Expected exactly one Magpie module.", nameof(module));
            }
        }

        /// <summary>The first installed source mod the module yields to, named in its notice; null when it does not yield.</summary>
        public static ModuleSource? InstalledSource(MagpieModule module, IEnumerable<string> installed, bool? mergeNearbyDrops)
        {
            var set = new HashSet<string>(installed ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            foreach (var source in Sources(module, mergeNearbyDrops))
                if (set.Contains(source.Guid))
                    return source;
            return null;
        }

        /// <summary>
        /// Source mods a module yields to. <paramref name="mergeNearbyDrops"/> is SwiftPickup's setting; an unreadable
        /// value counts as on, so Magpie never merges alongside a SwiftPickup that may be merging too.
        /// </summary>
        public static IReadOnlyList<ModuleSource> Sources(MagpieModule module, bool? mergeNearbyDrops)
        {
            switch (module)
            {
                case MagpieModule.Pickup:
                case MagpieModule.Loot:
                    return new[] { Swift };
                case MagpieModule.Merge:
                    return mergeNearbyDrops != false ? new[] { Swift } : Array.Empty<ModuleSource>();
                case MagpieModule.Float:
                    return new[] { Floating, FloatingLegacy };
                default:
                    return Array.Empty<ModuleSource>();
            }
        }

        /// <summary>The modules that yield for this set of installed plugin GUIDs.</summary>
        public static MagpieModule Yielding(IEnumerable<string> installed, bool? mergeNearbyDrops)
        {
            var set = new HashSet<string>(installed ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var result = MagpieModule.None;
            foreach (var module in Modules)
                if (Sources(module, mergeNearbyDrops).Any(s => set.Contains(s.Guid)))
                    result |= module;
            return result;
        }

        /// <summary>
        /// Reads <c>[Merge] MergeNearbyDrops</c> from SwiftPickup's BepInEx config text; null when the file, section, key
        /// or a boolean value is missing.
        /// </summary>
        public static bool? ReadMergeNearbyDrops(string? configText)
        {
            if (configText == null) return null;
            var section = "";
            foreach (var raw in configText.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }
                var equals = line.IndexOf('=');
                if (equals <= 0 || !string.Equals(section, "Merge", StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(line.Substring(0, equals).Trim(), "MergeNearbyDrops", StringComparison.OrdinalIgnoreCase)) continue;
                return bool.TryParse(line.Substring(equals + 1).Trim(), out var value) ? value : (bool?)null;
            }
            return null;
        }
    }
}
