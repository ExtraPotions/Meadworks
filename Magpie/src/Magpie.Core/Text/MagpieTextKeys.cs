// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;

namespace ExtraPotions.Magpie.Text
{
    /// <summary>
    /// Keys of Magpie's C05 catalog (<c>assets/localization/&lt;Language&gt;.json</c>, namespace <c>magpie</c>, tokens
    /// <c>$meadworks_magpie_&lt;key&gt;</c>). English is authoritative; a test keeps the catalog and this list equal.
    /// </summary>
    public static class MagpieTextKeys
    {
        public const string Namespace = "magpie";

        /// <summary>Player-facing option name in the blocked-option notice of the loot multiplier (C03).</summary>
        public const string LootMultiplier = "loot_multiplier";

        /// <summary>Module names used in step-aside notices (C09).</summary>
        public const string ModulePickup = "module_pickup";
        public const string ModuleMerge = "module_merge";
        public const string ModuleFloat = "module_float";
        public const string ModuleLoot = "module_loot";

        /// <summary>Per-module step-aside notices (Crucible C09 <c>ReportModule</c>, default keys); <c>{0}</c> is the source mod.</summary>
        public const string StepAsidePickup = "step_aside_pickup";
        public const string StepAsideMerge = "step_aside_merge";
        public const string StepAsideFloat = "step_aside_float";
        public const string StepAsideLoot = "step_aside_loot";

        public const string ImportDone = "import_done";
        public const string ImportSyncIncomplete = "import_sync_incomplete";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            LootMultiplier, ModulePickup, ModuleMerge, ModuleFloat, ModuleLoot,
            StepAsidePickup, StepAsideMerge, StepAsideFloat, StepAsideLoot, ImportDone, ImportSyncIncomplete,
        };

        public static string Token(string key) => "$meadworks_" + Namespace + "_" + key;
    }
}
