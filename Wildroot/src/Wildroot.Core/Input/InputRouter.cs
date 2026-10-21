using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Wildroot {
 public enum InputAction {MasterToggle,SnapToggle,GridToggle,ReplantToggle,ColumnsIncrease,ColumnsDecrease,RowsIncrease,RowsDecrease}
 public sealed class KeyChord {public string Main{get;}public IReadOnlyList<string> Modifiers{get;}public KeyChord(string main,IReadOnlyList<string>? modifiers=null){Main=main;Modifiers=new List<string>(modifiers??Array.Empty<string>()).AsReadOnly();}}
 public sealed class InputSnapshot {public IReadOnlyCollection<string> Down{get;}public IReadOnlyCollection<string> Held{get;}public int Wheel{get;}public bool Blocked{get;}public bool ToolActive{get;}public bool HoldingCultivator{get;}public InputSnapshot(IReadOnlyCollection<string> down,IReadOnlyCollection<string> held,int wheel,bool blocked,bool toolActive):this(down,held,wheel,blocked,toolActive,true){}public InputSnapshot(IReadOnlyCollection<string> down,IReadOnlyCollection<string> held,int wheel,bool blocked,bool toolActive,bool holdingCultivator){Down=down;Held=held;Wheel=wheel;Blocked=blocked;ToolActive=toolActive;HoldingCultivator=holdingCultivator;}}
 public sealed class InputBindings {public IReadOnlyDictionary<InputAction,KeyChord> Actions{get;}public KeyChord KeyboardModifier{get;}public string GamepadModifier{get;}public bool Enabled{get;}public InputBindings(IReadOnlyDictionary<InputAction,KeyChord> actions,KeyChord keyboardModifier,string gamepadModifier,bool enabled){Actions=actions;KeyboardModifier=keyboardModifier;GamepadModifier=gamepadModifier;Enabled=enabled;}}
 public static class InputRouter {
  /// <summary>Bulk harvest only while a harvest modifier is held, as in PlantEasily; a plain pick takes a single plant.</summary>
  public static bool AllowBulkHarvest(bool enabled,bool blocked,bool keyboardModifier,bool gamepadModifier)=>enabled&&!blocked&&(keyboardModifier||gamepadModifier);
  /// <summary>
  /// Whether a native Pickable/Beehive Interact(character, repeat, alt) becomes a bulk harvest. <paramref name="alt"/> is
  /// deliberately ignored: Valheim passes alt=true while AltPlace (LeftShift) or JoyAltPlace (LB) is held, and those are the
  /// default harvest modifiers, so rejecting alt made the modified press never harvest more than one plant.
  /// </summary>
  public static bool InterceptsInteract(bool wantsBulkHarvest,bool repeat,bool alt)=>wantsBulkHarvest&&!repeat;
  static bool Held(KeyChord chord,HashSet<string> held)=>chord.Main!="None"&&held.Contains(chord.Main)&&chord.Modifiers.All(held.Contains);
  static readonly InputAction[] ResizeOrder={InputAction.ColumnsIncrease,InputAction.RowsIncrease,InputAction.ColumnsDecrease,InputAction.RowsDecrease};
  static readonly string[] PadOrder={"PadRight","PadUp","PadLeft","PadDown"};
  /// <summary>
  /// Resolves one frame of input. PlantEasily applies at most one resize per frame in the order increase columns,
  /// increase rows, decrease columns, decrease rows (D-pad right, up, left, down); its toggles only work while the
  /// cultivator is held; only the master switch works with Wildroot off.
  /// </summary>
  public static IReadOnlyList<InputAction> Resolve(InputSnapshot snapshot,InputBindings bindings){
   if(snapshot==null||bindings==null||snapshot.Down==null||snapshot.Held==null)throw new ArgumentNullException(nameof(snapshot));var down=new HashSet<string>(snapshot.Down,StringComparer.OrdinalIgnoreCase);var held=new HashSet<string>(snapshot.Held,StringComparer.OrdinalIgnoreCase);var result=new HashSet<InputAction>();
   if(snapshot.Blocked)return Array.Empty<InputAction>();
   bool Pressed(KeyChord chord)=>chord.Main!="None"&&down.Contains(chord.Main)&&chord.Modifiers.All(held.Contains);
   foreach(var pair in bindings.Actions){if(pair.Key>=InputAction.ColumnsIncrease||!Pressed(pair.Value))continue;if(!bindings.Enabled&&pair.Key!=InputAction.MasterToggle)continue;if(!snapshot.HoldingCultivator)continue;result.Add(pair.Key);}
   if(bindings.Enabled&&snapshot.ToolActive){
    InputAction? resize=null;
    if(Held(bindings.KeyboardModifier,held))foreach(var action in ResizeOrder)if(bindings.Actions.TryGetValue(action,out var chord)&&Pressed(chord)){resize=action;break;}
    if(resize==null&&held.Contains(bindings.GamepadModifier))for(int i=0;i<PadOrder.Length;i++)if(down.Contains(PadOrder[i])){resize=ResizeOrder[i];break;}
    if(resize!=null)result.Add(resize.Value);
    if(held.Contains(bindings.GamepadModifier)&&down.Contains("PadRightStick"))result.Add(InputAction.SnapToggle);
   }
   return result.OrderBy(a=>a).ToArray();
  }
 }
}
