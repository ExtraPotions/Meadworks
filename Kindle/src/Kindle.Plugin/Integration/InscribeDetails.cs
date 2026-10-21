// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using Provider=System.Tuple<System.Func<object,string>,System.Func<object,string>,System.Func<object,System.Tuple<string,string,float,string,string,string>[]>>;
namespace ExtraPotions.Kindle.Runtime;

/// <summary>Optional item decoration changes presentation only; salvage returns remain vanilla materials.</summary>
internal static class InscribeDetails
{
    static Provider Resolve(){
        if(!ExtraPotions.Meadworks.Crucible.Plugin.IsReady)return null;
        return ExtraPotions.Meadworks.Crucible.Plugin.Services.Capabilities.TryResolve<Provider>(ExtraPotions.Meadworks.Contracts.ItemDecorationContract.Id,ExtraPotions.Meadworks.Contracts.ItemDecorationContract.ContractVersion,out var provider)?provider:null;
    }
    internal static string Name(ItemDrop.ItemData item,string fallback){
        try{var value=Resolve()?.Item1(item);return string.IsNullOrEmpty(value)?fallback:value;}catch(Exception){return fallback;}
    }
    internal static string Lines(ItemDrop.ItemData item){
        try{
            var rows=Resolve()?.Item3(item);if(rows==null)return "";
            var seen=new HashSet<string>(StringComparer.Ordinal);var text=new List<string>();
            foreach(var row in rows){
                if(text.Count==128)break;
                if(row==null||row.Item1==null||!row.Item1.StartsWith("inscribe.",StringComparison.Ordinal)||!seen.Add(row.Item1)
                    ||float.IsNaN(row.Item3)||float.IsInfinity(row.Item3)||string.IsNullOrEmpty(row.Item6))continue;
                text.Add(row.Item6);
            }
            return string.Join("\n",text);
        }catch(Exception){return "";}
    }
}
