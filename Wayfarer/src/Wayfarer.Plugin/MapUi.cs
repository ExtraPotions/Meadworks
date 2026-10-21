using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wayfarer.Core;
namespace Wayfarer {
 /// <summary>
 /// The Wayfarer map panel on the Crucible.UI kit (PackPanel 0.12.0 look): a <see cref="KitPanel"/> under the large map,
 /// kit buttons and tabs, the game's TextMeshPro font, and gamepad focus over <see cref="PanelLayout"/>'s grid.
 /// </summary>
 internal sealed class MapUi {
  static readonly Color Ink=new Color(.92f,.88f,.76f);
  readonly Plugin p;RectTransform? root;
  string signature="";string screen="main";Category category=Category.Boat;int page;long lastWorld;
  readonly Dictionary<GridCell,Selectable> focusables=new Dictionary<GridCell,Selectable>();
  GridNavigator? navigator;GridCell? focus;
  internal MapUi(Plugin plugin){p=plugin;}
  internal void Destroy() {
   focusables.Clear();navigator=null;
   if(root)UnityEngine.Object.Destroy(root!.gameObject);root=null;signature="";
  }
  internal void Refresh() {
   var map=Minimap.instance;
   if(p.World!=lastWorld){lastWorld=p.World;Destroy();screen="main";page=0;focus=null;}
   if(!map||!map.m_largeRoot||!map.m_largeRoot.activeSelf||p.World==0){p.State.Close();if(root)root!.gameObject.SetActive(false);screen="main";signature="";focus=null;return;}
   if(PortalPickerOpen(map)){p.State.Close();if(root)root!.gameObject.SetActive(false);return;}
   if(screen=="picker"&&!p.State.PickerOpen)screen="main";
   var next=$"{screen}|{category}|{page}|{p.Language}|{p.Options.Enabled.Value}|{p.State.Armed}|{p.CanTeleport}|{string.Join(",",Enum.GetValues(typeof(Category)).Cast<Category>().Where(p.CanUse))}";
   if(!root||signature!=next){Build(map);signature=next;}else root!.gameObject.SetActive(true);
   Pad();
  }
  internal static bool PortalPickerOpen(Minimap map) {
   if(!map||!map.m_mapImageLarge)return false;
   var parent=map.m_mapImageLarge.rectTransform.parent;
   var picker=parent?parent.Find("Threshold.Destinations"):null;
   return picker&&picker.gameObject.activeInHierarchy;
  }
  void Build(Minimap map) {
   Destroy();
   var layout=screen=="picker"?PickerLayout():MainLayout();
   if(layout==null){Build(map);return;}
   root=KitPanel.Create(map.m_largeRoot.transform,"WayfarerPanel",new Vector2(PanelLayout.Width,layout.Height));
   if(!root)return;
   KitForged.SidePlate(root!); // the map side panel wears the iron-cornered plate
   root!.anchorMin=root.anchorMax=root.pivot=new Vector2(0,1);
   // Scale to fit short windows while retaining dimensions for translated labels.
   var parent=(RectTransform)root.parent;root.localScale=Vector3.one*PanelLayout.Scale(parent.rect.width,parent.rect.height,layout.Height);
   // Fixed position (user decision 2026-10-11, restated 2026-10-13): the panel cannot be moved and no position is saved.
   Position(root,PanelLayout.DefaultLeft,PanelLayout.DefaultTop);
   var title=Text(layout,"Title",p.Presentation.Title,19);
   if(KitCanvas.Heading!=null)title.font=KitCanvas.Heading;
   Text(layout,"Brand",p.Presentation.Brand,12);
   // Wayfarer's settings live in Almanac (F8 > Wayfarer); the panel keeps only travel actions.
   if(screen=="picker")Picker(layout);else Main(layout);
   RestoreFocus();
  }
  PanelScreen MainLayout() {
   var allowed=new List<string>();
   if(p.CanUse(Category.Bed))allowed.Add("Bed");
   if(p.CanUse(Category.Stones))allowed.Add("Stones");
   if(p.CanUse(Category.Map))allowed.Add("Map");
   if(p.CanAdvanced)allowed.Add("Advanced");
   return PanelLayout.Main(allowed);
  }
  Category[] Categories=>new[]{Category.Boat,Category.Grave,Category.Player}.Where(p.CanUse).ToArray();
  PanelScreen? PickerLayout() {
   var categories=Categories;
   if(categories.Length==0){p.State.Close();screen="main";signature="";return null;}
   if(!categories.Contains(category)){category=categories[0];page=0;}
   var paging=PanelLayout.Page(p.Catalog.List(category).Count,page);page=paging.Page;
   return PanelLayout.Picker(categories.Length,paging.Shown);
  }
  void Main(PanelScreen layout) {
   if(layout.Has("Bed"))Button(layout,"Bed",p.T("bed"),()=>p.Teleports.TryTeleport("bed"));
   if(layout.Has("Stones"))Button(layout,"Stones",p.T("stones"),()=>p.Teleports.TryTeleport("stones"));
   if(layout.Has("Map"))Button(layout,"Map",p.T(p.State.Armed?"armed":"anywhere"),()=>{p.State.Arm();if(p.State.Armed)p.Report("pick");});
   if(layout.Has("Advanced"))Button(layout,"Advanced",p.T("destinations"),()=>{p.State.OpenPicker();screen="picker";page=0;});
   if(layout.Has("PolicyDenied"))Text(layout,"PolicyDenied",p.T("teleportdisabled"),16);
  }
  void Picker(PanelScreen layout) {
   var categories=Categories;
   var tabs=categories.Select(c=>new KitTab{Id=c.ToString(),Caption=p.T(c.ToString().ToLowerInvariant()),Click=Guard(()=>{category=c;page=0;signature="";})}).ToList();
   var placed=KitTabs.Place(root!,tabs,category.ToString(),PanelLayout.Left,PanelLayout.Top,PanelLayout.Inner);
   for(var i=0;i<placed.Count&&i<categories.Length;i++){Focus(layout["Category"+i],placed[i]);KitTooltip.Set(placed[i].gameObject,p.T(categories[i].ToString().ToLowerInvariant()),p.T(categories[i]==Category.Boat?"boathelp":categories[i]==Category.Grave?"deathhelp":"playerhelp"));}
   Text(layout,"Help",p.T(category==Category.Boat?"boathelp":category==Category.Grave?"deathhelp":"playerhelp"),14);
   var list=p.Catalog.List(category);var paging=PanelLayout.Page(list.Count,page);
   if(layout.Has("Empty"))Text(layout,"Empty",p.T("none"),16);
   for(var i=0;i<paging.Shown;i++) {
    var index=paging.Page*PanelLayout.PageSize+i;var d=list[index];
    var label=p.T(d.LabelKey)+(string.IsNullOrEmpty(d.Name)?"":" · "+d.Name)+(d.IsLastKnown?" · "+p.T("lastknown"):"");
    if(d.Category==Category.Grave&&d.Id!="death")label+=" · "+(index+1);
    var button=Button(layout,"Destination"+i,label,()=>p.Teleports.TryTeleport(d.Id));
    // Long names are ellipsized on the button; the tooltip keeps the whole label.
    if(button)KitTooltip.Set(button!.gameObject,p.T(category.ToString().ToLowerInvariant()),label+"\n"+p.T("destination_help"));
   }
   Button(layout,"Previous","‹",()=>{page=Mathf.Max(0,page-1);signature="";});
   Text(layout,"Page",$"{paging.Page+1} / {paging.Pages}",16).alignment=TextAlignmentOptions.Center;
   Button(layout,"Next","›",()=>{page=Mathf.Min(paging.Pages-1,page+1);signature="";});
   Button(layout,"Refresh",p.T("refresh"),()=>signature="");
   Button(layout,"Cancel",p.T("cancel"),()=>{p.State.Close();screen="main";signature="";});
  }

