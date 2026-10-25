// Keel - ExtraPotions Meadworks ships and sailing.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Portions Copyright (C) 2026 Gerald Glasgow, GrindstoneSkills 0.15.0, GPL-3.0-or-later.
// Adapted from ValheimMods at 14d151ed8a16f7264a21efd35e19f8ac346276c1:
// GrindstoneSkills/GrindstoneSkills/Sailing/Helm.cs, HelmSpeed.cs, ShipwrightHealth.cs, SailingXp.cs, SeaExploration.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: the factors and the experience meter come from Keel.Core (SailingRules, SailingXp); levels
// through Crucible CS1 (the local skill, peers' published levels); the shipwright level is written to keel.shipwright
// and GrindstoneSkills' grindstone_shipwright is read when Keel's key is absent; while Keel's tuning runs, the shipwright
// factor is part of KeelShips' health apply (so a tuning change keeps it), and only while ShipConfig is installed does
// Ship.Awake multiply it in as GrindstoneSkills did; the exploration patch steps back when Cairn is installed (Cairn reads
// the factor through meadworks.explore.radius); every patch acts only while the Sailing module is active.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Conversion;
using ExtraPotions.Keel.Integration;
using ExtraPotions.Keel.Sailing;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>The Sailing perks in the game (design B2 to B5).</summary>
    public static class SailingPerks
    {
        private static readonly int LevelHash = SailingKeys.ShipwrightLevel.GetStableHashCode();
        private static readonly int SourceLevelHash = SailingKeys.SourceShipwrightLevel.GetStableHashCode();
        private static readonly SailingXp xp = new SailingXp();

        /// <summary>Design 8.2: while Cairn runs it owns the exploration radius (meadworks.owns.explore_radius, presence) and applies Keel's sea factor itself.</summary>
        private static readonly FeatureLink radiusOwner = new FeatureLink(FeatureOwnerContract.Id(FeatureOwnerContract.ExploreRadius));
        public static bool RadiusOwned => radiusOwner.TryOwned(ExtraPotions.Meadworks.Crucible.Plugin.IsReady ? ExtraPotions.Meadworks.Crucible.Plugin.Services.Capabilities : null, out _);

        public static bool Active => KeelModules.Active(KeelModule.Sailing) && KeelSailing.Skill != null;

        /// <summary>
        /// Who steers a ship: the game keeps the helmsman's player ID in the ship's ZDO (ShipControlls.GetUser) and the
        /// players aboard in Ship.m_players on every client, so every client that has the ship loaded finds the helmsman.
        /// </summary>
        public static Player? Helmsman(Ship ship)
        {
            var controls = ship != null ? ship.m_shipControlls : null;
            var user = controls != null ? controls.GetUser() : 0L;
            if (user == 0L || ship == null || ship.m_players == null) return null;
            foreach (var player in ship.m_players)
                if (player != null && player.GetPlayerID() == user) return player;
            return null;
        }

        /// <summary>The builder level stored on a ship: Keel's key, else GrindstoneSkills'; negative when neither is set.</summary>
        public static float StoredBuilderLevel(ZDO? zdo) =>
            zdo == null ? -1f : SourceImport.ShipwrightLevel(zdo.GetFloat(LevelHash, -1f), zdo.GetFloat(SourceLevelHash, -1f));

        /// <summary>The max health factor of this loaded ship from its builder's level; 1 while Sailing is off.</summary>
        public static float ShipwrightFactor(WearNTear wearNTear)
        {
            if (!Active || wearNTear == null || wearNTear.m_nview == null) return 1f;
            var level = StoredBuilderLevel(wearNTear.m_nview.GetZDO());
            return level < 0f ? 1f : SailingRules.ShipwrightFactor(true, Plugin.Settings!.Get(KeelSettings.Sailing.ShipHealthAt100), level);
        }

        /// <summary>Once a second on the local client: Sailing experience for the helmsman and the crew.</summary>
        public static void Tick(float dt)
        {
            var player = Player.m_localPlayer;
            var skill = KeelSailing.Skill;
            var s = Plugin.Settings;
            if (player == null || skill == null || s == null) return;
            // The meter only needs the ship, helmsman and position once a second; the timer runs every frame.
            if (!xp.Advance(dt)) return;
            var ship = Ship.GetLocalShip();
            var helmsman = ship != null ? Helmsman(ship) : null;
            var role = helmsman == null ? HelmRole.Nobody : helmsman == player ? HelmRole.Helmsman : HelmRole.Crew;
            var position = ship != null ? ship.transform.position : Vector3.zero;
            var amount = xp.Measure(ship, position.x, position.z, role, Active,
                s.Get(KeelSettings.Sailing.HelmExperiencePerKilometre), s.Get(KeelSettings.Sailing.CrewExperienceShare));
            if (amount > 0f) skill.Raise(amount);
        }

        // Shipwright (ShipwrightHealth).

        /// <summary>
        /// Player.PlacePiece instantiates the ship on the builder's client, which owns it, then calls Piece.SetCreator:
        /// the builder's level is stored on the ship's ZDO and the new ship's max health raised.
        /// </summary>
        [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
        private static class Placed
        {
            [HarmonyPostfix]
            private static void Postfix(Piece __instance, long uid)
            {
                try
                {
                    var player = Player.m_localPlayer;
                    var nview = __instance.m_nview;
                    var wearNTear = __instance.GetComponent<WearNTear>();
                    if (!Active || player == null || player.GetPlayerID() != uid || wearNTear == null || __instance.GetComponent<Ship>() == null) return;
                    if (nview == null || !nview.IsValid() || !nview.IsOwner() || StoredBuilderLevel(nview.GetZDO()) >= 0f) return;
                    nview.GetZDO().Set(LevelHash, KeelSailing.Skill!.Local());
                    if (KeelModules.StepAside.SteppedAside(KeelModule.Tuning)) wearNTear.m_health *= ShipwrightFactor(wearNTear);
                    else KeelShips.RefreshHealth(wearNTear);
                }
                catch (Exception error) { KeelShips.Warn("shipwright", error); }
            }
        }

        /// <summary>Only while ShipConfig runs the tuning: every client multiplies the stored factor in as the ship loads.</summary>
        [HarmonyPatch(typeof(Ship), nameof(Ship.Awake))]
        private static class Loaded
        {
            [HarmonyPrefix]
            private static void Prefix(Ship __instance)
            {
                if (!KeelModules.StepAside.SteppedAside(KeelModule.Tuning)) return;
                var wearNTear = __instance.GetComponent<WearNTear>();
                var nview = __instance.GetComponent<ZNetView>();
                var zdo = nview != null ? nview.GetZDO() : null;
                if (wearNTear == null || zdo == null || !Active) return;
                var level = StoredBuilderLevel(zdo);
                if (level < 0f) return;
                var factor = SailingRules.ShipwrightFactor(true, Plugin.Settings!.Get(KeelSettings.Sailing.ShipHealthAt100), level);
                if (Mathf.Approximately(factor, 1f)) return;
                wearNTear.m_health *= factor;
                if (wearNTear.m_nview != null)
                    wearNTear.m_healthPercentage = Mathf.Clamp01(zdo.GetFloat(ZDOVars.s_health, wearNTear.m_health) / wearNTear.m_health);
            }
        }

        // Helm speed (HelmSpeed).

        private struct Forces
        {
            public bool Raised;
            public float Sail;
            public float Oars;
        }

        /// <summary>
        /// The game moves a ship in Ship.CustomFixedUpdate on its ZDO owner only, and the owner is not always the helmsman:
        /// the owner reads the helmsman's level (CS1: its own skill, or the level the helmsman's client publishes). For
        /// that one call the sail and oar forces are raised, then put back, so tuning values are kept.
        /// </summary>
        [HarmonyPatch(typeof(Ship), nameof(Ship.CustomFixedUpdate))]
        private static class Physics
        {
            [HarmonyPrefix]
            private static void Prefix(Ship __instance, out Forces __state)
            {
                __state = default;
                var nview = __instance.m_nview;
                if (!Active || nview == null || !nview.IsValid() || !nview.IsOwner()) return;
                var helmsman = Helmsman(__instance);
                var speed = SailingRules.HelmSpeedFactor(true, helmsman != null, Plugin.Settings!.Get(KeelSettings.Sailing.ShipSpeedAt100),
                    helmsman != null ? KeelSailing.Skill!.Of(helmsman) : 0f);
                var factor = SailingRules.HelmForceFactor(true, speed);
                if (factor <= 1f) return;
                __state = new Forces { Raised = true, Sail = __instance.m_sailForceFactor, Oars = __instance.m_backwardForce };
                __instance.m_sailForceFactor *= factor;
                __instance.m_backwardForce *= factor;
            }

            [HarmonyFinalizer]
            private static void Finalizer(Ship __instance, Forces __state)
            {
                if (!__state.Raised) return;
                __instance.m_sailForceFactor = __state.Sail;
                __instance.m_backwardForce = __state.Oars;
            }
        }

        // Sea exploration (SeaExploration), fallback while Cairn is absent.

        [HarmonyPatch(typeof(Minimap), nameof(Minimap.UpdateExplore))]
        private static class Explore
        {
            [HarmonyPrefix]
            private static void Prefix(Minimap __instance, Player player, out float __state)
            {
                __state = -1f;
                // UpdateExplore runs every frame and reveals the map every m_exploreInterval seconds; the radius is read only then.
                if (!ExploreShare.ExploresThisFrame(__instance.m_exploreTimer, Time.deltaTime, __instance.m_exploreInterval)) return;
                if (!Active || player == null || player != Player.m_localPlayer || SailingPerks.RadiusOwned) return;
                var factor = SailingRules.RadiusFactor(true, Ship.GetLocalShip() != null, Plugin.Settings!.Get(KeelSettings.Sailing.ExplorationRadiusAt100),
                    KeelSailing.Skill!.Local());
                if (factor <= 1f) return;
                __state = __instance.m_exploreRadius;
                __instance.m_exploreRadius *= factor;
            }

            [HarmonyFinalizer]
            private static void Finalizer(Minimap __instance, float __state)
            {
                if (__state >= 0f) __instance.m_exploreRadius = __state;
            }
        }
    }
}
