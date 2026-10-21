using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Wildroot {
 public static class HarvestPlanner {
  /// <summary>Farthest an actor may be from the primary crop a bulk harvest started on, in meters.</summary>
  public const float MaxActorReach=5f;
  /// <summary>
  /// The actor is within reach of the primary crop when its feet or its eye are within MaxActorReach. The game hovers from
  /// the eye (player reach 3.5 m to the hit point), so a crop above the player, such as a vine segment high on a wall, can be
  /// in reach from the eye while its root is farther than 5 m from the feet.
  /// </summary>
  public static bool ActorInReach(float feetToOrigin,float eyeToOrigin)=>Math.Min(feetToOrigin,eyeToOrigin)<=MaxActorReach;
  static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
  public static HarvestPlan Plan(HarvestRequest request,IReadOnlyList<HarvestableSnapshot> objects){
   if(request==null||objects==null)throw new ArgumentNullException(nameof(request));if(!Finite(request.Radius)||request.Radius<0||!Finite(request.Origin.X)||!Finite(request.Origin.Z)||!Finite(request.Height)||!Enum.IsDefined(typeof(HarvestSelection),request.Selection))throw new ArgumentException("Invalid harvest request");
   var ids=new HashSet<string>(StringComparer.Ordinal);var candidates=new List<(string id,double distance)>();
   foreach(var item in objects){if(item==null||!item.Ready||!item.Allowed||item.Beehive&&!request.IncludeBeehives||string.IsNullOrWhiteSpace(item.Id)||string.IsNullOrEmpty(item.Resource)||!Finite(item.Position.X)||!Finite(item.Position.Z)||!Finite(item.Height))continue;
    if(request.Selection==HarvestSelection.LikeResources&&item.Resource!=request.Resource)continue;double x=item.Position.X-request.Origin.X,z=item.Position.Z-request.Origin.Z,y=item.Height-request.Height,distance=x*x+y*y+z*z;if(distance<=request.Radius*request.Radius&&ids.Add(item.Id))candidates.Add((item.Id,distance));
   }
   return new HarvestPlan(candidates.OrderBy(c=>c.distance).ThenBy(c=>c.id,StringComparer.Ordinal).Select(c=>c.id).ToArray());
  }
 }
}
