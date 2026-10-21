// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/ChangedRules.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// Which groups of settings differ from their defaults, worked out once per <see cref="PatchSwitch"/> refresh (the
    /// frame after any setting change, an item database load) rather than on every call: the patch rules return these
    /// flags, and the hot hooks test one of them to skip a rule that is off.
    /// </summary>
    public static class ChangedRules
    {
        /// <summary>A food's health, stamina, duration, regen or eitr, or a global multiplier of them.</summary>
        public static bool FoodValues { get; private set; }

        /// <summary>Any Vigor: a food's own or Vigor Per Stamina Point (at their defaults every food's Vigor is 0).</summary>
        public static bool Vigor { get; private set; }

        /// <summary>Any Eitr Vigor, built like <see cref="Vigor"/>.</summary>
        public static bool EitrVigor { get; private set; }

        /// <summary>The stamina regen multiplier rules: Vigor, the regen curve, extra stamina, sneaking, blocking.</summary>
        public static bool StaminaRegenRules { get; private set; }

        /// <summary>Stamina Regen Multiplier, Low Stamina Regen Bonus or Eitr Regen Multiplier.</summary>
        public static bool RegenBasics { get; private set; }

        /// <summary>The encumbered stamina drain.</summary>
        public static bool EncumberedCost { get; private set; }

        /// <summary>Regeneration while encumbered or swimming.</summary>
        public static bool RestrictedRegen { get; private set; }

        public static void Refresh()
        {
            FoodValues = Customized.AnyFoodValues();
            Vigor = Customized.AnyFood(FeastMasterData.Vigor) || Customized.Any(Settings.VigorPerStaminaPoint);
            EitrVigor = Customized.AnyFood(FeastMasterData.EitrVigor) || Customized.Any(Settings.EitrVigorPerEitrPoint);
            StaminaRegenRules = StaminaRegenMultiplierPatch.Rules();
            RegenBasics = RegenBasicsRule.Rules();
            EncumberedCost = EncumberedCostRule.Rules();
            RestrictedRegen = RestrictedStaminaRegen.Rules();
            if (FoodValues)
                ItemValues.HookSpawns();
        }
    }
}
