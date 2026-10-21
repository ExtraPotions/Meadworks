// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Wildroot {
 public enum WildrootPreset { Vanilla, MeadWorksDefault, Relaxed, Custom }
 public sealed class WildrootPresetGroup {
  public string Selector{get;}
  public string[] Members{get;}
  readonly Dictionary<WildrootPreset,IReadOnlyDictionary<string,object>> choices;
  public WildrootPresetGroup(string selector,string[] members,object[] vanilla,object[] defaults,object[] relaxed){
   Selector=selector;Members=members;
   choices=new Dictionary<WildrootPreset,IReadOnlyDictionary<string,object>>{
    [WildrootPreset.Vanilla]=Values(vanilla),[WildrootPreset.MeadWorksDefault]=Values(defaults),[WildrootPreset.Relaxed]=Values(relaxed),
   };
  }
  IReadOnlyDictionary<string,object> Values(object[] values)=>Members.Select((id,index)=>new KeyValuePair<string,object>(id,values[index])).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
  public IReadOnlyDictionary<string,object> Values(WildrootPreset preset)=>choices.TryGetValue(preset,out var values)?values:new Dictionary<string,object>();
  public bool Matches(WildrootPreset preset,Func<string,object> read)=>choices.TryGetValue(preset,out var values)&&values.All(pair=>Equals(read(pair.Key),pair.Value));
  public WildrootPreset OnLoad(WildrootPreset saved,Func<string,object> read)=>Matches(saved,read)?saved:WildrootPreset.Custom;
 }
 public static class WildrootPresets {
  public static readonly WildrootPresetGroup[] Groups={
   new WildrootPresetGroup("Grid.SizePreset",new[]{"Grid.Rows","Grid.Columns"},new object[]{1,1},new object[]{2,2},new object[]{4,4}),
   new WildrootPresetGroup("Grid.SpacingPreset",new[]{"Grid.ExtraCropSpacing","Grid.ExtraSaplingSpacing"},new object[]{0f,0f},new object[]{0f,0f},new object[]{0.5f,1f}),
   new WildrootPresetGroup("Planting.EffortPreset",new[]{"Planting.UseStamina","Planting.UseDurability","Planting.PreventPartialPlanting","Planting.PreventInvalidPlanting"},new object[]{true,true,false,true},new object[]{true,true,false,true},new object[]{false,false,false,true}),
   new WildrootPresetGroup("Farming.ProgressionPreset",new[]{"Farming.Enabled","Farming.GrowthSpeedAt100","Farming.GrowSpaceReductionAt100","Farming.BonusYieldChanceAt100","Farming.SeedReturnChanceAt100","Farming.RainGrowthBonus","Farming.RowOfThreeLevel","Farming.RowOfFiveLevel","Farming.AutoReplantLevel","Farming.HeatToleranceLevel","Farming.ColdToleranceLevel","Farming.ExperienceMultiplier","Farming.TierScaling","Farming.TierReferenceValue","Farming.TierMaximumMultiplier","Farming.DiscoveryMultiplier"},
    new object[]{false,0f,0f,25f,0f,0f,101,101,101,101,101,1f,false,30f,1f,1f},
    new object[]{true,40f,40f,50f,30f,50f,25,50,50,75,100,1f,true,30f,3f,3f},
    new object[]{true,60f,60f,75f,50f,75f,10,25,25,50,75,1.5f,true,30f,3f,3f}),
   new WildrootPresetGroup("Growth.Crops.OverridePreset",new[]{"Growth.Crops.EnableCropOverrides","Growth.Crops.CropMinScale","Growth.Crops.CropMaxScale","Growth.Crops.CropGrowTimeMin","Growth.Crops.CropGrowTimeMax","Growth.Crops.CropGrowRadius"},new object[]{false,0.9f,1.1f,4000f,5000f,0.5f},new object[]{false,0.9f,1.1f,4000f,5000f,0.5f},new object[]{true,0.9f,1.1f,2000f,2500f,0.5f}),
  };
  public static readonly string[] Retired={"Planting.EnableDebugMessages","Catalog.EnableDebugMessages","Catalog.EnableLocalization","Catalog.Language","Farm.ToggleKey"};
  public static bool CanWrite(WildrootPresetGroup group,bool remoteClient)=>!remoteClient||WildrootSettings.ScopeOf(group.Selector)==ExtraPotions.Meadworks.Crucible.ConfigScope.Local;
 }
 /// <summary>Coalesces a reload or a network amendment before applying it. Explicit member edits take precedence over a selector in the same update.</summary>
 public sealed class WildrootPresetChanges {
  readonly Dictionary<WildrootPresetGroup,bool> pending=new Dictionary<WildrootPresetGroup,bool>();
  public void Observe(string id,bool remoteClient){
   foreach(var group in WildrootPresets.Groups){
    if(!WildrootPresets.CanWrite(group,remoteClient))continue;
    bool member=Array.IndexOf(group.Members,id)>=0;
    if(id==group.Selector||member)pending[group]=member||(pending.TryGetValue(group,out var previous)&&previous);
   }
  }
  public void Clear()=>pending.Clear();
  public void Flush(bool remoteClient,Func<string,object> read,Action<WildrootPresetGroup,IReadOnlyDictionary<string,object>> write){
   var changes=pending.ToArray();pending.Clear();var failures=new List<Exception>();
   foreach(var change in changes){
    var group=change.Key;if(!WildrootPresets.CanWrite(group,remoteClient))continue;
    try{
     var preset=change.Value?WildrootPreset.Custom:(WildrootPreset)read(group.Selector);
     var values=new Dictionary<string,object>(group.Values(preset),StringComparer.Ordinal){[group.Selector]=preset};
     write(group,values);
    }catch(Exception failure){failures.Add(failure);}
   }
   if(failures.Count>0)throw new AggregateException("Wildroot preset updates failed.",failures);
  }
 }
}
