// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Uses the adopted GrindstoneSkills AnglerLog/FishInfo data and Crucible's kit ownership/input.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace ExtraPotions.Saga.Runtime {
    public static class NativeAngler {
        static KitWindow? window;static KitTileList? tiles;static KitStatSheet? sheet;static Player? player;static TMP_Text? title;static Button? close;static GameObject? selected;static string language="";static long revision=-1;static readonly List<GameObject> rows=new List<GameObject>();
        public static void Open(){if(!Plugin.AllowsUi||!NativeSaga.Active("fishing")||Player.m_localPlayer==null||InventoryGui.instance?.m_recipeElementPrefab==null)return;
            if(window?.Panel==null){window=KitWindow.Create("saga.angler_log",new Vector2(620,430));if(window==null)return;title=KitCanvas.Text(window.Panel,"Saga_angler_title",SagaText.Text("angler_log"),18,Color.white,TextAlignmentOptions.Left,false);if(KitCanvas.Heading!=null)title.font=KitCanvas.Heading;title.rectTransform.anchorMin=title.rectTransform.anchorMax=title.rectTransform.pivot=new Vector2(0,1);title.rectTransform.anchoredPosition=new Vector2(16,-12);title.rectTransform.sizeDelta=new Vector2(450,28);
                var list=KitPanel.Create(window.Panel,"Saga_angler_species",new Vector2(225,335));list.anchorMin=list.anchorMax=list.pivot=new Vector2(0,1);list.anchoredPosition=new Vector2(12,-50);var content=SheetScroll.Make(list,8,8);tiles=KitTileList.For(content,38,0);tiles.View=KitTileView.CompactList;
                sheet=KitStatSheet.Create(window.Panel,"Saga_angler_record",new Vector2(249,-50),new Vector2(355,335));close=KitButton.Create(window.Panel,"Saga_angler_close",SagaText.Text("close"),()=>window?.Close(),new Vector2(480,-392),new Vector2(125,24));window.Focusable(close,1,20);
            }
            player=Player.m_localPlayer;foreach(var row in rows){window.Unfocusable(row.GetComponent<Button>());UnityEngine.Object.Destroy(row);}rows.Clear();var species=FishInfo.Species().Where(p=>AnglerLog.Levels(player,p.name)>0).ToArray();var rects=new List<RectTransform>();int index=0;
            foreach(var prefab in species){var row=UnityEngine.Object.Instantiate(InventoryGui.instance.m_recipeElementPrefab,tiles!.Content);row.name="Saga_fish_"+prefab.name;row.SetActive(true);rows.Add(row);var rect=(RectTransform)row.transform;rects.Add(rect);var fish=prefab.GetComponent<ItemDrop>();var name=FishInfo.Localize(fish.m_itemData.m_shared.m_name);var text=row.transform.Find("name")?.GetComponent<TMP_Text>();if(text!=null)text.text=name;var image=row.transform.Find("icon")?.GetComponent<Image>();if(image!=null){image.sprite=fish.m_itemData.GetIcon();image.gameObject.SetActive(true);}foreach(var element in new[]{"Durability","QualityLevel","selected"})row.transform.Find(element)?.gameObject.SetActive(false);
                var button=row.GetComponent<Button>()??row.AddComponent<Button>();button.onClick.RemoveAllListeners();var captured=prefab;button.onClick.AddListener(()=>Show(captured));KitForged.Button(button);window.Focusable(button,0,index++);KitTooltip.Set(row,name,FishHelp(prefab));}
            tiles!.Arrange(rects);if(species.Length>0)Show(selected!=null&&species.Contains(selected)?selected:species[0]);else {selected=null;sheet!.Show(s=>s.Add(SagaText.Format("unknown_species",FishInfo.Species().Count),""));}language=SagaText.Language;revision=SagaText.Catalog.Revision;if(title!=null)title.text=SagaText.Text("angler_log");if(close!=null){var caption=close.GetComponentInChildren<TMP_Text>();if(caption!=null)caption.text=SagaText.Text("close");KitTooltip.Set(close.gameObject,SagaText.Text("close"),SagaText.Text("angler_close_help"));}window.Open();
        }
        static string FishHelp(GameObject prefab){var item=prefab.GetComponent<ItemDrop>().m_itemData;var text=SagaText.Text("angler_fish_help")+"\n"+FishInfo.Localize(item.m_shared.m_description)+"\n"+SagaText.Text("fish_levels")+": "+string.Join(", ",AnglerLog.LevelsIn(AnglerLog.Levels(player,prefab.name)));if(AnglerLog.Best(player,prefab.name,out var weight,out var level))text+="\n"+SagaText.Text("fish_record")+": "+weight.ToString("0.0")+" kg\n"+SagaText.Text("saved_level")+": "+level;return text;}
        static void Show(GameObject prefab){if(player==null||sheet==null)return;selected=prefab;sheet.Show(s=>{s.Section(FishInfo.Localize(prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name));s.Add(SagaText.Text("fish_levels"),string.Join(", ",AnglerLog.LevelsIn(AnglerLog.Levels(player,prefab.name))));if(AnglerLog.Best(player,prefab.name,out var weight,out var level)){s.Add(SagaText.Text("fish_record"),weight.ToString("0.0")+" kg");s.Add(SagaText.Text("saved_level"),level.ToString());}s.Section();s.Add(SagaText.Format("unknown_species",Math.Max(0,FishInfo.Species().Count-AnglerLog.SpeciesCount(player))),"");});}
        public static void Tick(){if(window?.IsOpen!=true)return;if(Player.m_localPlayer!=player||!NativeSaga.Active("fishing")){window.Close();return;}if(language!=SagaText.Language||revision!=SagaText.Catalog.Revision)Open();}
        public static void Shutdown(){tiles?.Forget();tiles=null;window?.Close();window?.Destroy();window=null;sheet=null;title=null;close=null;selected=null;rows.Clear();player=null;language="";revision=-1;}
    }
}
