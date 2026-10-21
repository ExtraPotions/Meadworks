using System;
using System.Collections.Generic;
namespace Wayfarer.Core {
 public enum Category { Bed, Stones, Map, Boat, Grave, Player }
 public sealed class Destination {
  public string Id {get;} public Category Category {get;} public string LabelKey {get;} public long WorldId {get;}
  public bool IsLastKnown {get;set;} public string Name {get;set;}="";
  public float X {get;set;} public float Y {get;set;} public float Z {get;set;}
  public Destination(string id,Category category,string labelKey,long worldId) {Id=id;Category=category;LabelKey=labelKey;WorldId=worldId;}
 }
 public static class DestinationPolicy {
  public static bool SharedPlayer(bool online,bool sharesPosition,bool isLocal)=>online&&sharesPosition&&!isLocal;
  public static bool CanSelect(Destination d,long worldId,bool advancedEnabled) => d.WorldId==worldId && (advancedEnabled || d.Category==Category.Bed || d.Category==Category.Stones || d.Category==Category.Map);
 }
 public static class LandingPolicy {
  public static bool Clear(bool areaReady,float normalY,bool clear,bool water)=>areaReady&&clear&&(water||normalY>=.65f);
  public static float CorrectHeight(float supplied,float floor,float water)=>Math.Max(floor,water)+0.6f;
  public static bool IsValid(float x,float y,float z) => !float.IsNaN(x)&&!float.IsNaN(y)&&!float.IsNaN(z)&&!float.IsInfinity(x)&&!float.IsInfinity(y)&&!float.IsInfinity(z)&&Math.Abs(x)<12000&&Math.Abs(z)<12000;
 }
 public sealed class SelectionState {
  public bool Armed {get;private set;} public bool PickerOpen {get;private set;} public bool Advanced {get;private set;} public long WorldId {get;private set;}
  public void ChangeWorld(long world) {if(WorldId!=world) {Close();WorldId=world;}}
  public void Arm() {Armed=!Armed;PickerOpen=false;}
  public void Close() {Armed=false;PickerOpen=false;}
  public void SetAdvanced(bool enabled) {Advanced=enabled;if(!enabled)PickerOpen=false;}
  public void SetEnabled(bool enabled) {if(!enabled)Close();}
  public void OpenPicker() {if(Advanced) {PickerOpen=true;Armed=false;}}
 }
 public sealed class ObservedDestinations {
  long world; readonly Dictionary<string,Destination> entries=new Dictionary<string,Destination>();
  public IReadOnlyCollection<Destination> Items=>entries.Values;
  public void ChangeWorld(long id) {if(world!=id){entries.Clear();world=id;}}
  public void Put(Destination d) {if(d.WorldId==world)entries[d.Id]=d;}
 }
}
