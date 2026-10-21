using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Wildroot.Runtime {
 /// <summary>Reads native Valheim facts for the portable planting rules (spacing, ghost status, snapping world).</summary>
 internal static class NativePlanting {
  static int collisionMask=-1;
  internal static int CollisionMask=>collisionMask!=-1?collisionMask:(collisionMask=LayerMask.GetMask("Default","static_solid","Default_small","piece","piece_nonsolid","item"));
  static readonly int floorMask=LayerMask.GetMask("terrain","static_solid","piece","Default");
  // Prepared layers. Plant.m_spaceMask is 0 until the game's own check runs, and then misses every collider.
  static readonly int SpaceMask=LayerMask.GetMask(GrowSpaceCheck.Layers);
  internal static Vec3 V(Vector3 v)=>new Vec3(v.x,v.y,v.z);
  internal static Vector3 U(Vec3 v)=>new Vector3((float)v.X,(float)v.Y,(float)v.Z);
  internal static bool IsPlantOrPickable(GameObject obj)=>obj&&(obj.GetComponent<Plant>()||obj.GetComponent<Pickable>());
  internal static SpacingOptions SpacingOptions(WildrootConfig settings)=>new SpacingOptions(settings.Get("Grid.MinimizeGridSpacing",false),settings.Get("Grid.ExtraCropSpacing",0f),settings.Get("Grid.ExtraSaplingSpacing",0f));
  /// <summary>Configured pickable grid spacing: the named PlantEasily entry, else the piece harvest radius, else the default.</summary>
  internal static double PickableSpacing(WildrootConfig settings,GameObject obj){
   var name=BasePrefabName(obj);if(settings.Find("Spacing.Pickables."+name+" GridSpacing")?.BoxedValue is float configured)return configured;
   var piece=obj.GetComponent<Piece>();return piece&&piece.m_harvestRadius>0?piece.m_harvestRadius:settings.Get("Spacing.Pickables.DefaultGridSpacing",1f);
  }
  internal static string BasePrefabName(GameObject obj){var name=obj.name;int clone=name.IndexOf("(Clone)",StringComparison.Ordinal);return clone>=0?name.Substring(0,clone).TrimEnd():name;}
  /// <summary>Check again after a delayed reservation, ignoring only the harvested object's network identity.</summary>
  internal static bool ReplantClear(Vector3 position,string source){
   var occupants=UnityEngine.Object.FindObjectsByType<Plant>(FindObjectsSortMode.None).Cast<Component>().Concat(UnityEngine.Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None)).Where(c=>{
    if(!c||(c.transform.position-position).sqrMagnitude>=1)return false;
    var view=c.GetComponent<ZNetView>();return !view||!view.IsValid()||view.GetZDO().m_uid.ToString()!=source;
   }).Select(c=>new Point3(c.transform.position.x,c.transform.position.y,c.transform.position.z));
   return ReplantPlanner.PositionClear(new Point3(position.x,position.y,position.z),occupants,0.25);
  }
  internal static double Spacing(WildrootConfig settings,GameObject obj){
   var plant=obj.GetComponent<Plant>();
   if(!plant)return PlantSpacing.Compute(new SpacingFacts(false,false,0,0,PickableSpacing(settings,obj)),SpacingOptions(settings));
   var forms=new List<GameObject>{obj};if(plant.m_grownPrefabs!=null)forms.AddRange(plant.m_grownPrefabs.Where(g=>g));
   bool sapling=forms.Any(g=>g.GetComponent<TreeBase>());double collider=0;foreach(var form in forms)foreach(var capsule in form.GetComponentsInChildren<CapsuleCollider>())collider=Math.Max(collider,capsule.radius);
   return PlantSpacing.Compute(new SpacingFacts(true,sapling,FarmingTraits.Placement(plant).radius,collider,0),SpacingOptions(settings));
  }
  /// <summary>Placement height of a grid cell: the walkable surface closest to the root height, falling back to terrain.</summary>
  internal static Vector3 Ground(Vector3 requested){
   var hits=Physics.RaycastAll(requested+Vector3.up*4,Vector3.down,14,floorMask,QueryTriggerInteraction.Ignore).Where(h=>h.normal.y>=0.5f&&h.collider.gameObject.layer!=LayerMask.NameToLayer("ghost")).OrderBy(h=>Mathf.Abs(h.point.y-requested.y)).ToArray();
   if(hits.Length>0)return hits[0].point;
   if(Heightmap.GetHeight(requested,out var height))requested.y=height;return requested;
  }
  internal static bool Permitted(Vector3 position)=>ZNetScene.instance&&ZNetScene.instance.IsAreaReady(position)&&PrivateArea.CheckAccess(position,0,false,true)&&!Location.IsInsideNoBuildLocation(position);
  internal static bool Collides(Vector3 position)=>Physics.CheckCapsule(position,position+Vector3.up*0.1f,Mathf.Epsilon,CollisionMask);
  /// <summary>Native facts for one cell. The probe is a ghost already moved to the cell, used for roof and attachment rays.</summary>
  internal static GhostFacts Facts(Piece piece,Vector3 position,GameObject? probe){
   var plant=piece.GetComponent<Plant>();var heightmap=Heightmap.FindHeightmap(position);
   // A crop's traits as the local player will plant it: Farming grow space, biomes and tolerance (#12, #64).
   var traits=plant?FarmingTraits.Placement(plant!):default;
   var biome=heightmap?heightmap.GetBiome(position,0.02f,false):Heightmap.Biome.None;var mask=plant?traits.biome:piece.m_onlyInBiome;
   var probePlant=probe?probe!.GetComponent<Plant>():null;bool roof=false,attached=true;
   if(plant&&probePlant){roof=(bool)AccessTools.Method(typeof(Plant),"HaveRoof").Invoke(probePlant,Array.Empty<object>());if(plant.m_attachDistance>0)attached=probePlant.GetClosestAttachPosRot(out _,out _,out _);}
   var temperature=biome==Heightmap.Biome.AshLands?TemperatureRisk.Hot:biome==Heightmap.Biome.DeepNorth||biome==Heightmap.Biome.Mountain?TemperatureRisk.Cold:TemperatureRisk.None;
   bool tolerates=plant&&(temperature==TemperatureRisk.Hot?traits.heat:traits.cold);
   return new GhostFacts(heightmap!=null,Permitted(position),plant?plant.m_needCultivatedGround:piece.m_cultivatedGroundOnly,heightmap&&heightmap.IsCultivated(position),mask==Heightmap.Biome.None||(biome&mask)!=0,
    plant,!plant||GrowSpaceCheck.Clear(Physics.OverlapSphere(position,traits.radius,SpaceMask).Length),roof,plant&&plant.m_attachDistance>0,attached,temperature,tolerates,temperature!=TemperatureRisk.None&&ShieldGenerator.IsInsideShield(position),!plant&&Collides(position));
  }
  /// <summary>Snapping world backed by Unity physics; ghosts live on the ghost layer and are never targets.</summary>
  internal sealed class SnapWorld:ISnapWorld {
   readonly WildrootConfig settings;readonly Plant? ghostPlant;readonly Dictionary<int,double> spacing=new Dictionary<int,double>();
   internal SnapWorld(WildrootConfig settings,Plant? ghostPlant){this.settings=settings;this.ghostPlant=ghostPlant;}
   public IReadOnlyList<SnapTarget> PlantsWithin(Vec3 center,double radius){
    var result=new List<SnapTarget>();var seen=new HashSet<int>();
    foreach(var collider in Physics.OverlapSphere(U(center),(float)radius,CollisionMask)){
     var root=collider.transform.root;if(!seen.Add(root.GetInstanceID())||!IsPlantOrPickable(root.gameObject))continue;
     if(!spacing.TryGetValue(root.GetInstanceID(),out var value))spacing[root.GetInstanceID()]=value=Spacing(settings,root.gameObject);
     result.Add(new SnapTarget(root.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture),V(root.position),value));
    }
    return result;
   }
   public bool Collides(Vec3 position)=>NativePlanting.Collides(U(position));
   public bool HasGrowSpace(Vec3 position)=>!ghostPlant||GrowSpaceCheck.Clear(Physics.OverlapSphere(U(position),FarmingTraits.Placement(ghostPlant!).radius,SpaceMask).Length);
  }
 }
}
