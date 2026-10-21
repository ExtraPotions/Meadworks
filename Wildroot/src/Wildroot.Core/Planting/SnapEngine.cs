using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Wildroot {
 public enum SnapStyle {Intelligent,Legacy}
 /// <summary>An existing plant or pickable that a ghost can snap to, with its own grid spacing.</summary>
 public sealed class SnapTarget {
  public string Id{get;} public Vec3 Position{get;} public double Spacing{get;}
  public SnapTarget(string id,Vec3 position,double spacing){Id=id;Position=position;Spacing=spacing;}
 }
 /// <summary>World queries used by snapping. PlantsWithin returns plants and pickables in scan order.</summary>
 public interface ISnapWorld {IReadOnlyList<SnapTarget> PlantsWithin(Vec3 center,double radius);bool Collides(Vec3 position);bool HasGrowSpace(Vec3 position);}
 public sealed class SnapSettings {
  public SnapStyle Style{get;} public bool PreferCardinal{get;} public bool AltPlacement{get;} public int Rows{get;} public int Columns{get;} public bool GhostIsPlant{get;}
  public SnapSettings(SnapStyle style,bool preferCardinal,bool altPlacement,int rows,int columns,bool ghostIsPlant){Style=style;PreferCardinal=preferCardinal;AltPlacement=altPlacement;Rows=rows;Columns=columns;GhostIsPlant=ghostIsPlant;}
 }
 public sealed class SnapOutcome {
  public bool Snapped{get;} public bool Grid{get;} public Vec3 Position{get;} public GridAxes Axes{get;} public Vec3 Direction{get;}
  public SnapOutcome(bool snapped,bool grid,Vec3 position,GridAxes axes,Vec3 direction){Snapped=snapped;Grid=grid;Position=position;Axes=axes;Direction=direction;}
 }
 /// <summary>Hysteresis memory kept between frames for one placement ghost.</summary>
 public sealed class SnapState {
  internal SnapPoint? Last;internal bool HasLast;internal double LastGhostYaw;internal double? SavedYaw;internal Vec3 SavedRow,SavedColumn;
  public void Reset(){Last=null;HasLast=false;LastGhostYaw=0;SavedYaw=null;SavedRow=Vec3.Zero;SavedColumn=Vec3.Zero;}
 }
 sealed class SnapPoint {
  public Vec3 Position,Row,Column,Origin;public bool Cardinal;
  public SnapPoint(Vec3 position,Vec3 row,Vec3 column,Vec3 origin,bool cardinal){Position=position;Row=row;Column=column;Origin=origin;Cardinal=cardinal;}
 }
 /// <summary>
 /// Original implementation of PlantEasily's observable snapping rules. Grid snaps (an existing line of two plants
 /// at their spacing) win over free snaps around a single plant. Grid lines turn with the ghost yaw rounded to 90
 /// degrees; free snaps use 22.5 degree steps, or the exact pointer direction with alternate placement.
 /// </summary>
 public static class SnapEngine {
  const int MaxCollisions=8;
  public static SnapOutcome Find(Vec3 ghost,double ghostYaw,double spacing,SnapSettings settings,ISnapWorld world,SnapState state){
   if(settings==null||world==null||state==null)throw new ArgumentNullException(nameof(settings));
   if(double.IsNaN(spacing)||double.IsInfinity(spacing)||spacing<=0||double.IsNaN(ghostYaw)||double.IsInfinity(ghostYaw))throw new ArgumentException("Invalid snapping geometry");
   double fixedYaw=Math.Round(ghostYaw/90)*90;double radius=spacing+Tolerance(spacing);
   var primaries=Around(world,ghost,radius).Take(MaxCollisions).ToArray();
   var grid=TryGrid(ghost,fixedYaw,spacing,primaries,settings,world);
   if(grid!=null)return ApplyGrid(ghost,ghostYaw,fixedYaw,grid,settings,world,state);
   foreach(var primary in primaries){
    var row=(ghost-primary.Position).DirectionXZ.RotateYaw(fixedYaw)*spacing;
    if(!settings.AltPlacement){double angle=Math.Round((ghost-primary.Position).DirectionXZ.Yaw/22.5)*22.5;row=Vec3.Forward.RotateYaw(fixedYaw+angle)*spacing;}
    var points=Generate(primary.Position,row,Vec3.CrossUp(row),false,settings,world);
    if(points.Count==0)continue;
    var best=points[0];double bestDistance=(best.Position-ghost).SqrMagnitude;
    foreach(var point in points.Skip(1)){double d=(point.Position-ghost).SqrMagnitude;if(d<bestDistance){best=point;bestDistance=d;}}
    Commit(best,ghostYaw,state);
    return new SnapOutcome(true,false,best.Position,new GridAxes(Choose(best.Position,best.Row,world),Choose(best.Position,best.Column,world)),(best.Origin-best.Position).Normalized);
   }
   return new SnapOutcome(false,false,ghost,default,Vec3.Zero);
  }
  static double Tolerance(double spacing)=>Math.Max(0.005,spacing*0.01);
  static double Flat(Vec3 a,Vec3 b){double x=a.X-b.X,z=a.Z-b.Z;return Math.Sqrt(x*x+z*z);}
  /// <summary>
  /// Plants within a horizontal radius whose height differs by at most that radius. Grids are laid out horizontally,
  /// so spacing is measured on the ground plane and lines on sloped terrain are still detected.
  /// </summary>
  static IEnumerable<SnapTarget> Around(ISnapWorld world,Vec3 center,double radius)=>world.PlantsWithin(center,radius*Math.Sqrt(2)).Where(p=>Flat(p.Position,center)<=radius&&Math.Abs(p.Position.Y-center.Y)<=radius);
  static List<SnapPoint>? TryGrid(Vec3 ghost,double fixedYaw,double spacing,IReadOnlyList<SnapTarget> primaries,SnapSettings settings,ISnapWorld world){
   var ordered=primaries.Select(p=>(target:p,distance:(p.Position-ghost).SqrMagnitude,neighbours:Neighbours(p,world))).ToList();
   ordered.Sort((a,b)=>{int c=a.distance.CompareTo(b.distance);return c!=0?c:a.neighbours.Count.CompareTo(b.neighbours.Count);});
   foreach(var (target,_,neighbours) in ordered){
    int tried=0;
    foreach(var neighbour in neighbours){
     if(++tried>MaxCollisions)break;
     var direction=(neighbour.Position-target.Position).DirectionXZ;if(direction.SqrMagnitude<1e-6)continue;
     direction=direction.RotateYaw(fixedYaw);var points=Generate(target.Position,direction*spacing,Vec3.CrossUp(direction).Normalized*spacing,true,settings,world);
     if(points.Count>0)return points;
    }
   }
   return null;
  }
  static List<SnapTarget> Neighbours(SnapTarget primary,ISnapWorld world){
   // Rows planted at a Farming level are closer than the base spacing (by up to MaxSpaceReduction): the nearest plant in
   // that range gives the row's actual gap, and only plants at that gap count, so diagonals (sqrt 2 farther) never do.
   double max=primary.Spacing+Tolerance(primary.Spacing),floor=primary.Spacing*(1-FarmingPerks.MaxSpaceReduction);
   var near=Around(world,primary.Position,max).Where(n=>n.Id!=primary.Id).Select(n=>(n,d:Flat(n.Position,primary.Position))).Where(x=>x.d>=floor&&x.d<=max).ToList();
   if(near.Count==0)return new List<SnapTarget>();
   double gap=near.Min(x=>x.d),tolerance=Tolerance(gap);
   return near.Where(x=>x.d<=gap+tolerance).Select(x=>x.n).ToList();
  }
  static List<SnapPoint> Generate(Vec3 origin,Vec3 row,Vec3 column,bool gridMode,SnapSettings settings,ISnapWorld world){
   var candidates=new List<Vec3>();
   if(gridMode)candidates.AddRange(new[]{origin+row,origin-row,origin+column,origin-column,origin+row-column,origin+row+column,origin-row-column,origin-row+column});
   else if(settings.AltPlacement)candidates.AddRange(new[]{origin+row,origin-row,origin+column,origin-column});
   else for(int i=0;i<16;i++)candidates.Add(origin+row.RotateYaw(22.5*i));
   var valid=new List<SnapPoint>();bool anyCardinal=false;var rowUnit=row.Normalized;var columnUnit=column.Normalized;
   foreach(var position in candidates){
    if(world.Collides(position)||settings.GhostIsPlant&&!world.HasGrowSpace(position))continue;
    var offset=position-origin;double threshold=offset.Magnitude*0.25,alongRow=Math.Abs(Vec3.Dot(offset,rowUnit)),alongColumn=Math.Abs(Vec3.Dot(offset,columnUnit));
    bool cardinal=alongRow<threshold&&alongColumn>=threshold||alongColumn<threshold&&alongRow>=threshold;anyCardinal|=cardinal;
    valid.Add(new SnapPoint(position,row,column,origin,cardinal));
   }
   if(settings.PreferCardinal&&anyCardinal)valid.RemoveAll(p=>!p.Cardinal);
   return valid;
  }
  static double Manhattan(Vec3 position,Vec3 ghost,Vec3 row,Vec3 column){var d=position-ghost;return Math.Abs(Vec3.Dot(d,row))+Math.Abs(Vec3.Dot(d,column));}
  static SnapOutcome ApplyGrid(Vec3 ghost,double ghostYaw,double fixedYaw,List<SnapPoint> points,SnapSettings settings,ISnapWorld world,SnapState state){
   if(state.HasLast&&Math.Abs(Math.IEEERemainder(fixedYaw-state.LastGhostYaw,360))>1)state.HasLast=false;
   if(state.SavedYaw.HasValue&&Math.Abs(Math.IEEERemainder(state.SavedYaw.Value-state.LastGhostYaw,360))>1)ResetSaved(state);
   var best=points[0];double bestDistance=Manhattan(best.Position,ghost,best.Row,best.Column);
   foreach(var point in points.Skip(1)){double d=Manhattan(point.Position,ghost,point.Row,point.Column);if(d<bestDistance){best=point;bestDistance=d;}}
   if(state.HasLast&&state.Last!=null&&(state.Last.Origin-best.Origin).SqrMagnitude<1e-10){double last=Manhattan(state.Last.Position,ghost,state.Last.Row,state.Last.Column);if(!(bestDistance<=last-0.0025))best=state.Last;}
   Commit(best,ghostYaw,state);
   var axes=settings.Style==SnapStyle.Intelligent?Orient(best,settings,world,state):new GridAxes(best.Row,best.Column);
   return new SnapOutcome(true,true,best.Position,axes,(best.Origin-best.Position).Normalized);
  }
  static void Commit(SnapPoint point,double ghostYaw,SnapState state){state.Last=point;state.HasLast=true;state.LastGhostYaw=ghostYaw;if(!state.SavedYaw.HasValue)state.SavedYaw=ghostYaw;}
  static void ResetSaved(SnapState state){state.SavedYaw=null;state.SavedRow=Vec3.Zero;state.SavedColumn=Vec3.Zero;}
  static GridAxes Orient(SnapPoint point,SnapSettings settings,ISnapWorld world,SnapState state){
   var row=settings.Rows>1?Choose(point.Position,point.Row,world):point.Row;var column=settings.Columns>1?Choose(point.Position,point.Column,world):point.Column;
   if(state.SavedRow.Equals(Vec3.Zero)){state.SavedRow=row.Normalized;state.SavedColumn=column.Normalized;}
   else{
    double alignment=Math.Abs(Vec3.Dot(state.SavedRow,row.Normalized));
    if(!(alignment>0.95||alignment<0.05)){ResetSaved(state);state.SavedRow=row.Normalized;state.SavedColumn=column.Normalized;}
    else{double length=row.Magnitude;row=Choose(point.Position,state.SavedRow*length,world);column=Choose(point.Position,state.SavedColumn*length,world);}
   }
   if(!point.Cardinal&&world.Collides(point.Position+row+column))column=-column;
   return new GridAxes(row,column);
  }
  /// <summary>Keeps a direction unless its next cell is occupied and the opposite one is free.</summary>
  static Vec3 Choose(Vec3 position,Vec3 direction,ISnapWorld world){if(!world.Collides(position+direction))return direction;return world.Collides(position-direction)?direction:-direction;}
 }
}
