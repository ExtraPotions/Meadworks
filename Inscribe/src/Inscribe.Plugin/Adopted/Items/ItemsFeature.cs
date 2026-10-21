// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// Entry point of the Items area, called once from plugin Awake after the rules, settings and words are loaded.
    /// Owns: prefab registration (the seven runes, ObjectDB/ZNetScene: <see cref="StoneRegistrationPatches"/>),
    /// economy values pushed into the rune prefabs (<see cref="StoneItemData"/>), their look on clients
    /// (<see cref="StoneVisuals"/>), workbench-upgrade carry-over of ecf_ keys (<see cref="UpgradeCarryOver"/>) and the
    /// load warning for stackable items of a class that rolls (<see cref="StackableGearWarning"/>) and whole rune stacks on inventory
    /// load (<see cref="StoneStackGuard"/>).
    /// Harmony patches of this area are ordinary [HarmonyPatch] classes in this folder; the plugin's PatchAll finds them.
    /// </summary>
    public static class ItemsFeature
    {
        public static void Init()
        {
            StoneTints.Rebuild(ActiveRules.Current.Economy);
            StonePrefabs.Built += ApplyRules;
            ActiveRules.RulesChanged += ApplyRules;
            // The rune names and descriptions are localized text: a language change rewrites them.
            Text.Words.Changed += ApplyRules;
        }

        // Every peer: after the prefabs are built, and on every rules change (hot reload, the server's rules arriving).
        private static void ApplyRules() => StoneItemData.ApplyAll(ActiveRules.Current);
        internal static void Shutdown()
        {
            StonePrefabs.Built -= ApplyRules;
            ActiveRules.RulesChanged -= ApplyRules;
            Text.Words.Changed -= ApplyRules;
        }
    }
}
