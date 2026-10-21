// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Almanac.Coexistence
{
    public static class KeyConflictPolicy
    {
        public static string Describe(string path,bool hasModifiers,IEnumerable<KeyValuePair<string,string>> bindings,bool available)
        {
            if(string.IsNullOrEmpty(path))return "";
            if(!available)return "Game key bindings are unavailable; native conflicts were not checked.";
            if(!path.StartsWith("<Keyboard>/",StringComparison.OrdinalIgnoreCase))return "";
            var actions=bindings.Where(b=>string.Equals(path,b.Value,StringComparison.OrdinalIgnoreCase)
                && !b.Key.StartsWith("Joy",StringComparison.Ordinal)).Select(b=>Name(b.Key)).Distinct().OrderBy(n=>n,StringComparer.Ordinal).ToArray();
            if(actions.Length==0)return "";
            return (hasModifiers?"Main key also used by ":"Also bound to ")+string.Join(", ",actions)+
                (hasModifiers?". Possible overlap depends on modifiers and gameplay context.":". Gameplay context may prevent overlap.");
        }
        static string Name(string action)
        {
            if(action.Length==7 && action.StartsWith("Hotbar",StringComparison.Ordinal) && action[6]>='1' && action[6]<='8')return "Hotbar slot "+action[6];
            return action;
        }
    }
}
