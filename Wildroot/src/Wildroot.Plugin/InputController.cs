using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Wildroot.Runtime {
 public sealed class InputController {
  readonly WildrootConfig settings;readonly PlacementController planting;Player? actor;long world;
  public string LastSelectedPlant{get;private set;}="";
  // PlantEasily 2.2.2 reads configured key codes through the game's native ZInput path.
  static bool IsHeld(KeyChord chord)=>Enum.TryParse<KeyCode>(chord.Main,out var main)&&main!=KeyCode.None&&ZInput.GetKey(main,false)&&chord.Modifiers.All(key=>Enum.TryParse<KeyCode>(key,out var modifier)&&modifier!=KeyCode.None&&ZInput.GetKey(modifier,false));
  internal bool OverrideGamepadInput{
   get{
    var player=Player.m_localPlayer;if(!player||player.IsDead()||player.IsTeleporting()||!settings.Active||Blocked())return false;
    var ghost=(GameObject?)NativeFields.PlacementGhost.GetValue(player);var piece=player.GetSelectedPiece();
    return ghost&&piece&&planting.CanHandle(piece)&&IsHeld(Chord(settings.Find("Input.GamepadModifierKey")?.BoxedValue));
   }
  }
  internal bool WantsBulkHarvest()=>InputRouter.AllowBulkHarvest(settings.Active&&settings.Get("Harvest.Enabled",true),Blocked(),IsHeld(Chord(settings.Find("Input.KeyboardHarvestModifierKey")?.BoxedValue)),IsHeld(Chord(settings.Find("Input.GamepadModifierKey")?.BoxedValue)));
  internal InputController(WildrootConfig settings,PlacementController planting){this.settings=settings;this.planting=planting;}
  static KeyChord Chord(object? value,string fallback="None"){if(value is KeyboardShortcut shortcut)return new KeyChord(shortcut.MainKey.ToString(),shortcut.Modifiers.Select(k=>k.ToString()).ToArray());return new KeyChord(value?.ToString()??fallback);}
  // Everything below derives from configuration only, so it is rebuilt when a setting changes, not every frame.
  sealed class Plan{public IReadOnlyDictionary<InputAction,KeyChord> Actions=null!;public KeyChord KeyboardModifier=null!;public string GamepadModifier="None";public (string Name,KeyCode Code)[] Codes=Array.Empty<(string,KeyCode)>();}
  readonly RevisionCache<Plan> plan=new RevisionCache<Plan>();
  static readonly (string Pad,string Button)[] PadAliases={("PadRight","JoyDPadRight"),("PadLeft","JoyDPadLeft"),("PadUp","JoyDPadUp"),("PadDown","JoyDPadDown"),("PadRightStick","JoyRStick"),("JoystickButton4","JoyLBumper")};
  
  Plan CurrentPlan()=>plan.Get(settings.Revision,()=>{
   var actions=new Dictionary<InputAction,KeyChord>{
    {InputAction.MasterToggle,Chord(settings.Find("General.ToggleKey")?.BoxedValue)},
    {InputAction.SnapToggle,Chord(settings.Find("Grid.SnapKey")?.BoxedValue)},
    {InputAction.GridToggle,Chord(settings.Find("Grid.ToggleKey")?.BoxedValue)},
    {InputAction.ReplantToggle,Chord(settings.Find("Harvest.ReplantToggleKey")?.BoxedValue)},
    {InputAction.ColumnsIncrease,Chord(settings.Find("Input.IncreaseXKey")?.BoxedValue)},
    {InputAction.ColumnsDecrease,Chord(settings.Find("Input.DecreaseXKey")?.BoxedValue)},
    {InputAction.RowsIncrease,Chord(settings.Find("Input.IncreaseYKey")?.BoxedValue)},
    {InputAction.RowsDecrease,Chord(settings.Find("Input.DecreaseYKey")?.BoxedValue)}};
   var keyboard=Chord(settings.Find("Input.KeyboardModifierKey")?.BoxedValue);var gamepad=Chord(settings.Find("Input.GamepadModifierKey")?.BoxedValue).Main;
   var names=actions.Values.Concat(new[]{keyboard}).SelectMany(c=>c.Modifiers.Concat(new[]{c.Main})).Concat(new[]{gamepad}).Distinct();
   var codes=new List<(string,KeyCode)>();foreach(var name in names)if(Enum.TryParse<KeyCode>(name,out var code)&&code!=KeyCode.None)codes.Add((name,code));
   return new Plan{Actions=actions,KeyboardModifier=keyboard,GamepadModifier=gamepad,Codes=codes.ToArray()};
  });
  InputBindings Bindings(){var p=CurrentPlan();return new InputBindings(p.Actions,p.KeyboardModifier,p.GamepadModifier,settings.Active);}
  // Pinned PlantEasily InputPatches.cs uses IsDown for toggles, which rejects extra held keys.
  // Grid resizing deliberately keeps its native ZInput key-code/modifier path.
  bool ToggleShortcutDown(InputAction action){
   string id=action switch{InputAction.MasterToggle=>"General.ToggleKey",InputAction.SnapToggle=>"Grid.SnapKey",InputAction.GridToggle=>"Grid.ToggleKey",InputAction.ReplantToggle=>"Harvest.ReplantToggleKey",_=>""};
   return id.Length>0&&settings.Find(id) is ConfigEntry<KeyboardShortcut> entry&&entry.Value.IsDown(); // typed read: BoxedValue boxed the struct every frame
  }
  // The viewer lookup scans every loaded type (AccessTools.TypeByName); Blocked() runs several times per frame, so resolve it once.
  static readonly ResolveOnce<Func<bool>> textViewerVisible=new(()=>{var type=AccessTools.TypeByName("TextViewer");if(type==null)return null;var method=AccessTools.Method(type,"IsVisible",Type.EmptyTypes);if(method==null)return null;const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;var property=method.IsStatic?null:type.GetProperty("instance",flags);var field=method.IsStatic||property!=null?null:type.GetField("instance",flags);return ()=>{object? instance=null;if(!method.IsStatic){instance=property!=null?property.GetValue(null):field?.GetValue(null);if(instance==null)return false;}return method.Invoke(instance,Array.Empty<object>()) is bool visible&&visible;};});
  internal static bool Blocked(){if(!MeadworksInput.Allows(InputContext.Gameplay))return true;var viewer=textViewerVisible.Get();return viewer!=null&&viewer();}
  // The master switch is an entry of the MeadWorks menu (F8, Esc > MeadWorks); General.ToggleKey is unbound by default.
  // Clicking Wildroot opens its settings, whose first row is General.Enabled; the on/off entry stays only as the fallback
  // when no settings view can open (Almanac missing), so the view does not list the same switch twice.
  const string MenuId="wildroot";IDisposable? menuEntry;
  void TickMenu()=>menuEntry??=MeadworksMenu.Register(MenuId,()=>Plugin.Instance?.Text?.Text(settings.Enabled.Value?"menu_on":"menu_off")??WildrootSettings.ProductName,ToggleEnabled,()=>Player.m_localPlayer!=null&&Plugin.Instance?.GameplayAvailable==true&&!MeadworksMenuSettings.Available(WildrootSettings.ProductId));
  internal void ReleaseMenu(){menuEntry?.Dispose();menuEntry=null;}
  // A menu choice is the player's explicit request: it is not held back by the pause menu or the chooser that made it.
  void ToggleEnabled(){MeadworksConfig.Amend(settings.Enabled,WildrootSettings.ProductId,ConfigScope.ServerAuthoritative,!settings.Enabled.Value,"Wildroot");if(!settings.Active)CancelQueuedWork();}
  readonly HashSet<string> down=new HashSet<string>(),held=new HashSet<string>();readonly HashSet<InputAction> pressedToggles=new HashSet<InputAction>();
  internal void Update(){
   TickMenu();
   var player=Player.m_localPlayer;long currentWorld=NativeWorld.CurrentUid();
   if(!player||player.IsDead()||player.IsTeleporting()||!settings.Active||player!=actor||currentWorld!=world){CancelQueuedWork();if(player!=actor||currentWorld!=world)LastSelectedPlant="";actor=player;world=currentWorld;}
   if(!player||player.IsDead()||player.IsTeleporting())return;
   // Opening the pause menu, another window or typing cancels queued work; this is the player's way to stop a batch.
   if(Blocked()){CancelQueuedWork();return;}
   // The sets are reused every frame (cleared here); nothing keeps them past this call.
   var plan=CurrentPlan();down.Clear();held.Clear();pressedToggles.Clear();
   foreach(var (key,code) in plan.Codes){if(ZInput.GetKeyDown(code,false))down.Add(key);if(ZInput.GetKey(code,false))held.Add(key);}
   foreach(var pair in PadAliases){if(GamepadMap.TryParse(pair.Button,out var button)){if(MeadworksInput.PadDown(button))down.Add(pair.Pad);if(MeadworksInput.PadHeld(button))held.Add(pair.Pad);}}
   foreach(var pair in plan.Actions)if(pair.Key<InputAction.ColumnsIncrease&&ToggleShortcutDown(pair.Key)){pressedToggles.Add(pair.Key);down.Add(pair.Value.Main);foreach(var modifier in pair.Value.Modifiers)held.Add(modifier);}
   // Preserve the existing controller modifier + right-stick snapping gesture.
   if(held.Contains(plan.GamepadModifier)&&down.Contains("PadRightStick"))pressedToggles.Add(InputAction.SnapToggle);
   var tool=PlacementController.RightItem(player);bool toolActive=tool?.m_shared.m_buildPieces&&tool.m_dropPrefab&&tool.m_dropPrefab.name=="Cultivator";
   if(toolActive){var piece=player.GetSelectedPiece();if(piece)LastSelectedPlant=piece.name;}
   // With no key down InputRouter.Resolve yields no action (Core test NoKeyDownResolvesNothing).
   if(down.Count==0)return;
   var bindings=Bindings();
   foreach(var action in InputRouter.Resolve(new InputSnapshot(down,held,0,Blocked(),toolActive&&player.GetSelectedPiece()&&planting.CanHandle(player.GetSelectedPiece()),toolActive),bindings))if(action>=InputAction.ColumnsIncrease||pressedToggles.Contains(action)){Apply(action);ConsumePad(action,bindings,down,held);}
  }
  static void ConsumePad(InputAction action,InputBindings bindings,HashSet<string> down,HashSet<string> held){
   if(!held.Contains(bindings.GamepadModifier))return;
   string alias=action switch{InputAction.ColumnsIncrease=>"PadRight",InputAction.ColumnsDecrease=>"PadLeft",InputAction.RowsIncrease=>"PadUp",InputAction.RowsDecrease=>"PadDown",InputAction.SnapToggle=>"PadRightStick",_=>""};
   if(down.Contains(alias)&&GamepadMap.TryParse(alias,out var button))MeadworksInput.PadConsume(button);
  }
  /// <summary>Stops grid planting and bulk harvesting still in progress.</summary>
  public void CancelQueuedWork(){planting.CancelAll();Plugin.Instance?.Harvesting?.CancelAll();}
  public void Apply(InputAction action){if(Blocked())return;switch(action){case InputAction.MasterToggle:ToggleEnabled();break;case InputAction.SnapToggle:Toggle("Grid.SnappingEnabled");break;case InputAction.GridToggle:Toggle("Grid.Enabled");break;case InputAction.ReplantToggle:Toggle("Harvest.Replant");break;case InputAction.ColumnsIncrease:Resize("Grid.Columns",1);break;case InputAction.ColumnsDecrease:Resize("Grid.Columns",-1);break;case InputAction.RowsIncrease:Resize("Grid.Rows",1);break;case InputAction.RowsDecrease:Resize("Grid.Rows",-1);break;}}
  void Toggle(string key){if(settings.Find(key) is ConfigEntry<bool> entry)entry.Value=!entry.Value;}
  void Resize(string key,int delta){if(settings.Find(key) is ConfigEntry<int> entry)entry.Value=GridLayout.Resize(entry.Value,delta);}
 }
}
