using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Contracts;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Wildroot.Runtime {
 /// <summary>One grid planting layout: index 0 is the root ghost, the rest follow PlantEasily's row-major order.</summary>
 public sealed class PlantingLayout {
  public Piece Piece{get;} public Vector3[] Cells{get;} public Quaternion Rotation{get;} public GridAxes Axes{get;} public int Rows{get;} public int Columns{get;} public bool Snapped{get;} public Vector3 SnapDirection{get;}
  public GhostStatus[] Statuses{get;}
  internal PlantingLayout(Piece piece,Vector3[] cells,Quaternion rotation,GridAxes axes,int rows,int columns,bool snapped,Vector3 snapDirection){Piece=piece;Cells=cells;Rotation=rotation;Axes=axes;Rows=rows;Columns=columns;Snapped=snapped;SnapDirection=snapDirection;Statuses=new GhostStatus[cells.Length];}
 }
 /// <summary>Native ghost grid preview: one ghost per cell, snapping, status highlights, direction lines and cost labels.</summary>
 public sealed class GridPreview:IDisposable {
  readonly WildrootConfig settings;readonly PrefabRegistrar catalog;readonly PlacementController planting;
  readonly List<GameObject> ghosts=new List<GameObject>();readonly SnapState snap=new SnapState();
  GameObject? lines;LineRenderer[] renderers=Array.Empty<LineRenderer>();string poolSource="";int updateIndex;GhostStatus[] ghostStatus=Array.Empty<GhostStatus>();
  bool frozen;GameObject? highlighted;
  public PlantingLayout? Current{get;private set;}
  public IReadOnlyList<GameObject> Ghosts=>ghosts;
  internal GridPreview(WildrootConfig settings,PrefabRegistrar catalog,PlacementController planting){this.settings=settings;this.catalog=catalog;this.planting=planting;}
  internal static bool Handles(Player player,Piece? piece)=>player&&piece&&player==Player.m_localPlayer&&Plugin.Instance?.GameplayAvailable==true&&Plugin.Instance.Planting?.CanHandle(piece!)==true;
  /// <summary>
  /// The grid for a placement: the configured grid, or for a crop planted one at a time (1 x 1) the Farming row of
  /// three or five across the view (#108). Alt-place plants one. This is the single source of the planted cell count.
  /// </summary>
  internal (int rows,int columns,int max) Size()=>SizeFor(null);
  internal (int rows,int columns,int max) SizeFor(Piece? piece){
   var grid=GridLayout.Size(settings.Get("Grid.Enabled",true),settings.Get("Grid.Rows",2),settings.Get("Grid.Columns",2));
   if(piece&&grid.rows==1&&grid.columns==1){var rules=FarmingState.Rules;if(rules.Enabled&&rules.RowPlanting)grid=FarmingPerks.PlantingGrid(rules,grid,CropIndex.IsCrop(piece),AltPlacement(settings),FarmingState.Level(Player.m_localPlayer));}
   return (grid.rows,grid.columns,Math.Max(1,settings.Get("Performance.MaxConcurrentPlacements",500)));
  }
  internal int CellCount(Piece? piece){var (rows,columns,max)=SizeFor(piece);return GridLayout.CellCount(rows,columns,max);}
  static bool AltPlacement(WildrootConfig settings)=>settings.Get("Grid.ForceAltPlacement",false)||ZInput.GetButton("AltPlace")||ZInput.GetButton("JoyAltPlace")&&!ZInput.GetButton("JoyRotate");
  /// <summary>Computes cell positions. With snapping the root moves to the snap point; the caller applies it to the ghost.</summary>
  internal PlantingLayout Compute(Player player,Piece piece,Vector3 root,Quaternion rotation,bool allowSnap){
   var (rows,columns,max)=SizeFor(piece);double spacing=Math.Max(0.01,NativePlanting.Spacing(settings,piece.gameObject));double yaw=rotation.eulerAngles.y;
   GridAxes axes=default;bool snapped=false;Vector3 direction=Vector3.zero;
   if(allowSnap&&settings.Get("Grid.SnappingEnabled",true)){
    var style=string.Equals(settings.Get("Grid.GridSnappingStyle","Intelligent"),"Legacy",StringComparison.Ordinal)?SnapStyle.Legacy:SnapStyle.Intelligent;
    var outcome=SnapEngine.Find(NativePlanting.V(root),yaw,spacing,new SnapSettings(style,settings.Get("Grid.PreferCardinalSnapping",false),AltPlacement(settings),rows,columns,piece.GetComponent<Plant>()),new NativePlanting.SnapWorld(settings,piece.GetComponent<Plant>()),snap);
    if(outcome.Snapped){snapped=true;root=NativePlanting.Ground(NativePlanting.U(outcome.Position));axes=outcome.Axes;direction=NativePlanting.U(outcome.Direction);}
   }
   if(!snapped)axes=GridAxes.Default(yaw,settings.Get("Grid.GloballyAlignGridDirections",true),NativePlanting.V(root),NativePlanting.V(player.transform.position),spacing);
   var cells=GridLayout.Cells(NativePlanting.V(root),axes,rows,columns,max).Select(c=>c.Index==0?root:NativePlanting.Ground(NativePlanting.U(c.Position))).ToArray();
   return new PlantingLayout(piece,cells,rotation,axes,rows,columns,snapped,direction);
  }
  internal GhostRules Rules()=>new GhostRules(settings.UnrestrictedPlacement,settings.Get("Spacing.Pickables.PreventOverlappingPlacements",true));
  internal static bool NoPlacementCost(Player player)=>(bool)NativeFields.NoPlacementCost.GetValue(player);
  /// <summary>Cumulative material affordability; unknown with an asynchronous provider, where the transaction decides.</summary>
  internal IReadOnlyList<GhostStatus> CostStatuses(Player player,Piece piece,int cells){
   if(!catalog.Definitions.TryGetValue(NativePlanting.BasePrefabName(piece.gameObject),out var definition)||NoPlacementCost(player)||CruciblePlugin.Services.Capabilities.TryResolve<IAsyncResourceProvider>("meadworks.resources.async",new System.Version(2,0),out _))return new GhostStatus[cells];
   var provider=planting.Resources(player);var context=PlacementController.Context(player,"preview");
   return CostPreview.Statuses(definition.Costs,item=>provider.GetAvailable(context,item),cells,false);
  }
  /// <summary>Evaluates every cell, moving pooled ghosts to their cells first so roof and attachment rays are exact.</summary>
  internal void EvaluateAll(Player player,PlantingLayout layout,GameObject? rootGhost){
   var costs=CostStatuses(player,layout.Piece,layout.Cells.Length);var rules=Rules();
   for(int i=0;i<layout.Cells.Length;i++){GameObject? probe=i==0?rootGhost:i-1<ghosts.Count&&poolSource==NativePlanting.BasePrefabName(layout.Piece.gameObject)?ghosts[i-1]:null;if(probe&&i>0){probe!.transform.position=layout.Cells[i];probe.transform.rotation=layout.Rotation;}
    layout.Statuses[i]=GhostStatusRules.Evaluate(NativePlanting.Facts(layout.Piece,layout.Cells[i],probe),rules,costs[i]);}
  }
  internal static string Message(GhostStatus status)=>status switch{GhostStatus.LackResources=>"$msg_missingrequirement",GhostStatus.NotCultivated=>"$piece_plant_notcultivated",GhostStatus.WrongBiome=>"$piece_plant_wrongbiome",GhostStatus.NoSpace=>"$piece_plant_nospace",GhostStatus.NoSun=>"$piece_plant_nosun",GhostStatus.NoAttachPiece=>"$piece_plant_nowall",GhostStatus.TooHot=>"$piece_plant_toohot",GhostStatus.TooCold=>"$piece_plant_toocold",GhostStatus.NoPermission=>"$msg_privatezone",_=>"$msg_invalidplacement"};
  /// <summary>Runs after the native ghost update: snaps the root, lays out the grid and refreshes a batch of ghosts.</summary>
  internal void AfterGhostUpdate(Player player){
   var ghost=(GameObject?)NativeFields.PlacementGhost.GetValue(player);var piece=player.GetSelectedPiece();
   if(!Handles(player,piece)||!ghost){Hide(true);return;}
   if(planting.IsPlanting){Freeze();return;}
   if(frozen)Unfreeze();
   if(!ghost!.activeSelf){Hide(false);return;}
   var layout=Compute(player,piece!,ghost.transform.position,ghost.transform.rotation,true);if(layout.Snapped)ghost.transform.position=layout.Cells[0];
   Current=layout;EnsurePool(ghost,layout.Cells.Length-1);
   var costs=CostStatuses(player,piece!,layout.Cells.Length);var rules=Rules();
   var rootStatus=GhostStatusRules.Evaluate(NativePlanting.Facts(piece!,layout.Cells[0],ghost),rules,costs[0]);layout.Statuses[0]=rootStatus;Highlight(ghost,rootStatus,true,layout.Cells.Length);
   var native=(Player.PlacementStatus)NativeFields.PlacementStatus.GetValue(player);
   if(rootStatus==GhostStatus.Healthy&&settings.UnrestrictedPlacement&&GrowthFailure(native))NativeFields.PlacementStatus.SetValue(player,Player.PlacementStatus.Valid);
   int extras=layout.Cells.Length-1,batch=Math.Min(Math.Max(1,settings.Get("Performance.GhostUpdateBatchSize",20)),Math.Max(1,extras));
   for(int n=0;n<batch&&extras>0;n++){int i=updateIndex++%extras;var cell=ghosts[i];
    cell.SetActive(true);cell.transform.position=layout.Cells[i+1];cell.transform.rotation=ghost.transform.rotation;
    var status=GhostStatusRules.Evaluate(NativePlanting.Facts(piece!,layout.Cells[i+1],cell),rules,costs[i+1]);ghostStatus[i]=status;Highlight(cell,status,false,layout.Cells.Length);}
   // Extras not refreshed this frame keep their last evaluated status (PlantEasily refreshes ghosts in round-robin batches).
   for(int i=0;i<extras;i++)layout.Statuses[i+1]=ghostStatus[i];
   UpdateLines(layout,ghost.activeSelf);
  }
  static bool GrowthFailure(Player.PlacementStatus status)=>status==Player.PlacementStatus.WrongBiome||status==Player.PlacementStatus.NeedCultivated||status==Player.PlacementStatus.NeedDirt||status==Player.PlacementStatus.MoreSpace||status==Player.PlacementStatus.DeepSnow||status==Player.PlacementStatus.NoSnow;
  /// <summary>Status tint for a ghost. The root highlight is cleared before the invalid tint so neither hides the other.</summary>
  void Highlight(GameObject ghost,GhostStatus status,bool root,int cells){
   bool highlight=root&&status==GhostStatus.Healthy&&cells>1&&settings.Get("Preview.HighlightRootGhost",true);
   if(root&&highlighted&&(!highlight||highlighted!=ghost)){if(highlighted){MaterialMan.instance.ResetValue(highlighted!,ShaderProps._Color);MaterialMan.instance.ResetValue(highlighted!,ShaderProps._EmissionColor);}highlighted=null;}
   var piece=ghost.GetComponent<Piece>();if(piece)piece.SetInvalidPlacementHeightlight(status!=GhostStatus.Healthy);
   if(highlight){var color=settings.Get("Preview.RootGhostHighlightColor",Color.green);MaterialMan.instance.SetValue(ghost,ShaderProps._Color,color,false);MaterialMan.instance.SetValue(ghost,ShaderProps._EmissionColor,color*0.7f,false);highlighted=ghost;}
  }
  /// <summary>Copies the native placement ghost without running Awake on a ZNetView-less Plant: the vanilla ghost lost its ZNetView, so the copy is made inactive, given a ZNetView back and activated under m_forceDisableInit exactly as Player.SetupPlacementGhost activates the original.</summary>
  static GameObject CloneGhost(GameObject source){
   var wasActive=source.activeSelf;GameObject clone;
   source.SetActive(false);try{clone=UnityEngine.Object.Instantiate(source);}finally{source.SetActive(wasActive);}
   ZNetView.m_forceDisableInit=true;
   try{if(!clone.GetComponent<ZNetView>())clone.AddComponent<ZNetView>();clone.SetActive(true);}finally{ZNetView.m_forceDisableInit=false;}
   return clone;
  }
  void EnsurePool(GameObject source,int count){
   for(int i=0;i<ghosts.Count;i++)if(!ghosts[i]){DestroyPool();break;}
   var name=NativePlanting.BasePrefabName(source);if(poolSource!=name){DestroyPool();poolSource=name;}
   while(ghosts.Count<count){
    var clone=CloneGhost(source);
    clone.name=source.name;int layer=LayerMask.NameToLayer("ghost");foreach(var t in clone.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;ghosts.Add(clone);
   }
   if(ghostStatus.Length!=ghosts.Count)Array.Resize(ref ghostStatus,ghosts.Count);
   for(int i=0;i<ghosts.Count;i++){bool active=i<count;if(ghosts[i].activeSelf!=active)ghosts[i].SetActive(active);}
  }
  void UpdateLines(PlantingLayout layout,bool visible){
   bool show=visible&&settings.Get("Preview.ShowGridDirections",true);
   if(!show){if(lines)lines!.SetActive(false);return;}
   if(!lines){
    lines=new GameObject("WildrootGridDirections");UnityEngine.Object.DontDestroyOnLoad(lines);var material=Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m=>m.name=="Default-Line");
    renderers=new LineRenderer[3];for(int i=0;i<3;i++){var child=new GameObject("Line"+i);child.transform.SetParent(lines.transform);renderers[i]=child.AddComponent<LineRenderer>();if(material)renderers[i].material=material;renderers[i].widthMultiplier=0.025f;}
   }
   lines!.SetActive(true);
   renderers[0].startColor=settings.Get("Preview.RowStartColor",Color.blue);renderers[0].endColor=settings.Get("Preview.RowEndColor",Color.cyan);
   renderers[1].startColor=settings.Get("Preview.ColumnStartColor",Color.green);renderers[1].endColor=settings.Get("Preview.ColumnEndColor",Color.yellow);
   renderers[2].startColor=settings.Get("Preview.SnapStartColor",Color.red);renderers[2].endColor=settings.Get("Preview.SnapEndColor",Color.magenta);
   var start=layout.Cells[0]+Vector3.up*0.5f;int total=layout.Cells.Length,actualRows=(total+layout.Columns-1)/layout.Columns,actualColumns=Math.Min(layout.Columns,total);
   var row=NativePlanting.U(layout.Axes.Row);var column=NativePlanting.U(layout.Axes.Column);
   renderers[0].SetPositions(new[]{start,start+row*(actualRows-1)});renderers[1].SetPositions(new[]{start,start+column*(actualColumns-1)});
   renderers[2].gameObject.SetActive(settings.Get("Preview.ShowSnapDirection",true));renderers[2].SetPositions(new[]{start,start+layout.SnapDirection*row.magnitude});
  }
  /// <summary>Bulk planting in progress: valid ghosts stay as gray silhouettes when enabled, the rest are hidden.</summary>
  void Freeze(){
   if(frozen)return;frozen=true;var layout=Current;bool silhouettes=settings.Get("Preview.ShowGhostsDuringPlacement",true);var placed=new HashSet<int>(planting.LastGamePlan?.Placed??Array.Empty<int>());
   for(int i=0;i<ghosts.Count;i++){if(!ghosts[i].activeSelf)continue;if(silhouettes&&placed.Contains(i+1))MaterialMan.instance.SetValue(ghosts[i],ShaderProps._Color,Color.gray,false);else ghosts[i].SetActive(false);}
   if(lines)lines!.SetActive(false);_=layout;
  }
  void Unfreeze(){frozen=false;foreach(var ghost in ghosts)if(ghost){MaterialMan.instance.ResetValue(ghost,ShaderProps._Color);ghost.SetActive(false);}}
  internal void Hide(bool reset){foreach(var ghost in ghosts)if(ghost&&ghost.activeSelf)ghost.SetActive(false);if(lines)lines!.SetActive(false);if(reset){Current=null;snap.Reset();}}
  void DestroyPool(){foreach(var ghost in ghosts)if(ghost)UnityEngine.Object.Destroy(ghost);ghosts.Clear();ghostStatus=Array.Empty<GhostStatus>();poolSource="";}
  public void Dispose(){DestroyPool();if(lines)UnityEngine.Object.Destroy(lines);lines=null;Current=null;}
  [HarmonyPatch(typeof(Player),"UpdatePlacementGhost")]
  static class UpdateGhostPatch {static void Postfix(Player __instance){try{Plugin.Instance?.Preview?.AfterGhostUpdate(__instance);}catch(Exception error){Plugin.Instance?.LogPreviewError(error);}}}
  [HarmonyPatch(typeof(InventoryGui),"SetupRequirement")]
  static class CostLabelPatch {
   static void Postfix(Transform elementRoot,Piece.Requirement req,Player player){
    var plugin=Plugin.Instance;var preview=plugin?.Preview;if(preview==null||req==null||!elementRoot||!Handles(player,player?player.GetSelectedPiece():null))return;
    int cells=preview.CellCount(player!.GetSelectedPiece());if(cells<=1||!plugin!.Settings!.Get("UI.ShowCost",true))return;
    var amount=elementRoot.Find("res_amount");if(!amount)return;var text=amount.GetComponents<Component>().FirstOrDefault(c=>c&&IsText(c.GetType()));var property=text?.GetType().GetProperty("text");if(property==null)return;
    var style=plugin.Settings.Get("Preview.CostDisplayStyle","TotalCount")=="FullCost"?CostDisplayStyle.FullCost:CostDisplayStyle.TotalCount;var location=plugin.Settings.Get("Preview.CostDisplayLocation","RightSide")=="LeftSide"?CostDisplayLocation.LeftSide:CostDisplayLocation.RightSide;
    property.SetValue(text,CostLabel.Format((string)property.GetValue(text)??"",req.m_amount,cells,style,location,true));
   }
   static bool IsText(Type? type){for(;type!=null;type=type.BaseType)if(type.FullName=="TMPro.TMP_Text")return true;return false;}
  }
 }
}
