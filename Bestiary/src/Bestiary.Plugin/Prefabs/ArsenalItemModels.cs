// Portions Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1:
// Arsenal/ArsenalItems.cs (BuildWeapon/BuildSpine), Arsenal/ArsenalLook.cs.
// Changed by expDARE, 2026: apply reference model dressing to the existing registered clone;
// keep Bestiary's config, localisation and registration adapters; unavailable art leaves the game model.
using System.Linq;
using ExtraPotions.Bestiary.Catalog;
using ExtraPotions.Bestiary.Assets;
using ExtraPotions.Bestiary.Runtime.Adopted.Arsenal;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Prefabs
{
    internal static class ArsenalItemModels
    {
        internal static bool Apply(ZNetScene scene,GameObject item,ItemDefinition definition,MeadworksLog? log)
        {
            var art=BundleCatalog.FindItem(definition.Prefab);
            if(art==null||art.Bundle!="ecp_skel_arsenal"||definition.Kind==ItemKind.Ammo)return false;
            var bundle=RebuiltBundles.Load(art.Bundle,log);
            if(bundle==null)return false;
            var slot=item.transform.Find("attach");
            var skeleton=scene.GetPrefab("Skeleton");
            var visual=skeleton!=null?skeleton.transform.Find("Visual"):null;
            if(slot==null||visual==null||!visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any())return false;
            var model=bundle.LoadAsset<GameObject>(art.Model);
            if(model==null)return false;
            var skin=ArsenalLook.Skin(visual);
            if(skin==null)return false;
            // All prerequisites are checked before removing any part of the plain fallback.
            if(definition.Kind==ItemKind.Material){
                ArsenalLook.Clear(slot);
                ArsenalLook.Strip(slot);
                ArsenalLook.Collide(slot,ArsenalLook.Wear(slot,model,skin));
            }else{
                ArsenalLook.Clear(slot,"UpgraderGlow","equiped");
                var worn=ArsenalLook.Wear(slot,model,skin);
                if(definition.Prefab=="ECP_BoneBow")ArsenalLook.String(slot,ArsenalLook.PlayerBow,skin);
                ArsenalLook.Glow(slot,ArsenalLook.Collide(slot,worn));
            }
            return true;
        }
    }
}
