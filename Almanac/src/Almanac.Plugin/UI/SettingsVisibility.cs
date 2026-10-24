// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Almanac.Attributes;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Almanac.Runtime.UI
{
    internal static class SettingsVisibility
    {
        internal static bool Shows(SettingTier tier,bool tiered,EntryAttributes attributes,bool productPage,bool advanced,bool searchMatch,bool allEntriesAdvanced=false)
        {
            if(Presentation.Decide(attributes,advanced,searchMatch,false)!=Visibility.Visible)return false;
            if(allEntriesAdvanced&&!advanced&&!searchMatch)return false;
            if(tier==SettingTier.FileOnly)return !productPage&&advanced;
            if(tiered&&tier==SettingTier.Advanced&&!advanced&&!searchMatch)return false;
            return !productPage||tier==SettingTier.Essential||advanced||searchMatch;
        }
    }
}