  static Vector2 At(PanelRect rect)=>new Vector2(rect.X,-rect.Y);
  static void Place(RectTransform rect,PanelRect at){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=At(at);rect.sizeDelta=new Vector2(at.Width,at.Height);}
  /// <summary>Failures are logged and reported like the 0.4.0 panel did.</summary>
  Action Guard(Action action)=>()=>{try{action();}catch(Exception e){p.Error(e);}};
  TMP_Text Text(PanelScreen layout,string name,string words,float size) {
   var text=KitCanvas.Text(root!,name,words,size,Ink,TextAlignmentOptions.MidlineLeft,true);
   text.enableAutoSizing=true;text.fontSizeMin=Mathf.Min(12,size);text.fontSizeMax=size;text.overflowMode=TextOverflowModes.Truncate;
   Place(text.rectTransform,layout[name].Rect);return text;
  }
  Button? Button(PanelScreen layout,string name,string caption,Action action) {
   var slot=layout[name];
   var button=KitButton.Create(root!,name,caption,Guard(action),At(slot.Rect),new Vector2(slot.Rect.Width,slot.Rect.Height));
   if(button){Focus(slot,button!);KitTooltip.Set(button!.gameObject,caption,ButtonHelp(name));}
   return button;
  }
  string ButtonHelp(string name) {
   switch(name) {
    case "Bed":case "Stones":return p.T("usagehelp");
    case "Map":return p.T("pick")+"\n"+p.T("usagehelp");
    case "Advanced":return p.T("cheathelp");
    case "Previous":return p.T("previous_help");
    case "Next":return p.T("next_help");
    case "Refresh":return p.T(category==Category.Boat?"boathelp":category==Category.Grave?"deathhelp":"playerhelp");
    case "Cancel":return p.T("back_help");
    default:return p.T("destination_help");
   }
  }

