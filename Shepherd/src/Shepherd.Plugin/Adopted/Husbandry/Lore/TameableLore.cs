// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Lore/TameableLore.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Shepherd localization and millisecond content timestamp; existing breeding/lineage readers.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Shepherd.Runtime.Integration;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    public static class TameableLore
    {
        public static string Lines(Tameable tameable, bool lore)
        {
            string lines = GateLine(tameable);
            if (!lore) return lines;
            lines = LoreText.Add(lines, TamingLine(tameable));
            lines = LoreText.Add(lines, FedLine(tameable));
            lines = LoreText.Add(lines, ContentLine(tameable));
            return LoreText.Add(lines, BreedingLore.Lines(tameable));
        }
        private static string GateLine(Tameable tameable)
        {
            if (tameable.IsTamed()) return "";
            float required = TamingLevels.Required(Herd.PrefabName(tameable));
            return required > 0 && TamingSpeed.Factor(tameable) <= 0
                ? ShepherdLocalization.Format("lore_taming_gate", required, tameable.m_tamingSpeedMultiplierRange) : "";
        }
        private static string TamingLine(Tameable tameable)
        {
            if (!Herd.IsBeingTamed(tameable)) return "";
            float factor = TamingSpeed.Factor(tameable);
            return factor > 0 ? ShepherdLocalization.Format("lore_taming", LoreText.Duration(Herd.TamingLeft(tameable) / factor)) : "";
        }
        private static string FedLine(Tameable tameable)
        {
            if (tameable.m_character == null || !(tameable.IsTamed() || Herd.IsBeingTamed(tameable)) || tameable.IsHungry()) return "";
            double left = tameable.m_fedDuration - Herd.SecondsSince(tameable.m_nview.GetZDO().GetLong(ZDOVars.s_tameLastFeeding));
            return left > 0 ? ShepherdLocalization.Format("lore_fed", LoreText.Duration(left)) : "";
        }
        private static string ContentLine(Tameable tameable)
        {
            if (!tameable.IsTamed() || !NativeHusbandry.IsContent(tameable.m_nview)) return "";
            double left = tameable.m_nview.GetZDO().GetLong(Keys.ContentUntil) / 1000.0
                - Herd.Now.Ticks / (double)System.TimeSpan.TicksPerSecond;
            return left > 0 ? ShepherdLocalization.Format("lore_content", LoreText.Duration(left)) : "";
        }
    }
}
