// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Bestiary.Assets;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Prefabs
{
    /// <summary>Stream-backed, process-cached rebuilt assets. Missing platform art leaves registered vanilla clones usable.</summary>
    internal static class RebuiltBundles
    {
        static readonly HashSet<string> unavailable=new HashSet<string>(StringComparer.Ordinal);
        internal static AssetBundle? Load(string name,MeadworksLog? log)
        {
            if(unavailable.Contains(name))return null;
            try{return EmbeddedBundle.Load(typeof(Plugin).Assembly,name);}
            catch(Exception error){
                unavailable.Add(name);
                log?.Warning("Bestiary rebuilt bundle "+name+" unavailable on "+Application.platform+"; its plain game-prefab fallback remains: "+error.GetType().Name+": "+error.Message);
                return null;
            }
        }
        internal static Sprite? ItemIcon(string item,MeadworksLog? log)
        {
            var art=BundleCatalog.FindItem(item);
            return art==null?null:Load(art.Bundle,log)?.LoadAsset<Sprite>(art.Icon);
        }
    }
}
