// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>Read-only runtime diagnostics for the active adopted catalog and applied integration hooks.</summary>
    public static class InscribeRuntimeStatus
    {
        public static int RulesGeneration => Adopted.Rules.ActiveRules.Generation;
        public static int ItemClassCount => Adopted.Rules.ActiveRules.Current.Economy.Classes.Count;
        public static int InscriptionCount => Adopted.Rules.ActiveRules.Current.Affixes.Affixes.Count;
        public static int RuneCount => Adopted.Rules.ActiveRules.Current.Economy.Stones.Count;
        public static int AppliedPatchClasses => NativeGameplay.AppliedPatchClasses;
        public static int NativeFailures => InscribeLootInterop.Failures + Plugin.NativeFailures + NativeGameplay.PatchFailures + LegacyEnhancement.PatchFailures;
        public static int PatchFailures => NativeGameplay.PatchFailures;
        /// <summary>Enhancement was removed (0.2.0); kept for existing native-runner waits, it follows the rules generation.</summary>
        public static int EnhancementGeneration => RulesGeneration;
        /// <summary>The four inert old scroll prefabs that let saved scrolls load and convert.</summary>
        public static bool ScrollPrefabsBuilt => LegacyEnhancement.ScrollPrefabsBuilt;
        public static int EnhancedItemsConverted => LegacyEnhancement.ItemsConverted;
        public static int ScrollStacksConverted => LegacyEnhancement.ScrollStacksConverted;
        public static bool RunePrefabsBuilt => Adopted.Items.StonePrefabs.IsBuilt;
    }
}
