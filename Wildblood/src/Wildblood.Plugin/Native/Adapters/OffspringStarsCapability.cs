// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Stars;
using WildbloodPlugin = ExtraPotions.Wildblood.Runtime.Plugin;

namespace ExtraPotions.Wildblood.Adopted.Adapters
{
    /// <summary>
    /// Optional birth-star rule for Shepherd. Absent means the caller keeps the game's own level.
    /// Shape <c>Func&lt;bool&gt;</c>, true only when a new birth may inherit stars.
    /// </summary>
    public static class OffspringStarsCapability
    {
        public static void Register(ProductRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            registration.AddCapability<Func<bool>>(TamedStars.OffspringCapability, TamedStars.OffspringVersion, Allow);
        }

        /// <summary>False when settings or the server rules are not ready, so a birth does not use a local guess.</summary>
        public static bool Allow()
        {
            try
            {
                if (!WildbloodPlugin.Running || !WildbloodPlugin.NativeRulesReady || WildbloodPlugin.Settings == null) return false;
                var settings = WildbloodPlugin.Settings;
                if (!settings.Get(WildbloodSettings.General.Enabled)) return false;
                if (!settings.Get(WildbloodSettings.General.StarsEnabled)) return false;
                return TamedStars.InheritOffspring(settings.Get(WildbloodSettings.Traits.TamedStars), RuleState.Active.CreatureStars);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
