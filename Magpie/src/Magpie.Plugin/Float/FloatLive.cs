// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Magpie.Config;
using ExtraPotions.Magpie.StepAside;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Magpie.Runtime.Adopted.FloatingItems
{
    /// <summary>
    /// Makes Float Enabled and Excluded Items apply to items already in the world. A float Magpie added is never removed
    /// (the item caches it in Awake); while floating is off or the item is excluded its force is skipped. When floating
    /// turns on or an exclusion is removed, items without one get it, with the item's cached reference set as Awake would.
    /// </summary>
    internal static class FloatLive
    {
        // Each float Magpie added, with its prefab name read once: the physics-step gate below must not allocate a name per item.
        private static readonly Dictionary<Floating, string> Added = new Dictionary<Floating, string>();
        private static readonly List<Floating> Gone = new List<Floating>();
        private static float nextPrune;
        private static readonly AccessTools.FieldRef<ItemDrop, Floating> CachedFloat = AccessTools.FieldRefAccess<ItemDrop, Floating>("m_floating");
        private static readonly List<ItemDrop> Instances = (List<ItemDrop>)AccessTools.Field(typeof(ItemDrop), "s_instances").GetValue(null);
        private static bool seen;
        private static bool lastOn;
        private static string? lastExcluded;

        internal static void Track(Floating floating) { if (floating != null) Added[floating] = Utils.GetPrefabName(floating.gameObject); }

        internal static bool Allows(GameObject item) => Allows(Utils.GetPrefabName(item));

        private static bool Allows(string prefab) =>
            MagpieRuntime.ModuleOn(MagpieModule.Float) && !MagpieRuntime.Settings!.List(MagpieSettings.ExcludedItems).Contains(prefab);

        // Destroyed items leave their entries behind; drop them once a minute and on every settings change.
        private static void Prune()
        {
            nextPrune = Time.unscaledTime + 60f;
            foreach (var floating in Added.Keys) if (floating == null) Gone.Add(floating);
            foreach (var floating in Gone) Added.Remove(floating);
            Gone.Clear();
        }

        /// <summary>From Plugin.Update: adds floats to existing items when floating turns on or an exclusion is removed.</summary>
        internal static void Tick()
        {
            if (MagpieRuntime.Settings == null) return;
            if (Time.unscaledTime >= nextPrune) Prune();
            bool on = MagpieRuntime.ModuleOn(MagpieModule.Float);
            string excluded = MagpieRuntime.Settings.Get<string>(MagpieSettings.ExcludedItems);
            if (seen && on == lastOn && excluded == lastExcluded) return;
            bool first = !seen;
            seen = true;
            lastOn = on;
            lastExcluded = excluded;
            Prune();
            if (first || !on || Instances == null) return;
            foreach (var drop in Instances)
            {
                if (drop == null || CachedFloat(drop) != null || !Allows(drop.gameObject)) continue;
                if (Buoyancy.TryAdd(drop.gameObject, impactEffects: Plugin.AllowsUi))
                    CachedFloat(drop) = drop.GetComponent<Floating>();
            }
        }

        [HarmonyPatch(typeof(Floating), nameof(Floating.CustomFixedUpdate))]
        private static class Gate
        {
            [HarmonyPrefix]
            private static bool Prefix(Floating __instance) => !Added.TryGetValue(__instance, out var prefab) || Allows(prefab);
        }
    }
}
