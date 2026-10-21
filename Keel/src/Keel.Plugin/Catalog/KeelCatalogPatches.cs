// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the catalog's game hooks (K07b): hold size and name when a ship's container wakes, the rudder's
// hover text "Use rudder (Fast Longship)" (design C12; TheGreatestShips README behavior, no code read).
using System;
using ExtraPotions.Keel.Localization;
using HarmonyLib;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// A container woke. Its items load on the next frame (Container.CheckForChanges), so the hold is sized here first:
    /// the game drops a stored item whose slot lies outside the grid. Containers that are not on a ship leave at once.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.Awake))]
    internal static class KeelContainerAwakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Container __instance)
        {
            if (__instance.m_rootObjectOverride == null && __instance.GetComponent<Ship>() == null) return;
            var ship = __instance.GetComponentInParent<Ship>();
            if (ship == null) return;
            try { KeelCatalog.OnContainerAwake(__instance, ship); }
            catch (Exception error) { KeelCatalog.Warn("ship hold", error); }
        }
    }

    /// <summary>"[E] Use rudder (Knarr)": the ship's name in the rudder's hover text.</summary>
    [HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.GetHoverText))]
    internal static class KeelRudderHoverTextPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ShipControlls __instance, ref string __result) => RudderText.Rename(__instance, ref __result);
    }

    [HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.GetHoverName))]
    internal static class KeelRudderHoverNamePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ShipControlls __instance, ref string __result) => RudderText.Rename(__instance, ref __result);
    }

    internal static class RudderText
    {
        /// <summary>Replaces the game's rudder words with Keel's named text; the out-of-reach text stays as it is.</summary>
        public static void Rename(ShipControlls controls, ref string text)
        {
            try
            {
                if (controls == null || string.IsNullOrEmpty(text) || global::Localization.instance == null) return;
                var name = KeelCatalog.ShipName(controls.m_ship);
                if (name.Length == 0) return;
                var rudder = global::Localization.instance.Localize(controls.m_hoverText);
                if (string.IsNullOrEmpty(rudder) || !text.Contains(rudder)) return;
                text = text.Replace(rudder, KeelText.Format(KeelTextKeys.RudderUse, name));
            }
            catch (Exception error)
            {
                KeelCatalog.Warn("rudder text", error);
            }
        }
    }
}
