using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Contracts;
namespace ExtraPotions.Wildroot {
 /// <summary>Per-cell preview status. The first ten values follow PlantEasily's order; NoPermission is Wildroot's ward/no-build guard.</summary>
 public enum GhostStatus {Healthy,LackResources,NotCultivated,WrongBiome,NoSpace,NoSun,Invalid,NoAttachPiece,TooHot,TooCold,NoPermission}
 public enum TemperatureRisk {None,Hot,Cold}
 public readonly struct GhostFacts {
  public bool HasTerrain{get;} public bool Permitted{get;} public bool NeedsCultivation{get;} public bool Cultivated{get;} public bool BiomeAllowed{get;} public bool IsPlant{get;} public bool HasGrowSpace{get;} public bool HasRoof{get;} public bool NeedsAttachment{get;} public bool HasAttachment{get;} public TemperatureRisk Temperature{get;} public bool ToleratesTemperature{get;} public bool Shielded{get;} public bool Overlaps{get;}
  public GhostFacts(bool hasTerrain,bool permitted,bool needsCultivation,bool cultivated,bool biomeAllowed,bool isPlant,bool hasGrowSpace,bool hasRoof,bool needsAttachment,bool hasAttachment,TemperatureRisk temperature,bool toleratesTemperature,bool shielded,bool overlaps){HasTerrain=hasTerrain;Permitted=permitted;NeedsCultivation=needsCultivation;Cultivated=cultivated;BiomeAllowed=biomeAllowed;IsPlant=isPlant;HasGrowSpace=hasGrowSpace;HasRoof=hasRoof;NeedsAttachment=needsAttachment;HasAttachment=hasAttachment;Temperature=temperature;ToleratesTemperature=toleratesTemperature;Shielded=shielded;Overlaps=overlaps;}
 }
 /// <summary>Unrestricted removes biological checks (cultivation, biome, grow space, sun, temperature); physical checks always apply.</summary>
 public sealed class GhostRules {public bool Unrestricted{get;} public bool PreventOverlap{get;} public GhostRules(bool unrestricted,bool preventOverlap){Unrestricted=unrestricted;PreventOverlap=preventOverlap;}}
 public static class GhostStatusRules {
  /// <summary>First failing check in reference order, otherwise the supplied resource status.</summary>
  public static GhostStatus Evaluate(GhostFacts facts,GhostRules rules,GhostStatus status){
   if(rules==null)throw new ArgumentNullException(nameof(rules));bool biological=!rules.Unrestricted;
   if(!facts.HasTerrain)return GhostStatus.Invalid;
   if(!facts.Permitted)return GhostStatus.NoPermission;
   if(biological&&facts.NeedsCultivation&&!facts.Cultivated)return GhostStatus.NotCultivated;
   if(biological&&!facts.BiomeAllowed)return GhostStatus.WrongBiome;
   if(facts.IsPlant){
    if(biological&&!facts.HasGrowSpace)return GhostStatus.NoSpace;
    if(biological&&facts.HasRoof)return GhostStatus.NoSun;
    if(facts.NeedsAttachment&&!facts.HasAttachment)return GhostStatus.NoAttachPiece;
    if(biological&&facts.Temperature!=TemperatureRisk.None&&!facts.ToleratesTemperature&&!facts.Shielded)return facts.Temperature==TemperatureRisk.Hot?GhostStatus.TooHot:GhostStatus.TooCold;
   }else if(rules.PreventOverlap&&facts.Overlaps)return GhostStatus.NoSpace;
   return status;
  }
 }
 public sealed class GridPlacementPlan {
  public bool Refused{get;} public GhostStatus Reason{get;} public IReadOnlyList<int> Placed{get;}
  public GridPlacementPlan(bool refused,GhostStatus reason,IReadOnlyList<int> placed){Refused=refused;Reason=reason;Placed=new List<int>(placed).AsReadOnly();}
 }
 public static class GridPlacementRules {
  static bool Placeable(GhostStatus status,bool noPlacementCost)=>status==GhostStatus.Healthy||status==GhostStatus.LackResources&&noPlacementCost;
  /// <summary>
  /// Decides which cells of a grid are planted. Index 0 is the root ghost: it must be placeable or nothing is planted.
  /// With partial planting prevented, any unplaceable cell refuses the whole grid. Otherwise extra cells are planted
  /// when placeable, or with invalid planting allowed when they only fail a growth or target check. A cell without
  /// placement permission is never planted.
  /// </summary>
  public static GridPlacementPlan Decide(IReadOnlyList<GhostStatus> statuses,bool preventInvalid,bool preventPartial,bool noPlacementCost){
   if(statuses==null||statuses.Count==0)throw new ArgumentException("No grid cells");
   if(!Placeable(statuses[0],noPlacementCost))return new GridPlacementPlan(true,statuses[0],Array.Empty<int>());
   if(preventPartial)foreach(var status in statuses)if(!Placeable(status,noPlacementCost))return new GridPlacementPlan(true,status,Array.Empty<int>());
   var placed=new List<int>{0};
   for(int i=1;i<statuses.Count;i++){var s=statuses[i];if(s==GhostStatus.NoPermission)continue;if(Placeable(s,noPlacementCost)||!preventInvalid&&s>GhostStatus.LackResources)placed.Add(i);}
   return new GridPlacementPlan(false,GhostStatus.Healthy,placed);
  }
 }
 public static class CostPreview {
  /// <summary>Cell i is affordable when every material covers i + 1 placements; duplicate cost lines are summed.</summary>
  public static IReadOnlyList<GhostStatus> Statuses(IReadOnlyList<ResourceCost> costs,Func<string,long> available,int cells,bool noPlacementCost){
   if(costs==null||available==null||cells<0)throw new ArgumentException("Invalid cost preview");
   var totals=costs.GroupBy(c=>c.ItemId,StringComparer.Ordinal).Select(g=>(item:g.Key,amount:g.Sum(c=>(long)c.Amount),have:available(g.Key))).ToArray();
   var result=new GhostStatus[cells];
   for(int i=0;i<cells;i++)result[i]=noPlacementCost||totals.All(t=>t.have>=t.amount*(i+1))?GhostStatus.Healthy:GhostStatus.LackResources;
   return result;
  }
 }
 public enum CostDisplayStyle {TotalCount,FullCost}
 public enum CostDisplayLocation {LeftSide,RightSide}
 public static class CostLabel {
  /// <summary>Requirement amount text for a multi-cell grid: "x{cells}" or "({amount*cells})", appended or prepended.</summary>
  public static string Format(string text,int amount,int cells,CostDisplayStyle style,CostDisplayLocation location,bool show){
   if(!show||cells<=1)return text;
   string label=style==CostDisplayStyle.FullCost?"("+((long)amount*cells).ToString(System.Globalization.CultureInfo.InvariantCulture)+")":location==CostDisplayLocation.RightSide?"x"+cells.ToString(System.Globalization.CultureInfo.InvariantCulture):cells.ToString(System.Globalization.CultureInfo.InvariantCulture)+"x";
   return location==CostDisplayLocation.RightSide?text+label:label+text;
  }
 }
}
