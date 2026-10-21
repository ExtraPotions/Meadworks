using System;
using System.Collections.Generic;
namespace ExtraPotions.Wildroot {
 public enum HarvestSelection {LikeResources,AllResources}
 public sealed class HarvestableSnapshot {
  public string Id{get;}public string Resource{get;}public Point2 Position{get;}public bool Ready{get;}public bool Allowed{get;}public bool Beehive{get;}
  public HarvestableSnapshot(string id,string resource,Point2 position,bool ready,bool allowed,bool beehive=false){Id=id;Resource=resource;Position=position;Ready=ready;Allowed=allowed;Beehive=beehive;}
  public double Height{get;}
  public HarvestableSnapshot(string id,string resource,Point3 position,bool ready,bool allowed,bool beehive=false):this(id,resource,new Point2(position.X,position.Z),ready,allowed,beehive){Height=position.Y;}
 }
 public sealed class HarvestRequest {public Point2 Origin{get;}public double Height{get;}public double Radius{get;}public string Resource{get;}public HarvestSelection Selection{get;}public bool IncludeBeehives{get;}public HarvestRequest(Point2 origin,double radius,string resource,HarvestSelection selection,bool includeBeehives=true){Origin=origin;Radius=radius;Resource=resource;Selection=selection;IncludeBeehives=includeBeehives;}public HarvestRequest(Point3 origin,double radius,string resource,HarvestSelection selection,bool includeBeehives=true):this(new Point2(origin.X,origin.Z),radius,resource,selection,includeBeehives){Height=origin.Y;}}
 public sealed class HarvestPlan {public IReadOnlyList<string> ObjectIds{get;}public HarvestPlan(IReadOnlyList<string> ids){ObjectIds=new List<string>(ids).AsReadOnly();}}
 public sealed class ReplantContext {public string OriginalDefinition{get;}public string SelectedDefinition{get;}public bool PreferSelected{get;}public Func<string,int> Available{get;}public ReplantContext(string originalDefinition,string selectedDefinition,bool preferSelected,Func<string,int> available){OriginalDefinition=originalDefinition;SelectedDefinition=selectedDefinition;PreferSelected=preferSelected;Available=available;}}
 public sealed class ReplantDecision {public PlantDefinition? Plant{get;}public ReplantDecision(PlantDefinition? plant){Plant=plant;}}
}
