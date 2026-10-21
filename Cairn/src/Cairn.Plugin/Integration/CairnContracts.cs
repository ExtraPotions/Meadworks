// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the shared contracts Cairn provides (Crucible main cda9696). meadworks.explore.current 1.0 gives
// Keel's ship panel the radius Cairn last computed for the local player. Since Crucible 0.3.15 it also owns
// meadworks.owns.explore_radius (read by Keel) and publishes meadworks.hud.occupied (read by Ember).
using System;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Cairn.Hud;

namespace ExtraPotions.Cairn.Runtime
{
    public static class CairnContracts
    {
        /// <summary>Registers Cairn's providers. Called once from Cairn's registration.</summary>
        public static void Register(ProductRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            registration.AddCapability<Func<float>>(ExploreCurrentContract.Id, ExploreCurrentContract.ContractVersion, CairnRadius.Current);
            registration.AddCapability<Func<bool>>(WeatherDisplayCapability.Id, WeatherDisplayCapability.ContractVersion, CairnHud.ShowingWeather);
            registration.AddCapability<Func<bool>>(SleepDisplay.CapabilityId, SleepDisplay.ContractVersion, CairnHud.ShowingSleep);
            // Presence: Keel asked only whether Cairn was installed, so the answer is true whatever Cairn's own switches say.
            registration.AddCapability<Func<bool>>(FeatureOwnerContract.Id(FeatureOwnerContract.ExploreRadius), FeatureOwnerContract.ContractVersion, () => true);
            // The compass HUD's rectangle, so another HUD (Ember's sleep panel) keeps clear of it without finding it by name.
            registration.AddCapability<Func<Tuple<bool, float, float, float, float>>>(HudOccupiedContract.Id, HudOccupiedContract.ContractVersion, CairnHud.Occupied);
        }
    }
}
