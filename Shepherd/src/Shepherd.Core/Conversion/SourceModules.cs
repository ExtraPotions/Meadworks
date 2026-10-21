// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Shepherd.Conversion
{
    public sealed class SourceModuleDecision
    {
        public bool Feeding { get; internal set; }
        public bool Husbandry { get; internal set; }
        public bool Inheritance { get; internal set; }
        public bool Containers { get; internal set; }
    }
    public static class SourceModules
    {
        public const string AutoFeedRedux = "vapok.mods.AutoFeedRedux", GrindstoneSkills = "com.GrindstoneSkills",
            EliteCreaturesReborn = "gglasgow.elitecreaturesreborn", OpenKeep = "milkyteam.openkeep";
        public static SourceModuleDecision Decide(IEnumerable<string> installed, bool? gsHusbandry, bool? ecrBreeding, bool? openKeepPets)
        {
            var ids = new HashSet<string>(installed, StringComparer.Ordinal);
            bool gs = ids.Contains(GrindstoneSkills) && gsHusbandry != false;
            return new SourceModuleDecision { Feeding = ids.Contains(AutoFeedRedux) || gs, Husbandry = gs,
                Inheritance = ids.Contains(EliteCreaturesReborn) && ecrBreeding != false,
                Containers = ids.Contains(OpenKeep) && openKeepPets != false };
        }
    }
}
