// Magpie - ExtraPotions Meadworks loot handling.
// Adapted from FloatingItems 1.0.1 Plugin.cs, ItemDrop_Awake_Patch (https://github.com/Ab5oluteZer0/valheim-floating-items).
// Portions Copyright (c) 2026 Ab5oluteZer0, MIT. Pinned commit f9f72993c0a958403003c2f085fc6f21161a875e.
// The MIT permission notice is retained in THIRD_PARTY_NOTICES/FloatingItems-MIT.txt.
// Changed by expDARE, 2026: the exclusion list comes from Magpie's server-authoritative Excluded Items (Crucible C01,
// parsed by Magpie.Core NameList with the same trim and case rule), the Float module switch and step-aside (C09) gate
// it, errors go through Crucible logging (C06); the legacy config migration and Game.isModded stay with Crucible and the
// C09 converter.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Magpie.Config;
using ExtraPotions.Magpie.StepAside;
using HarmonyLib;

namespace ExtraPotions.Magpie.Runtime.Adopted.FloatingItems
{
    /// <summary>
    /// Before the item's Awake: the game caches the item's <c>Floating</c> component (used among other things for the tar
    /// check), so it must already be there. Patched on instances, not prefabs, so it also covers items added by other mods
    /// whenever they registered them. Every peer adds it (design 3.5); the force acts on the owner.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Awake))]
    internal static class FloatPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ItemDrop __instance)
        {
            try
            {
                if (!MagpieRuntime.ModuleOn(MagpieModule.Float))
                    return;
                var item = __instance.gameObject;
                if (!MagpieRuntime.Settings!.List(MagpieSettings.ExcludedItems).Contains(Utils.GetPrefabName(item)))
                    Buoyancy.TryAdd(item, impactEffects: Plugin.AllowsUi);
            }
            catch (Exception e)
            {
                MagpieRuntime.Log?.Error($"Could not add floating to '{__instance.name}': {e}");
            }
        }
    }
}
