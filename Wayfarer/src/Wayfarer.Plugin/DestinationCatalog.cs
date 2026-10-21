using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Wayfarer.Core;
namespace Wayfarer {
 internal sealed class DestinationCatalog {
  readonly Plugin plugin; readonly ObservedDestinations boats=new ObservedDestinations();
  readonly Dictionary<string,Ship> liveBoats=new Dictionary<string,Ship>();
  internal DestinationCatalog(Plugin plugin){this.plugin=plugin;}
  static Vector3 Position(Destination d)=>new Vector3(d.X,d.Y,d.Z);
  Destination Make(string id,Category c,string key,Vector3 pos,string name="")=>new Destination(id,c,key,plugin.World){X=pos.x,Y=pos.y,Z=pos.z,Name=name};
  internal void Observe() {
   boats.ChangeWorld(plugin.World);liveBoats.Clear();if(plugin.World==0)return;
   foreach(var old in boats.Items)old.IsLastKnown=true;
   // Ship.Instances is the game's own registry of enabled ships (OnEnable/OnDisable), local and peer-owned; a scene-wide FindObjectsByType cost 20-28 ms.
   foreach(var updater in Ship.Instances) {
    if(!(updater is Ship ship))continue;
    var view=ship.GetComponent<ZNetView>();if(!view || !view.IsValid())continue;
    var id="boat:"+view.GetZDO().m_uid;var d=Make(id,Category.Boat,"boat",ship.transform.position,ship.name.Replace("(Clone)",""));
    boats.Put(d);liveBoats[id]=ship;
   }
  }
  internal List<Destination> List(Category category) {
   if(!plugin.CanUse(category) || plugin.World==0)return new List<Destination>();
   if(category==Category.Boat){Observe();return boats.Items.OrderBy(d=>d.Id).ToList();}
   var result=new List<Destination>();var profile=Game.instance?.GetPlayerProfile();
   if(category==Category.Grave) {
    if(profile!=null&&profile.HaveDeathPoint())result.Add(Make("death",category,"death",profile.GetDeathPoint()));
    if(Player.m_localPlayer)foreach(var stone in UnityEngine.Object.FindObjectsByType<TombStone>(FindObjectsSortMode.None)) {
     var view=stone.GetComponent<ZNetView>();if(!view||!view.IsValid())continue;
     var owner=AccessTools.Method(typeof(TombStone),"GetOwner")?.Invoke(stone,null);
     if(owner is long id&&id==Player.m_localPlayer.GetPlayerID())result.Add(Make("grave:"+view.GetZDO().m_uid,category,"grave",stone.transform.position));
    }
   }else if(category==Category.Player && ZNet.instance) {
    var localView=Player.m_localPlayer?Player.m_localPlayer.GetComponent<ZNetView>():null;
    var localId=localView&&localView.IsValid()?localView.GetZDO().m_uid.ToString():"";
    foreach(var player in ZNet.instance.GetPlayerList())if(DestinationPolicy.SharedPlayer(true,player.m_publicPosition,player.m_characterID.ToString()==localId) && LandingPolicy.IsValid(player.m_position.x,player.m_position.y,player.m_position.z))
     result.Add(Make("player:"+player.m_characterID,category,"player",player.m_position,player.m_name));
   }
   return result.OrderBy(d=>d.Name).ThenBy(d=>d.Id).ToList();
  }
  internal bool TryResolve(string id,out Destination? d,out string error) {
   d=null;error="unavailable";if(plugin.World==0)return false;
   var profile=Game.instance?.GetPlayerProfile();
   if(id=="bed") {error="nobed";if(profile==null||!profile.HaveCustomSpawnPoint())return false;d=Make(id,Category.Bed,"bed",profile.GetCustomSpawnPoint());}
   else if(id=="stones") {
    error="nostones";var location=(string?)AccessTools.Field(typeof(Game),"m_StartLocation")?.GetValue(Game.instance)??"StartTemple";
    if(!ZoneSystem.instance||!ZoneSystem.instance.GetLocationIcon(location,out var pos))return false;
    d=Make(id,Category.Stones,"stones",pos);
   }else {
    if(!plugin.CanAdvanced)return false;
    var category=id.StartsWith("boat:")?Category.Boat:id.StartsWith("player:")?Category.Player:Category.Grave;
    d=List(category).FirstOrDefault(item=>item.Id==id);if(d==null)return false;
   }
   if(!plugin.CanUse(d.Category)){error=plugin.Refusal(d.Category);return false;}
   return LandingPolicy.IsValid(d.X,d.Y,d.Z)&&DestinationPolicy.CanSelect(d,plugin.World,plugin.CanAdvanced);
  }
 }
}
