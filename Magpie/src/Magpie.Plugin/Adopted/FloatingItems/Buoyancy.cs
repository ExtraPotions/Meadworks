// Magpie - ExtraPotions Meadworks loot handling.
// Adapted from FloatingItems 1.0.1 Buoyancy.cs (https://github.com/Ab5oluteZer0/valheim-floating-items).
// Portions Copyright (c) 2026 Ab5oluteZer0, MIT. Pinned commit f9f72993c0a958403003c2f085fc6f21161a875e.
// The MIT permission notice is retained in THIRD_PARTY_NOTICES/FloatingItems-MIT.txt.
// Changed by expDARE, 2026: Polish comments and log text replaced by English, logging through Crucible C06 (quiet by
// default), impact effects left out on a headless server (design 3.5), namespace and nullability; added floats are tracked for live switching.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Magpie.Runtime.Adopted.FloatingItems
{
    /// <summary>
    /// Gives an item the game's own <c>Floating</c> component. Water (<c>WaterVolume</c>) looks for an
    /// <c>IWaterInteractable</c> on a physics object and hands it the water level; <c>Floating</c> pushes the body up while
    /// its center of mass is below the surface and damps the motion, like wood in the game. The force acts only on the
    /// item's owner, exactly as in the game.
    /// </summary>
    internal static class Buoyancy
    {
        // The item that floats in the game on its own; its settings are the template for the rest.
        private const string TemplateItem = "Wood";

        private static Floating? _template;
        private static bool _templateMissingLogged;

        public static bool TryAdd(GameObject item, bool impactEffects)
        {
            // Already handles water itself: items that float in the game (Floating) and fish (Fish swim with their own
            // logic; pushing them to the surface would break them).
            if (item.GetComponent<IWaterInteractable>() != null)
                return false;
            // Floating uses these three without checking; without a body (an item turned into a building piece) there
            // is nothing to lift.
            if (item.GetComponent<Rigidbody>() == null || item.GetComponent<ZNetView>() == null ||
                item.GetComponentInChildren<Collider>() == null)
                return false;

            var floating = item.AddComponent<Floating>();
            FloatLive.Track(floating);
            var template = Template();
            if (template != null)
                CopySettings(template, floating, impactEffects);
            return true;
        }

        // The surface effect (m_surfaceEffects) stays empty: it is a child object of the template prefab, shared by all
        // copies, and switching it from many items would toggle the template. The impact splash (m_impactEffects) is only
        // a list of prefabs to create, so it can be shared.
        private static void CopySettings(Floating from, Floating to, bool impactEffects)
        {
            to.m_waterLevelOffset = from.m_waterLevelOffset;
            to.m_forceDistance = from.m_forceDistance;
            to.m_force = from.m_force;
            to.m_balanceForceFraction = from.m_balanceForceFraction;
            to.m_damping = from.m_damping;
            if (impactEffects)
                to.m_impactEffects = from.m_impactEffects;
        }

        private static Floating? Template()
        {
            if (_template != null)
                return _template;
            var db = ObjectDB.instance;
            if (db == null)
                return null;

            _template = FindTemplate(db);
            if (_template != null)
            {
                MagpieRuntime.Log?.Debug($"Float template: {_template.name} (offset {_template.m_waterLevelOffset}, force {_template.m_force}, " +
                                         $"distance {_template.m_forceDistance}, damping {_template.m_damping}).");
            }
            else if (!_templateMissingLogged)
            {
                _templateMissingLogged = true;
                MagpieRuntime.Log?.Warning("No game item has a Floating component; floating items use the component defaults.");
            }
            return _template;
        }

        private static Floating? FindTemplate(ObjectDB db)
        {
            var preferred = db.GetItemPrefab(TemplateItem);
            if (preferred != null && preferred.TryGetComponent(out Floating floating))
                return floating;
            foreach (var prefab in db.m_items)
                if (prefab != null && prefab.TryGetComponent(out floating))
                    return floating;
            return null;
        }
    }
}
