// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Display;
using ExtraPotions.Almanac.Editors;
using ExtraPotions.Almanac.Index;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Almanac.Runtime.UI
{
    public sealed partial class SettingsWindow
    {
        AlmanacPreferences? preferences;
        TMP_Text? title;
        Button? close;
        readonly List<Button> tools=new List<Button>();
        readonly HashSet<string> filters=new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> collapsed=new HashSet<string>(StringComparer.Ordinal);
        string[] displayedKeys=Array.Empty<string>();
        float rightWidth=760,leftWidth=290;TMP_Text? measure;
        string lastSelected="";
        public string SelectedPlugin=>selected;
        public event Action<string>? ToolRequested;
        public void ConfigurePreferences(AlmanacPreferences value)
        {
            preferences=value??throw new ArgumentNullException(nameof(value));showAdvanced=value.ShowAdvanced.Value;
            if(value.RememberWindow.Value)selected=value.LastPlugin.Value;
            if(window?.Panel!=null)Reflow(window.Panel.rect.size);dirty=true;
        }
        void Preference<T>(ConfigEntry<T> entry,T value)
            =>writer.TryWrite(entry,TomlTypeConverter.ConvertToString(value,typeof(T)));
        void ConfigureCanvas()
        {
            var panel=window!.Panel;
            // ConfigDrawers owns a separate canvas at sortingOrder 9999; this changes only Almanac's own canvas.
            var canvas=panel.GetComponent<Canvas>()??panel.gameObject.AddComponent<Canvas>();
            var parent=panel.parent.GetComponentInParent<Canvas>();canvas.overrideSorting=true;
            if(parent!=null)canvas.sortingLayerID=parent.sortingLayerID;
            canvas.sortingOrder=Math.Max(10000,parent!=null?parent.sortingOrder+1:10000);
            if(panel.GetComponent<GraphicRaycaster>()==null)panel.gameObject.AddComponent<GraphicRaycaster>();
        }
        void SyncPreferences()
        {
            if(preferences==null||window?.Panel==null)return;
            if(showAdvanced!=preferences.ShowAdvanced.Value){showAdvanced=preferences.ShowAdvanced.Value;advanced?.SetValue(showAdvanced);dirty=true;}
            var group=window.Panel.GetComponent<CanvasGroup>()??window.Panel.gameObject.AddComponent<CanvasGroup>();group.alpha=preferences.WindowOpacity.Value;
            Fit();
            float desired=PaneWidth(window.Panel.rect.width);
            if(!Mathf.Approximately(leftWidth,desired))Reflow(window.Panel.rect.size);
        }
        GameObject? hiddenMainMenu;
        /// <summary>
        /// Keeps the window inside the canvas it is drawn on, with margins, at any resolution and interface scale (1100x760 is
        /// taller than a 1280x720 screen), and clear of the main menu's modded-version notice; resizes when the canvas does.
        /// </summary>
        void Fit()
        {
            var panel=window!.Panel;if(!(panel.parent is RectTransform canvas))return;
            float limit=float.PositiveInfinity;
            var notice=FejdStartup.instance!=null?FejdStartup.instance.m_moddedText:null;
            if(notice!=null&&notice.activeInHierarchy)
            {
                var words=notice.GetComponentInChildren<TMP_Text>();
                var left=words!=null&&words.textInfo!=null&&words.textInfo.characterCount>0?words.transform.TransformPoint(words.textBounds.min):Corner((RectTransform)notice.transform);
                float half=canvas.InverseTransformPoint(left).x-canvas.rect.center.x;if(half>0)limit=half;
            }
            // Window Scale enlarges or shrinks the whole panel; the layout fits the canvas divided by it so it stays on screen.
            float scale=WindowScale.Factor(preferences?.WindowScale.Value);
            var size=WindowFit.Size(canvas.rect.width/scale,canvas.rect.height/scale,limit/scale);var want=new Vector2(size.Width,size.Height);
            if(!Mathf.Approximately(panel.localScale.x,scale))panel.localScale=new Vector3(scale,scale,1f);
            if(panel.sizeDelta==want)return;
            panel.sizeDelta=want;KitPanel.Refresh(panel);Reflow(want);
        }
        static Vector3 Corner(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);return corners[0];}
        /// <summary>On the main menu the window takes the screen as the game's own Settings does: the menu column, logo and notice hide while it is open.</summary>
        void HideMainMenu()
        {
            var menu=FejdStartup.instance!=null?FejdStartup.instance.m_mainMenu:null;
            if(menu==null||!menu.activeSelf||window?.Panel==null||window.Panel.IsChildOf(menu.transform))return;
            menu.SetActive(false);hiddenMainMenu=menu;
        }
        void RestoreMainMenu(){if(hiddenMainMenu!=null)hiddenMainMenu.SetActive(true);hiddenMainMenu=null;}
        float PaneWidth(float width)=>Mathf.Clamp(preferences?.DrawerWidth.Value??300,220,Mathf.Max(220,width-430));
        void Reflow(Vector2 size)
        {
            if(window?.Panel==null||plugins==null||settings==null)return;
            leftWidth=PaneWidth(size.x);rightWidth=Mathf.Max(320,size.x-leftWidth-60);
            title!.rectTransform.sizeDelta=new Vector2(leftWidth,32);
            float start=leftWidth+40;
            // A long Close caption (German "Schließen") widens its button and moves Advanced and the search box over.
            float closeWidth=RowButtons.Width(60,AlmanacButtons.CaptionWidth(close)),extra=closeWidth-60;
            Set(search!.Rect,new Vector2(start,-16),new Vector2(Mathf.Max(100,rightWidth-220-extra),32));
            Set((RectTransform)advanced!.Button.transform,new Vector2(size.x-230-extra,-16),new Vector2(140,32));
            Set((RectTransform)close!.transform,new Vector2(size.x-20-closeWidth,-16),new Vector2(closeWidth,32));
            var weights=tools.Select(button=>Mathf.Max(5,(button.GetComponentInChildren<TMP_Text>(true)?.text.Length??5)+2)).ToArray();
            float weightSum=weights.Sum(),toolX=start;
            for(int i=0;i<tools.Count;i++){float buttonWidth=(rightWidth-12)*weights[i]/weightSum;Set((RectTransform)tools[i].transform,new Vector2(toolX,-60),new Vector2(buttonWidth-4,30));toolX+=buttonWidth;}
            Set(plugins.Root,new Vector2(20,-100),new Vector2(leftWidth,Mathf.Max(200,size.y-152)));
            Set(settings.Root,new Vector2(start,-133),new Vector2(rightWidth,Mathf.Max(170,size.y-185)));
            Set(heading!.rectTransform,new Vector2(start,-102),new Vector2(Mathf.Max(100,rightWidth-140),28));
            if(collapseAll!=null)Set((RectTransform)collapseAll.transform,new Vector2(start+rightWidth-130,-100),new Vector2(130,28));
            Set(footer!.rectTransform,new Vector2(20,-size.y+52),new Vector2(size.x-40,40));
            foreach(var view in views.Values)view.Reflow();lastFingerprint="";dirty=true;
        }
        static void Set(RectTransform rect,Vector2 at,Vector2 size){Position(rect,at);rect.sizeDelta=size;}
        void BuildToolbar(RectTransform panel)
        {
            string[] ids={"profiles","convert","logging","data","keybinds"};
            string[] labels={text("Profiles"),text("Convert"),text("Logging"),text("Data"),text("Keybinds")};
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i],action=id;
                var button=AlmanacButtons.Create(panel,"Tool_"+id,labels[i],()=>{if(action=="keybinds"){filters.Clear();filters.Add("keybinds");selected="";dirty=true;}else ToolRequested?.Invoke(action);},Vector2.zero,new Vector2(85,30));
                tools.Add(button);
            }
            tools.Add(AlmanacButtons.Create(panel,"Filters",text("Filters"),FilterMenu,Vector2.zero,new Vector2(85,30)));
            tools.Add(AlmanacButtons.Create(panel,"Pin",text("Pin"),TogglePin,Vector2.zero,new Vector2(70,30)));
            tools.Add(AlmanacButtons.Create(panel,"Files",text("Files"),FileMenu,Vector2.zero,new Vector2(70,30)));
            RefreshToolbarHelp();
            for(int i=0;i<tools.Count;i++)window!.Focusable(tools[i],i,1);
        }
        void FilterMenu()
        {
            string[] ids={"changed","session","locked","synced","restart","keybinds","meadworks","errors"};
            string[] labels={text("Changed from default"),text("Changed this session"),text("Locked"),text("Synced"),text("Restart required"),text("Keybinds"),text("Meadworks"),text("Errors")};
            KitAlmanacChoice.Open(window!,text("Filters combine with AND"),labels,ids.Where(filters.Contains).Select(id=>labels[Array.IndexOf(ids,id)]).ToArray(),true,values=>
            {filters.Clear();foreach(var value in values){int at=Array.IndexOf(labels,value);if(at>=0)filters.Add(ids[at]);}dirty=true;});
        }
        IEnumerable<IndexGroup> OrderedGroups()
        {
            var pins=new HashSet<string>((preferences?.PinnedPlugins.Value??"").Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).Select(value=>value.Trim()),StringComparer.Ordinal);
            return discovery.Index.Groups.OrderBy(group=>pins.Contains(group.Key)?0:group.Key.StartsWith("com.meadworks.",StringComparison.Ordinal)?1:2)
                .ThenBy(group=>group.DisplayName,StringComparer.OrdinalIgnoreCase);
        }
        void TogglePin()
        {
            if(preferences==null||selected.Length==0)return;
            var pins=new HashSet<string>(preferences.PinnedPlugins.Value.Split(new[]{','},StringSplitOptions.RemoveEmptyEntries),StringComparer.Ordinal);
            if(!pins.Remove(selected))pins.Add(selected);Preference(preferences.PinnedPlugins,string.Join(",",pins.OrderBy(value=>value)));dirty=true;
        }
        void SelectGroup(string key)
        {selected=key;if(preferences?.RememberWindow.Value==true)Preference(preferences.LastPlugin,key);dirty=true;}
        ChangeTiming Timing(NativeEntry native,IndexedFile? file=null)
        {
            if(file?.AllEntriesRequireRestart==true||native.Attributes.RequiresRestart)return ChangeTiming.Restart;
            return SettingNotes.Classify(native.Attributes.Description??native.Entry.Description.Description,(preferences?.RestartPhrases.Value??"").Split(','));
        }
        bool Restart(NativeEntry native,IndexedFile? file=null)=>Timing(native,file)==ChangeTiming.Restart;
        static string Suffix(string note)=>note.Length>0?" | "+note:"";
        string TimingNote(NativeEntry native,IndexedFile? file=null)
        {
            switch(Timing(native,file))
            {
                case ChangeTiming.Restart:return text("Restart required");
                case ChangeTiming.WorldReload:return text("Applies after re-entering the world");
                case ChangeTiming.NewItemsOnly:return text("Applies only to new items");
                default:return "";
            }
        }
        bool PassFilters(NativeEntry native,NativeWriteFacts facts,bool changed,string reason,IndexGroup group)
        {
            foreach(var filter in filters)
            {
                if(filter=="changed"&&!changed)return false;
                if(filter=="session"&&!ChangedInSession(native.Entry))return false;
                if(filter=="locked"&&reason.Length==0)return false;
                if(filter=="synced"&&!facts.EntrySynced)return false;
                if(filter=="restart"&&!Restart(native,discovery.Index.Find(native.Entry.ConfigFile.ConfigFilePath)))return false;
                if(filter=="keybinds"&&native.Shape.Kind!=SettingKind.KeyCode&&native.Shape.Kind!=SettingKind.KeyboardShortcut)return false;
                if(filter=="meadworks"&&!group.Key.StartsWith("com.meadworks.",StringComparison.Ordinal))return false;
                if(filter=="errors"&&native.Tags.Errors.Count==0&&(!writes.TryGetValue(native.Entry,out var state)||!state.Error))return false;
            }
            return true;
        }
        bool InlineDescriptions=>productFilter.Length>0||preferences?.DescriptionLines.Value=="Inline";
        float RowHeight(RowModel row){row.DescriptionExtra=DescriptionExtra(row);return row.Header?34:(rightWidth<620?144:112)-(InlineDescriptions?0:40)+(row.Native?.Shape.HasRange==true||row.Native?.Editor.Kind==EditorKind.CustomDrawer||row.Native?.Editor.Kind==EditorKind.UguiDrawer?40:0)+row.DescriptionExtra;}
        string DescriptionText(RowModel row)=>string.IsNullOrWhiteSpace(row.Info)?text("This mod did not supply a description."):row.Info;
        // An inline description wraps; past its two-line slot the row grows by the extra lines instead of truncating them.
        float DescriptionExtra(RowModel row)
        {
            if(row.Header||!InlineDescriptions||window?.Panel==null)return 0;
            if(measure==null){measure=KitCanvas.Text(window.Panel,"Almanac_measure","",14,Color.clear,TextAlignmentOptions.TopLeft,true);measure.richText=false;measure.raycastTarget=false;measure.enabled=false;}
            float height=measure.GetPreferredValues(DescriptionText(row),rightWidth-20,0).y;
            return float.IsNaN(height)||float.IsInfinity(height)?0:Mathf.Max(0,Mathf.Ceil(height)+2-36);
        }
        RowView? Focused()
        {
            var selected=EventSystem.current?.currentSelectedGameObject;
            return selected==null?null:views.Values.FirstOrDefault(view=>view.Contains(selected.transform));
        }
        WorldRowView? FocusedWorld()
        {
            var selected=EventSystem.current?.currentSelectedGameObject;
            return selected==null?null:worldViews.Values.FirstOrDefault(view=>view.Contains(selected.transform));
        }
        public bool CloseTopmost()=>window?.Panel!=null&&KitAlmanacDialog.Active?.Panel!=null&&KitAlmanacDialog.Active.Panel.IsChildOf(window.Panel)&&KitAlmanacDialog.CloseTopmost();
        public void FocusSearch()=>search?.Focus();
        public void FocusPane(int pane)
        {var list=pane==0?plugins:settings;if(list!=null&&list.Content!=null){var control=list.Content.GetComponentsInChildren<Selectable>(false).FirstOrDefault(c=>c.IsInteractable());if(control!=null)EventSystem.current?.SetSelectedGameObject(control.gameObject);}}
        public void ResetFocused(){var view=Focused();view?.Reset();}
        public void NudgeFocused(int amount){Focused()?.Nudge(amount);FocusedWorld()?.Nudge(amount);}
        public void FocusMoved(){plugins?.Tick();settings?.Tick();}
        public bool NavigateRow(int delta)
        {
            var key=Focused()?.Key??FocusedWorld()?.Key;if(key==null||settings==null)return false;
            int at=Array.IndexOf(displayedKeys,key);if(at<0)return false;
            int next=Mathf.Clamp(at+delta,0,displayedKeys.Length-1);if(next==at)return false;
            settings.Focus(displayedKeys[next]);return true;
        }
        public void RequestToolbar()=>ToolRequested?.Invoke("profiles");
        public bool ActivateFocused(){var view=Focused();return view?.Activate()==true||FocusedWorld()?.Activate()==true;}
    }
}
