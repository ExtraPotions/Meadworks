// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: K06 (design 8.2). Keel provides meadworks.explore.radius 1.0 (the Sailing sea factor; Cairn
// multiplies it into its radius) and the ship panel reads meadworks.explore.current 1.0 (Cairn's computed radius), with
// the game's radius times Keel's own factor as the fallback. Contracts: Crucible main cda9696.
using System;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Sailing;
using ExtraPotions.Meadworks.Contracts;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Keel.Runtime
{
    public static class KeelExplore
    {
        /// <summary>Registers the <c>meadworks.explore.radius</c> 1.0 provider. Called once from Keel's registration.</summary>
        public static void Register(ExtraPotions.Meadworks.Crucible.ProductRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            registration.AddCapability<Func<float>>(ExploreRadiusContract.Id, ExploreRadiusContract.ContractVersion, Factor);
        }

        /// <summary>
        /// The local player's sea exploration factor: 1 + share(Exploration Radius At 100, Sailing level) while aboard a
        /// ship with Keel's Sailing active, else exactly 1. Main thread only; never throws.
        /// </summary>
        public static float Factor()
        {
            try
            {
                var settings = Plugin.Settings;
                var player = Player.m_localPlayer;
                if (!SailingPerks.Active || settings == null || player == null) return ExploreRadiusContract.NeutralFactor;
                return ExploreShare.Factor(true, Ship.GetLocalShip() != null, settings.Get(KeelSettings.Sailing.ExplorationRadiusAt100),
                    KeelSailing.Skill!.Local());
            }
            catch (Exception)
            {
                return ExploreRadiusContract.NeutralFactor;
            }
        }

        /// <summary>
        /// The radius in meters the ship panel shows: Cairn's <c>meadworks.explore.current</c> when a provider gives a valid
        /// value, else the game's radius times <see cref="Factor"/>.
        /// </summary>
        public static float PanelRadius()
        {
            Func<float>? current = null;
            try { CruciblePlugin.Services.Capabilities.TryResolve(ExploreCurrentContract.Id, ExploreCurrentContract.ContractVersion, out current); }
            catch (Exception) { current = null; }
            bool has = ExploreCurrentContract.TryRead(current, out float metres);
            var game = Minimap.instance != null ? Minimap.instance.m_exploreRadius : ExploreShare.GameRadius;
            return ExploreShare.PanelRadius(has, metres, game, has ? 1f : Factor());
        }
    }
}
