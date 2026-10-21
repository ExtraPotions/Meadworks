using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Wildroot {
 public static class ReplantPlanner {
  static bool CanAfford(PlantDefinition plant,ReplantContext context)=>plant.Enabled&&(plant.Costs.Count!=0||plant.Free)&&plant.Costs.GroupBy(c=>c.ItemId,StringComparer.Ordinal).All(g=>context.Available(g.Key)>=g.Sum(c=>(long)c.Amount));
  /// <summary>
  /// A harvested position can be replanted only while no plant stands within the tolerance (3D distance). A second
  /// client harvesting the same crop sees the first client's replant and does not plant on top of it.
  /// </summary>
  public static bool PositionClear(Point3 target,IEnumerable<Point3> plants,double tolerance){
   if(plants==null)throw new ArgumentNullException(nameof(plants));if(double.IsNaN(tolerance)||tolerance<0)throw new ArgumentException("Invalid replant tolerance");
   foreach(var p in plants){double x=p.X-target.X,y=p.Y-target.Y,z=p.Z-target.Z;if(x*x+y*y+z*z<=tolerance*tolerance)return false;}
   return true;
  }
  public static ReplantDecision Choose(ReplantContext context,CatalogSnapshot catalog){
   if(context==null||catalog==null||context.Available==null)throw new ArgumentNullException(nameof(context));
   // A selected crop replaces the harvested one; any other selection (bush, sapling, flora) keeps the original crop.
   var selected=context.PreferSelected?catalog.Entries.FirstOrDefault(p=>p.Id==context.SelectedDefinition&&p.Kind==PlantKind.Crop):null;
   var desired=selected??catalog.Entries.FirstOrDefault(p=>p.Id==context.OriginalDefinition);
   if(desired!=null&&CanAfford(desired,context))return new ReplantDecision(desired);
   return new ReplantDecision(null);
  }
 }
}
