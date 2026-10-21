// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Wildroot.Runtime {
 public sealed partial class WildrootConfig {
  readonly WildrootPresetChanges presetChanges=new WildrootPresetChanges();
  bool presetsReady,writingPreset,presetReloaded;
  long presetGeneration;
  SessionRole presetRole;
  void BindPresets(){
   Add("Grid.SizePreset",Bind("Grid","SizePreset",WildrootPreset.MeadWorksDefault,null,"Your grid size. Vanilla: 1 by 1; MeadWorksDefault: 2 by 2; Relaxed: 4 by 4. Custom keeps your numbers. Choosing a preset writes Rows and Columns immediately; editing either number selects Custom. Saved numbers are never replaced on load."));
   Add("Grid.SpacingPreset",Bind("Grid","SpacingPreset",WildrootPreset.MeadWorksDefault,null,"Your extra spacing between plants. Vanilla and MeadWorksDefault: no extra spacing; Relaxed: 0.5 meters for crops and 1 meter for saplings. Custom keeps your numbers. Choosing a preset applies immediately; editing either extra spacing selects Custom. Saved numbers are never replaced on load."));
   Add("Planting.EffortPreset",Bind("Planting","EffortPreset",WildrootPreset.MeadWorksDefault,null,"Host-owned cultivator effort. Vanilla and MeadWorksDefault: native stamina and durability costs, partial planting allowed, invalid extra cells skipped. Relaxed: no stamina or durability cost, with the same placement checks. Custom keeps your choices. Changes apply immediately; editing a member selects Custom. Saved choices are never replaced on load."));
   Add("Farming.ProgressionPreset",Bind("Farming","ProgressionPreset",WildrootPreset.MeadWorksDefault,null,"Host-owned Farming progression. Vanilla turns off Wildroot Farming perks and extra experience scaling. MeadWorksDefault restores Wildroot growth, yield, seed, rain, perk-level and experience defaults. Relaxed gives faster growth, more yields and seeds, earlier perks and 1.5 times experience. Custom keeps your values. Changes apply immediately; editing a member selects Custom. Saved values are never replaced on load. Your RowPlanting and AutoReplant switches are kept."));
   Add("Growth.Crops.OverridePreset",Bind("Growth.Crops","OverridePreset",WildrootPreset.MeadWorksDefault,null,"Host-owned crop overrides. Vanilla and MeadWorksDefault disable overrides and set scale to 0.9-1.1, growth to 4000-5000 seconds and clearance to 0.5 meters. Relaxed enables overrides with 2000-2500 seconds, the same scale and clearance. Custom keeps your values. Changes apply immediately; editing a member selects Custom. Saved values are never replaced on load. Growing-condition switches are kept."));
   // Adding selectors changes no numeric defaults. A legacy or mismatched group becomes Custom without touching its members.
   foreach(var group in WildrootPresets.Groups){
    var entry=(ConfigEntry<WildrootPreset>)entries[group.Selector];
    var saved=entry.Value;var loaded=group.OnLoad(saved,id=>entries[id].BoxedValue);
    if(saved!=loaded){bool save=file.SaveOnConfigSet;file.SaveOnConfigSet=false;try{entry.Value=loaded;MeadworksConfig.PersistOwnValue(entry);}finally{file.SaveOnConfigSet=save;}}
   }
   presetGeneration=CruciblePlugin.IsReady?CruciblePlugin.Session.Generation:0;presetRole=Role;presetsReady=true;
   file.ConfigReloaded+=OnPresetReloaded;
  }
  void OnPresetReloaded(object sender,EventArgs e){presetChanges.Clear();presetReloaded=true;}
  void ObservePreset(string id){
   if(!presetsReady||writingPreset)return;
   presetChanges.Observe(id,Role==SessionRole.RemoteClient);
  }
  internal void ApplyPendingPresets(){
   long generation=CruciblePlugin.IsReady?CruciblePlugin.Session.Generation:0;
   bool transitioned=generation!=presetGeneration||Role!=presetRole;
   if(transitioned){presetChanges.Clear();presetReloaded=false;presetGeneration=generation;presetRole=Role;}
   if(presetReloaded||transitioned){
    presetReloaded=false;
    foreach(var group in WildrootPresets.Groups){
     if(transitioned&&ScopeOf(group.Selector)!=ConfigScope.Local)continue;
     if(!WildrootPresets.CanWrite(group,Role==SessionRole.RemoteClient))continue;
     var current=(WildrootPreset)entries[group.Selector].BoxedValue;
     if(group.OnLoad(current,id=>entries[id].BoxedValue)!=current)presetChanges.Observe(group.Members[0],Role==SessionRole.RemoteClient);
    }
   }
   presetChanges.Flush(Role==SessionRole.RemoteClient,id=>entries[id].BoxedValue,(group,values)=>{
    if(values.All(pair=>Equals(entries[pair.Key].BoxedValue,pair.Value)))return;
    var targets=values.Keys.Select(id=>entries[id]).ToArray();
    try{
     var result=MeadworksConfig.AmendBatch(WildrootSettings.ProductId,targets,()=>{
      writingPreset=true;
      try{PresetConfigStore.Write(file,values,Find);}finally{writingPreset=false;}
     },"Wildroot");
     if(!result.Applied)throw new InvalidOperationException("Preset authority or synchronization refused "+group.Selector+".");
    }catch(Exception failure){
     // A failed write must not advertise a named choice whose members were rolled back.
     var selector=(ConfigEntry<WildrootPreset>)entries[group.Selector];
     var actual=group.OnLoad(selector.Value,id=>entries[id].BoxedValue);
     if(actual!=selector.Value){
      writingPreset=true;
      try{
       var repaired=MeadworksConfig.Amend(selector,WildrootSettings.ProductId,ScopeOf(group.Selector),actual,"Wildroot");
       if(!repaired.Applied)throw new InvalidOperationException("Preset label correction was refused.");
      }catch(Exception correction){
       if(MeadworksConfig.TryGetMetadata(selector,out var metadata)&&metadata.Decision.Allowed&&!metadata.Decision.RequiresServerAmendment){
        bool save=file.SaveOnConfigSet;file.SaveOnConfigSet=false;
        try{selector.Value=actual;}finally{file.SaveOnConfigSet=save;}
       }
       throw new AggregateException("Preset application and saved label correction failed; the own-value live label was reconciled where authorized.",failure,correction);
      }
      finally{writingPreset=false;}
     }
     throw;
    }
   });
  }
 }
}
