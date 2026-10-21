// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Items
{
    public static class UpgradeData
    {
        public static Dictionary<string,string>? Capture(IReadOnlyDictionary<string,string> source)
        {
            Dictionary<string,string>? result=null;
            foreach(var pair in source)
            {
                if(!pair.Key.StartsWith(ItemKeys.EcPrefix,StringComparison.Ordinal)
                    &&pair.Key!=ItemKeys.EnhancementLevel&&pair.Key!=ItemKeys.EnhancementCrafter)continue;
                result??=new Dictionary<string,string>(StringComparer.Ordinal);
                result.Add(pair.Key,pair.Value);
            }
            return result;
        }
    }
}
