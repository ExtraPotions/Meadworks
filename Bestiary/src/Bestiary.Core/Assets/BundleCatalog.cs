// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 (ArsenalWeapon, ArsenalArrow, XbowItem, XbowBolts,
// GreataxeItems and Kraken loot model and icon identifiers).
// Changed by expDARE, 2026: one manifest for the independently rebuilt Bestiary bundles.
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Bestiary.Assets
{
    public sealed class ItemArt
    {
        public ItemArt(string item,string bundle,string model,string icon){Item=item;Bundle=bundle;Model=model;Icon=icon;}
        public string Item { get; }
        public string Bundle { get; }
        public string Model { get; }
        public string Icon { get; }
    }

    /// <summary>Resource names refer to rebuilt output, never to the original ECP binary assets.</summary>
    public static class BundleCatalog
    {
        public static IReadOnlyList<string> Names { get; }=new[]{"ecr_cryptmimic","ecr_slinger","ecr_rimegiant","ecp_kraken","ecp_kraken_loot","ecp_skel_arsenal","ecp_crossbowman","ecp_headsman"};
        public static IReadOnlyList<ItemArt> Items { get; }=Build();
        public static string Resource(string name,string platform)=>"Bestiary.bundles."+name+"."+platform;
        public static ItemArt? FindItem(string item)=>Items.FirstOrDefault(i=>i.Item==item);

        static ItemArt[] Build()
        {
            var items=new List<ItemArt>();
            foreach(string weapon in new[]{"Dagger","Sword","Axe","Mace","Spear","Atgeir","Bow"}){
                string model="ecp_skel_"+weapon.ToLowerInvariant()+(weapon=="Bow"?"_player":"");
                items.Add(new ItemArt("ECP_Bone"+weapon,"ecp_skel_arsenal",model,model+"_icon"));
            }
            items.Add(new ItemArt("ECP_Spine","ecp_skel_arsenal","ecp_spine","ecp_spine_icon"));
            items.Add(new ItemArt("ECP_ArrowBone","ecp_skel_arsenal","ecp_skel_arrow","ecp_skel_arrow_icon"));
            items.Add(new ItemArt("ECP_BoneCrossbow","ecp_crossbowman","ecp_xbow_item_rig","ecp_xbow_crossbow_icon"));
            items.Add(new ItemArt("ECP_BoltBoneBlunt","ecp_crossbowman","ecp_xbow_bolt","ecp_xbow_bolt_icon"));
            items.Add(new ItemArt("ECP_ExecutionerGreataxe","ecp_headsman","ecp_greataxe_held","ecp_greataxe_icon"));
            items.Add(new ItemArt("ECP_ExecutionerAxehead","ecp_headsman","ecp_greataxe_axehead","ecp_greataxe_axehead_icon"));
            items.Add(new ItemArt("ECP_KrakenBeak","ecp_kraken_loot","ecp_kraken_beak","ecp_kraken_beak_icon"));
            items.Add(new ItemArt("ECP_KrakenMeat","ecp_kraken_loot","ecp_kraken_meat","ecp_kraken_meat_icon"));
            items.Add(new ItemArt("ECP_KrakenMeatCooked","ecp_kraken_loot","ecp_kraken_meat_cooked","ecp_kraken_meat_cooked_icon"));
            items.Add(new ItemArt("ECP_ShieldKraken","ecp_kraken_loot","ecp_kraken_beak_shield","ecp_kraken_beak_shield_icon"));
            return items.ToArray();
        }
    }
}