  // Gamepad (C07 input, KitPad rules): the D-pad moves focus over the screen's grid, skipping hidden and disabled
  // controls; A presses the focused control. B is left to the game, which closes the map and with it the panel.
  void Focus(PanelSlot slot,Selectable control){if(!slot.Focusable)return;foreach(var (x,y) in slot.Cells())focusables[new GridCell(x,y)]=control;navigator=null;}
  bool Usable(GridCell cell)=>focusables.TryGetValue(cell,out var control)&&control&&control.gameObject.activeInHierarchy&&control.IsInteractable();
  GridNavigator? Navigator() {
   if(navigator!=null||focusables.Count==0)return navigator;
   int width=focusables.Keys.Max(c=>c.X)+1,height=focusables.Keys.Max(c=>c.Y)+1;
   return navigator=new GridNavigator(width,height,Usable);
  }
  void RestoreFocus() {
   if(!focus.HasValue)return;
   if(!focusables.ContainsKey(focus.Value)){var grid=Navigator();focus=grid==null?null:KitPad.Start(grid);}
   Select();
  }
  void Select(){if(focus.HasValue&&EventSystem.current!=null&&focusables.TryGetValue(focus.Value,out var control)&&control)EventSystem.current.SetSelectedGameObject(control.gameObject);}
  void Pad() {
   if(!root||!root!.gameObject.activeInHierarchy||!MeadworksInput.GamepadActive)return;
   foreach(var button in KitPad.Polled) {
    if(button==PadButton.B||!MeadworksInput.PadDown(button))continue;
    if(KitPad.ActionOf(button)==KitPadAction.Activate) {
     if(!focus.HasValue||!Usable(focus.Value))return;
     MeadworksInput.PadConsume(button);
     if(focusables[focus.Value] is Button pressed)pressed.onClick.Invoke();
     return;
    }
    var grid=Navigator();if(grid==null)return;
    MeadworksInput.PadConsume(button);
    focus=focus.HasValue?KitPad.Next(grid,focus.Value,button):KitPad.Start(grid);
    Select();
    return;
   }
  }

  internal static void Position(RectTransform panel,float x,float y) {
   var parent=(RectTransform)panel.parent;var size=parent.rect.size;
   var scaled=Vector2.Scale(panel.sizeDelta,new Vector2(panel.localScale.x,panel.localScale.y));
   var at=PanelLayout.Position(x,y,size.x,size.y,scaled.x,scaled.y);
   panel.anchoredPosition=new Vector2(at.X,-at.Y);
  }
 }
}
