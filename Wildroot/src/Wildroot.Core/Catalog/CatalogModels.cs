using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Contracts;
namespace ExtraPotions.Wildroot {
 public enum PlantKind {Crop,Sapling,Pickable,Decorative,Vine}
 public sealed class PlantDefinition {
  public string Id{get;}public string Prefab{get;}public string GrownPrefab{get;}public PlantKind Kind{get;}public IReadOnlyList<ResourceCost> Costs{get;}public string HarvestItem{get;}public int Yield{get;}public double GrowMin{get;}public double GrowMax{get;}public double RespawnSeconds{get;}public double Radius{get;}public double ScaleMin{get;}public double ScaleMax{get;}public bool Enabled{get;}public bool Free{get;}
  public PlantDefinition(string id,string prefab,string grownPrefab,PlantKind kind,IReadOnlyList<ResourceCost> costs,string harvestItem,int yield,double growMin,double growMax,double respawnSeconds,double radius,double scaleMin,double scaleMax,bool enabled,bool free){Id=id;Prefab=prefab;GrownPrefab=grownPrefab;Kind=kind;Costs=new List<ResourceCost>(costs).AsReadOnly();HarvestItem=harvestItem;Yield=yield;GrowMin=growMin;GrowMax=growMax;RespawnSeconds=respawnSeconds;Radius=radius;ScaleMin=scaleMin;ScaleMax=scaleMax;Enabled=enabled;Free=free;}
 }
 public interface IPrefabCatalog {bool HasPrefab(string prefab);bool HasItem(string item);}
 public sealed class CatalogSnapshot {public IReadOnlyList<PlantDefinition> Entries{get;}public CatalogSnapshot(IReadOnlyList<PlantDefinition> entries){Entries=new List<PlantDefinition>(entries).AsReadOnly();}}
 public sealed class CatalogLoadResult {public CatalogSnapshot? Snapshot{get;}public IReadOnlyList<string> Errors{get;}public bool Valid=>Snapshot!=null&&Errors.Count==0;public CatalogLoadResult(CatalogSnapshot? snapshot,IReadOnlyList<string> errors){Snapshot=snapshot;Errors=new List<string>(errors).AsReadOnly();}}
 public sealed class CatalogStore {public CatalogSnapshot Current{get;private set;}public CatalogStore(CatalogSnapshot initial){Current=initial??throw new ArgumentNullException(nameof(initial));}public bool TryReplace(CatalogLoadResult candidate){if(candidate==null||!candidate.Valid)return false;Current=candidate.Snapshot!;return true;}}
 public readonly struct PlantState {public double Started{get;}public double Duration{get;}public bool Picked{get;}public bool Blocked{get;}public PlantState(double started,double duration,bool picked,bool blocked){Started=started;Duration=duration;Picked=picked;Blocked=blocked;}}
 public enum GrowthPhase {Growing,Ready,Regrowing,Blocked,Exhausted}
 public readonly struct GrowthResult {public GrowthPhase Phase{get;}public double Fraction{get;}public double Remaining{get;}public GrowthResult(GrowthPhase phase,double fraction,double remaining){Phase=phase;Fraction=fraction;Remaining=remaining;}}
}
