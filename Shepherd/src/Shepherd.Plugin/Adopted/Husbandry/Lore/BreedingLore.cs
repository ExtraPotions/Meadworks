// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Lore/BreedingLore.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: current native pace, Shepherd C05 and existing pen-aware herd/replicated lineage readers.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Shepherd.Breeding;
using ExtraPotions.Shepherd.Runtime.Breeding;
using ExtraPotions.Shepherd.Runtime.Integration;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    public static class BreedingLore
    {
        public static string Lines(Tameable tameable)
        {
            if (!tameable.IsTamed() || tameable.m_character == null) return "";
            var zdo = tameable.m_nview.GetZDO();
            var parent = tameable.GetComponent<Procreation>();
            string lines = "";
            if (parent != null)
            {
                float level = Keeper.BestLevel(tameable.transform.position);
                long pregnant = zdo.GetLong(ZDOVars.s_pregnant);
                if (pregnant == 0) lines = ShepherdLocalization.Format("lore_love", zdo.GetInt(ZDOVars.s_lovePoints), parent.m_requiredLovePoints);
                else
                {
                    double factor = NativeBreeding.PaceActive ? BreedingPace.SpeedFactor(HusbandryBreedingSettings.BreedingSpeed.Value,
                        level, NativeHusbandry.IsContent(parent.m_nview), HusbandryBreedingSettings.ContentBonus.Value) : 1;
                    double left = parent.m_pregnancyDuration / factor - Herd.SecondsSince(pregnant);
                    lines = left > 0 ? ShepherdLocalization.Format("lore_pregnant", LoreText.Duration(left)) : ShepherdLocalization.Text("lore_due");
                }
                lines = LoreText.Add(lines, HerdHooks.Line(tameable));
            }
            return LoreText.Add(lines, LineageLines(BreedingStore.ReadLineage(new ZdoKeyStore(zdo))));
        }
        public static string LineageLines(LineageRecord? record)
        {
            if (record == null) return "";
            string text = ShepherdLocalization.Format("lore_generation", record.Generation);
            if (record.MotherName.Length > 0 || record.SireName.Length > 0)
                text = LoreText.Add(text, ShepherdLocalization.Format("lore_parents", Name(record.MotherName), Name(record.SireName)));
            return text;
        }
        static string Name(string name) => string.IsNullOrEmpty(name) ? ShepherdLocalization.Text("lore_unknown")
            : LoreText.Literal(global::Localization.instance != null ? global::Localization.instance.Localize(name) : name);
    }
}
