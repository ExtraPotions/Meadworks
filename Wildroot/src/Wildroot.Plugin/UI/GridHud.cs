using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ExtraPotions.Meadworks.Crucible.UI;
namespace ExtraPotions.Wildroot.Runtime {
 public sealed class GridHud:IDisposable {
  readonly WildrootConfig settings;readonly PlacementController planting;readonly LocalizationLoader text;
  Hud? owner;RectTransform? panel,row;TMP_Text? title,body;TMP_Text[] keyText=Array.Empty<TMP_Text>();RectTransform[] keyBoxes=Array.Empty<RectTransform>();
  LayoutElement? reservation;Vector3 rowScale;Vector2 rowSize;string language="";bool gamepad;
  internal GridHud(WildrootConfig settings,PlacementController planting,LocalizationLoader text){this.settings=settings;this.planting=planting;this.text=text;}
  internal static string KeyLabel(object? value){var key=value?.ToString()??"None";return key.Replace("RightControl","Right Ctrl").Replace("LeftControl","Left Ctrl").Replace("RightArrow","→").Replace("LeftArrow","←").Replace("UpArrow","↑").Replace("DownArrow","↓").Replace("JoystickButton4","LB");}
  string Key(string id)=>KeyLabel(settings.Find(id)?.BoxedValue);
  internal void Update(){
   var hud=Hud.instance;var player=Player.m_localPlayer;var piece=player?player.GetSelectedPiece():null;
   // The equipped-items scan allocates a list; it only matters when a piece is selected that planting does not handle.
   bool Cultivator()=>player!.GetInventory().GetEquippedItems().Exists(item=>item.m_shared.m_name=="$item_cultivator");
   bool visible=CrucibleUiPlugin.Active&&settings.Active&&hud&&player&&!player.IsDead()&&!player.IsTeleporting()&&piece&&(planting.CanHandle(piece)||Cultivator())&&!Menu.IsVisible()&&!InventoryGui.IsVisible();
   if(!visible){Hide();return;}
   if(owner!=hud||language!=text.Language||gamepad!=ZInput.IsGamepadActive()||!panel){Dispose();owner=hud;Build();}
   if(!panel||!title||!body||!row||!reservation)return;panel.gameObject.SetActive(true);
   var preview=Plugin.Instance?.Preview;var (rows,columns)=preview!=null?Shape(preview.SizeFor(piece)):GridLayout.Size(settings.Get("Grid.Enabled",true),settings.Get("Grid.Rows",2),settings.Get("Grid.Columns",2));
   title.text=text.Text("grid_title");body.text=text.Format("grid_shape",columns,rows,(long)rows*columns);
   var keys=gamepad?new[]{Key("Input.GamepadModifierKey"),"←","↑","→","↓"}:new[]{Key("Input.KeyboardModifierKey"),Key("Input.DecreaseXKey"),Key("Input.IncreaseYKey"),Key("Input.IncreaseXKey"),Key("Input.DecreaseYKey")};
   bool showKeys=settings.Get("Preview.ShowHUDKeyHints",true);
   var widths=new double[showKeys?6:1];widths[0]=Math.Max(title.GetPreferredValues(title.text).x,body.GetPreferredValues(body.text).x);
   for(int i=0;i<keyText.Length;i++){keyBoxes[i].gameObject.SetActive(showKeys);keyText[i].text=keys[i];if(showKeys)widths[i+1]=keyText[i].GetPreferredValues(keys[i]).x+16;}
   var positions=GridHintLayout.Positions(widths,5);float height=32;
   Place(title.rectTransform,0,0,(float)widths[0],height/2);Place(body.rectTransform,0,height/2,(float)widths[0],height/2);
   for(int i=0;i<keyBoxes.Length&&showKeys;i++)Place(keyBoxes[i],(float)positions[i+1],0,(float)widths[i+1],height);
   reservation.minWidth=reservation.preferredWidth=(float)(positions[positions.Length-1]+widths[widths.Length-1]);reservation.minHeight=reservation.preferredHeight=height;
   LayoutRebuilder.ForceRebuildLayoutImmediate(panel);LayoutRebuilder.ForceRebuildLayoutImmediate(row);
   // Reserve the complete hint before Remove, and fit the native row when translations or bindings need more space.
   var available=((RectTransform)row.parent).rect.width+rowSize.x;var required=LayoutUtility.GetPreferredWidth(row);
   float scale=(float)GridHintLayout.Scale(available,required);row.localScale=Vector3.Scale(rowScale,new Vector3(scale,scale,1));row.sizeDelta=new Vector2(rowSize.x+(scale>0?available/scale-available:0),rowSize.y);LayoutRebuilder.ForceRebuildLayoutImmediate(row);
  }
  static (int rows,int columns) Shape((int rows,int columns,int max) size)=>(size.rows,size.columns);
  void Build(){
   var hints=KeyHints.instance?KeyHints.instance.m_buildHints:null;if(!hints)return;
   var keyboard=hints.transform.Find("Keyboard");var template=keyboard?keyboard.Find("Place"):null;
   var label=template?template.Find("Text")?.GetComponent<TMP_Text>():null;var frame=template?template.Find("key_bkg") as RectTransform:null;
   if(!template||!label||!frame)return;
   gamepad=ZInput.IsGamepadActive();row=hints.transform.Find(gamepad?"Gamepad":"Keyboard") as RectTransform;if(!row)return;
   rowScale=row.localScale;rowSize=row.sizeDelta;language=text.Language;
   panel=new GameObject("WildrootGridHUD",typeof(RectTransform),typeof(LayoutElement)).GetComponent<RectTransform>();panel.SetParent(row,false);
   panel.SetSiblingIndex(gamepad?1:template.GetSiblingIndex()+1);panel.pivot=new Vector2(0,1);reservation=panel.GetComponent<LayoutElement>();
   title=CloneLabel(label,"GridTitle");body=CloneLabel(label,"GridBody");
   keyBoxes=new RectTransform[5];keyText=new TMP_Text[5];
   for(int i=0;i<5;i++){keyBoxes[i]=UnityEngine.Object.Instantiate(frame,panel,false);keyBoxes[i].name="GridKey"+i;keyText[i]=keyBoxes[i].GetComponentInChildren<TMP_Text>();keyText[i].text="";}
  }
  TMP_Text CloneLabel(TMP_Text template,string name){var label=UnityEngine.Object.Instantiate(template,panel,false);label.name=name;label.text="";label.raycastTarget=false;return label;}
  static void Place(RectTransform rect,float x,float y,float width,float height){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
  void Hide(){if(panel)panel.gameObject.SetActive(false);RestoreRow();}
  void RestoreRow(){if(row){row.localScale=rowScale;row.sizeDelta=rowSize;LayoutRebuilder.MarkLayoutForRebuild(row);}}
  public void Dispose(){RestoreRow();if(panel){panel.gameObject.SetActive(false);UnityEngine.Object.Destroy(panel.gameObject);}panel=null;row=null;title=null;body=null;reservation=null;keyText=Array.Empty<TMP_Text>();keyBoxes=Array.Empty<RectTransform>();owner=null;language="";}
 }
}
