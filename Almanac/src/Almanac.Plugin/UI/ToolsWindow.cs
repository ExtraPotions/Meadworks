// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Editors;
using ExtraPotions.Almanac.Runtime.Discovery;
using ExtraPotions.Almanac.Runtime.Profiles;
using ExtraPotions.Almanac.Runtime.Utilities;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace ExtraPotions.Almanac.Runtime.UI {
    /// <summary>Product tools over the existing profile/converter/logging services. Every apply is explicit and uses its owner's admission.</summary>
    public sealed class ToolsWindow:IDisposable {
        readonly ConfigProfiles profiles;readonly ConfigDiscovery discovery;readonly Func<string> selectedPlugin;
        readonly Func<ConfigEntryBase,bool>? matchesFilter;
        readonly HashSet<string> pickedPlugins=new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string,string> inputDrafts=new Dictionary<string,string>(StringComparer.Ordinal);
        string? inputPage;
        KitWindow? window;KitVirtualList? list;KitTextField? name,path;TMP_Text? status;
        readonly List<KitTextField> fields=new List<KitTextField>();
        readonly List<string> focusRows=new List<string>();
        readonly Dictionary<RectTransform,ToolRow> rowViews=new Dictionary<RectTransform,ToolRow>();
        readonly HashSet<ProfilePreviewRow> selected=new HashSet<ProfilePreviewRow>();
        ProfilePreview? preview;ConfigProfile? profile;
        string page="profiles",captureScope="all";bool includeKeys=true,includeAdvanced=true,changedOnly;long revision;
        readonly Dictionary<string,ConversionPlan> conversionPlans=new Dictionary<string,ConversionPlan>();
        readonly ConversionTools conversions=new ConversionTools();readonly DataFileTools data=new DataFileTools();
        readonly Func<string,string> translate;
        public ToolsWindow(ConfigProfiles profiles,ConfigDiscovery discovery,Func<string>? selectedPlugin=null,Func<ConfigEntryBase,bool>? matchesFilter=null,Func<string,string>? translate=null){this.profiles=profiles;this.discovery=discovery;this.selectedPlugin=selectedPlugin??(()=>"");this.matchesFilter=matchesFilter;this.translate=translate??(value=>value);}
        string T(string english)=>translate(english);
        string ToolLabel(string id)=>id=="profiles"?T("Profiles"):id=="convert"?T("Convert"):id=="logging"?T("Logging"):T("Data");
        string ScopeLabel(string id)=>id=="all"?T("All plugins"):id=="Meadworks"?T("Meadworks"):id=="filter"?T("Current filter"):id=="selected"?T("Selected plugin"):T("Hand-picked");
        public bool IsOpen=>window?.IsOpen==true;
        public KitWindow? Window=>window;
        public void FocusSearch(){if(path?.Rect.gameObject.activeInHierarchy==true)EventSystem.current?.SetSelectedGameObject(path.Input.gameObject);else FocusToolbar();}
        public void FocusToolbar(){var first=window?.Panel?.Find("Toolprofiles")?.GetComponent<Button>();if(first!=null)EventSystem.current?.SetSelectedGameObject(first.gameObject);}
        public bool NavigateRow(int delta){
            if(list==null||focusRows.Count==0)return false;
            var selectedObject=EventSystem.current?.currentSelectedGameObject;
            if(selectedObject==null||!selectedObject.transform.IsChildOf(list.Content))return false;
            list.Tick();int current=focusRows.IndexOf(list.FocusedKey);if(current<0)return false;
            int next=current+(delta<0?-1:1);if(next<0||next>=focusRows.Count)return false;list.Focus(focusRows[next]);return true;
        }
        public void Open(string tool){page=tool;if(!Initialize())return;window!.Open();Render();}
        public void Close()=>window?.Close();
        bool Initialize(){
            if(window?.Panel!=null)return true;Release();window=KitWindow.Create("Almanac tools",new Vector2(1000,700),typing:false);if(window==null)return false;
            var panel=window.Panel;var title=Label(panel,"Title",T("Almanac tools"),new Vector2(20,-15),new Vector2(750,28));if(KitCanvas.Heading!=null)title.font=KitCanvas.Heading;
            Button(panel,"Close",T("Close"),Close,new Vector2(895,-15),new Vector2(80,30));
            string[] tools={"profiles","convert","logging","data"};for(int i=0;i<tools.Length;i++){string id=tools[i];Button(panel,"Tool"+id,ToolLabel(id),()=>{page=id;Render();},new Vector2(20+i*240,-55),new Vector2(225,32));}
            name=KitTextField.Create(panel,"ProfileName",KitFieldMode.Text,new Vector2(20,-98),new Vector2(250,32),T("Profile name"));name.Text=T("My settings");fields.Add(name);window.Focusable(name.Input,0,2);
            path=KitTextField.Create(panel,"ProfilePath",KitFieldMode.Text,new Vector2(285,-98),new Vector2(690,32),T("Import/export JSON path"));fields.Add(path);window.Focusable(path.Input,1,2);
            list=KitVirtualList.Create(panel,"Tools",new Vector2(955,495),RowRect);Position(list.Root,new Vector2(20,-145));
            status=Label(panel,"Status","",new Vector2(20,-650),new Vector2(955,35));return true;
        }
        /// <summary>The Window Scale preference as a factor; the designed 1000x700 size is never enlarged past the screen.</summary>
        public Func<float> Scale{get;set;}=()=>1f;
        public void Tick(){if(!IsOpen)return;ApplyScale();list?.Tick();}
        void ApplyScale(){
            var panel=window!.Panel;if(panel==null||!(panel.parent is RectTransform canvas))return;
            float scale=ExtraPotions.Almanac.Display.WindowScale.Bounded(Scale(),1000,700,canvas.rect.width,canvas.rect.height);
            if(!Mathf.Approximately(panel.localScale.x,scale))panel.localScale=new Vector3(scale,scale,1f);
        }
        void Render(){
            if(list==null)return;focusRows.Clear();shownRows.Clear();var rows=new List<KitVirtualRow>();
            if(inputPage!=page)
            {
                if(inputPage!=null&&inputPage!="data")inputDrafts[inputPage]=path!.Text;
                if(page!="data")
                {
                    if(!inputDrafts.TryGetValue(page,out var draft))draft=page=="profiles"?Path.Combine(profiles.ProfilesDirectory,"export.json"):page=="logging"?MeadworksLogging.ConsoleFilter.Format():"";
                    path!.Text=draft;
                }
                inputPage=page;
            }
            name!.Rect.gameObject.SetActive(page=="profiles");path!.Rect.gameObject.SetActive(page!="data");
            KitTooltip.Set(name.Input.gameObject,T("Profile name"),T("Name the profile to save or import. Existing profile names are not overwritten."));
            string pathHelp=page=="convert"?T("Enter only the choices requested by the conversion preview, separated by semicolons. Review the complete plan before applying."):
                page=="logging"?T("Enter console filter rules here before choosing Use text. Rules use source=level, separated by semicolons."):
                T("Full JSON path for importing or exporting a profile. Existing export files are not overwritten.");
            string inputLabel=page=="convert"?T("Conversion choices"):page=="logging"?T("Console rules"):T("Import/export JSON path");
            if(path.Input.placeholder is TMP_Text hint)hint.text=inputLabel;
            KitTooltip.Set(path.Input.gameObject,inputLabel,pathHelp);
            if(page=="profiles")ProfileRows(rows);else if(page=="convert")ConversionRows(rows);else if(page=="logging")LoggingRows(rows);else if(page=="data")DataRows(rows);
            list.SetRows(rows,++revision);
        }
        void ProfileRows(List<KitVirtualRow> rows){
            Add(rows,"scope",T("Capture:")+" "+ScopeLabel(captureScope),T("Change"),()=>{string[] scopes={"all","Meadworks","filter","selected","handpicked"};captureScope=scopes[(Array.IndexOf(scopes,captureScope)+1)%scopes.Length];Render();});
            if(captureScope=="handpicked")foreach(var item in discovery.Index.Groups.Where(g=>g.Kind==ExtraPotions.Almanac.Index.GroupKind.Plugin)){string key=item.Key;Toggle(rows,"pick:"+key,item.DisplayName,pickedPlugins.Contains(key),()=>{if(!pickedPlugins.Add(key))pickedPlugins.Remove(key);Render();});}
            Toggle(rows,"keys",T("Include keybinds"),includeKeys,()=>{includeKeys=!includeKeys;Render();});
            Toggle(rows,"advanced",T("Include advanced"),includeAdvanced,()=>{includeAdvanced=!includeAdvanced;Render();});
            Toggle(rows,"changed",T("Capture only changed values"),changedOnly,()=>{changedOnly=!changedOnly;Render();});
            Add(rows,"capture",T("Save chosen local values as a named profile."),T("Save"),()=>{
                profile=profiles.Capture(name!.Text,Selection());var result=profiles.Save(profile);Notice(result.Success?T("Profile saved."):result.Error??T("Save failed."));Render();
            });
            Add(rows,"defaults",T("Defaults is computed from currently discovered settings."),T("Preview"),()=>SetPreview(profiles.Capture("Defaults",Selection(),true)));
            Add(rows,"undo",T("Undo the last applied profile."),T("Undo"),()=>ShowApply(profiles.Undo()),profiles.CanUndo);
            Add(rows,"import",T("Import JSON from the path above; an existing name is refused."),T("Import"),()=>{var result=profiles.Import(path!.Text,name!.Text);Notice(result.Success?T("Profile imported."):result.Error??T("Import failed."));Render();});
            Add(rows,"export",T("Export the current preview's profile; an existing destination is refused."),T("Export"),()=>{var result=profiles.Export(profile??throw new InvalidOperationException(T("Preview a profile first.")),path!.Text);Notice(result.Success?T("Profile exported."):result.Error??T("Export failed."));},profile!=null);
            foreach(string item in profiles.List()){string key=item;Add(rows,"load:"+key,key,T("Preview"),()=>SetPreview(profiles.Load(key)));}
            if(preview==null)return;
            Add(rows,"apply",T("Apply selected preview rows. Remote server values are excluded."),T("Apply"),()=>ShowApply(profiles.Apply(preview,row=>selected.Contains(row))));
            int index=0;foreach(var item in preview.Rows){var row=item;bool choose=row.Outcome==ProfileOutcome.Apply;
                string title=row.Value.Plugin+" / "+row.Value.Section+" / "+row.Value.Key+"\n"+row.Outcome+": "+(row.Current??"?")+" → "+(row.Target??row.Value.Value)+(row.WasClamped?" ("+T("clamped")+")":"")+(row.RequiresRestart?" ("+T("restart")+")":"")+" "+row.Reason;
                if(choose)Toggle(rows,"preview:"+(index++),title,selected.Contains(row),()=>{if(!selected.Add(row))selected.Remove(row);Render();},88,T("Selected"),T("Excluded"));
                else Add(rows,"preview:"+(index++),title,row.Outcome.ToString(),null,false,88);
            }
        }
        ProfileSelection Selection()=>new ProfileSelection{IncludeKeybinds=includeKeys,IncludeAdvanced=includeAdvanced,OnlyChangedFromDefault=changedOnly,
            File=file=>captureScope=="all"||captureScope=="filter"||captureScope=="Meadworks"&&file.Owner?.Guid.StartsWith("com.meadworks.",StringComparison.Ordinal)==true||captureScope=="selected"&&file.Owner?.Guid==selectedPlugin()||captureScope=="handpicked"&&file.Owner!=null&&pickedPlugins.Contains(file.Owner.Guid),
            Entry=(file,entry)=>captureScope!="filter"||(matchesFilter?.Invoke(entry)??false)};
        void SetPreview(ConfigProfile value){profile=value;preview=profiles.Preview(value);selected.Clear();foreach(var row in preview.Rows)if(row.Outcome==ProfileOutcome.Apply)selected.Add(row);Notice(T("Review each value, then Apply."));Render();}
        void ShowApply(ProfileApplyResult result){if(profile!=null)SetPreview(profile);else Render();Notice((result.Success?T("Applied")+": "+result.Applied+" · "+T("Skipped")+": "+result.Skipped:T("Refused")+": "+result.Error)+(result.RollbackErrors.Count>0?"; "+T("restoration")+": "+string.Join("; ",result.RollbackErrors):""));}
        void ConversionRows(List<KitVirtualRow> rows){
            var registrations=conversions.List();if(registrations.Count==0){Add(rows,"none",T("No installed product has registered a conversion."));return;}
            foreach(var option in registrations){var item=option.Registration;var descriptor=item;
                Add(rows,"conversion:"+item.Id,item.SourceName+" → "+item.ProductId+" "+option.StepAsideNotice,T("Preview"),()=>{conversionPlans[descriptor.Id]=conversions.Preview(descriptor);Notice(Describe(conversionPlans[descriptor.Id].Preview));Render();},option.Available);
                if(conversionPlans.TryGetValue(item.Id,out var plan)){
                Add(rows,"review:"+item.Id,T("Review the complete conversion plan and required choices."),T("Review"),()=>KitAlmanacTextEditor.Open(window!,item.SourceName,Describe(plan.Preview),_=>"",_=>{},true));
                Add(rows,"apply:"+item.Id,T("Apply the reviewed values. Write each choice above as target=source (a key binding as binding:target=shortcut), separated by semicolons."),T("Apply"),()=>{
                    // The producer owns collision choices, backup policy, stale plan and write admission.
                    var choices=new Dictionary<string,string>(StringComparer.Ordinal);foreach(string value in path!.Text.Split(';')){int split=value.IndexOf('=');if(split>0)choices[value.Substring(0,split).Trim()]=value.Substring(split+1).Trim();}
                    var result=conversions.Apply(plan,choices);Notice(Describe(result));conversionPlans.Remove(descriptor.Id);Render();
                },option.Available,100);
                }
            }
        }
        /// <summary>Read-only list of MeadWorks mods and modules that stay off because another mod covers them (Crucible step-aside reports).</summary>
        void CompatibilityRows(List<KitVirtualRow> rows){
            Add(rows,"compat",T("Compatibility")+"\n"+T("MeadWorks parts that stay off because another installed mod covers them. Remove that mod to use them; this list is read-only."),null,null,true,64);
            IReadOnlyList<StepAsideReport> reports;
            try{reports=MeadworksStepAside.Reports();}catch(Exception error){Add(rows,"compat:error",error.GetBaseException().Message);return;}
            if(reports.Count==0){Add(rows,"compat:none",T("Nothing has stepped aside."));return;}
            foreach(var report in reports)
                Add(rows,"compat:"+report.ProductId+"#"+report.ModuleId+"|"+report.Source.Guid,string.Format(report.WholeProduct
                    ?T("{0}: the whole mod is off because {1} is installed."):T("{0}: {1} is off because {2} is installed."),
                    report.ProductName,report.WholeProduct?report.Source.Name:report.ModuleName,report.Source.Name));
        }
        void LoggingRows(List<KitVirtualRow> rows){
            CompatibilityRows(rows);
            Add(rows,"level",T("Meadworks verbosity")+": "+MeadworksLogging.Policy.Level,T("Change"),()=>{var values=(LogVerbosity[])Enum.GetValues(typeof(LogVerbosity));int i=Array.IndexOf(values,MeadworksLogging.Policy.Level);MeadworksLogging.SetLevel(values[(i+1)%values.Length]);Render();});
            Toggle(rows,"filter",T("Console filter"),MeadworksLogging.ConsoleFilter.Enabled,()=>{MeadworksLogging.SetConsoleEnabled(!MeadworksLogging.ConsoleFilter.Enabled);Render();});
            Add(rows,"rules",T("Rules")+": "+MeadworksLogging.ConsoleFilter.Format(),T("Use text"),()=>{MeadworksLogging.SetConsoleRules(path!.Text);Notice(T("Console rules saved."));Render();});
            Toggle(rows,"disk",T("Filter disk log"),MeadworksLogging.FilterDiskEnabled,()=>{MeadworksLogging.SetFilterDisk(!MeadworksLogging.FilterDiskEnabled);Render();});
            var names=BepInEx.Logging.Logger.Sources.Select(s=>s.SourceName).Concat(MeadworksLogging.SourceCounters.Select(c=>c.Source)).Concat(new[]{"*"}).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s=>s,StringComparer.OrdinalIgnoreCase);
            var counts=MeadworksLogging.SourceCounters.ToDictionary(c=>c.Source,StringComparer.OrdinalIgnoreCase);
            foreach(var source in names){string key=source;var level=MeadworksLogging.ConsoleFilter.LevelFor(key);counts.TryGetValue(key,out var count);
                Add(rows,"source:"+key,key+"  "+T("written")+" "+(count?.Written??0)+" / "+T("hidden")+" "+(count?.Hidden??0)+"  "+level,T("Change"),()=>{
                    var rules=MeadworksLogging.ConsoleFilter.Rules.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.OrdinalIgnoreCase);var values=(ConsoleFilterLevel[])Enum.GetValues(typeof(ConsoleFilterLevel));rules[key]=values[(Array.IndexOf(values,level)+1)%values.Length];MeadworksLogging.SetConsoleRules(string.Join(";",rules.Select(p=>p.Key+"="+p.Value)));Render();
                });
            }
            foreach(var product in discovery.Index.Groups.Where(g=>g.Kind==ExtraPotions.Almanac.Index.GroupKind.Plugin)){string id=product.DisplayName;
                Add(rows,"product:"+id,id+" "+T("verbosity")+": "+MeadworksLogging.Policy.LevelFor(id),T("Change"),()=>{var values=(LogVerbosity[])Enum.GetValues(typeof(LogVerbosity));MeadworksLogging.SetProductLevel(id,values[(Array.IndexOf(values,MeadworksLogging.Policy.LevelFor(id))+1)%values.Length]);Render();});
            }
        }
        void DataRows(List<KitVirtualRow> rows){
            if(data.List().Count==0){Add(rows,"none",T("No installed product has registered an editable data file."));return;}
            foreach(var file in data.List()){var descriptor=file;Add(rows,"data:"+file.ProductId+":"+file.Name,file.ProductId+" / "+file.Name,T("Open"),()=>{
                var document=data.Read(descriptor);
                var kind=TextSyntax.Detect(descriptor.Name,document.Original);
                KitAlmanacTextEditor.Open(window!,descriptor.Name,document.Original,value=>{string syntax=AlmanacSyntax.Error(value,kind);if(syntax.Length>0)return syntax;try{return string.Join("\n",data.Validate(document,value));}catch(Exception error){return error.GetBaseException().Message;}},value=>{if(!data.Apply(document,value))throw new InvalidOperationException(T("The product refused this data-file edit."));Notice(T("Data file saved."));},false,AlmanacSyntax.Options(kind,window!,descriptor.Name,AlmanacSyntax.HasTable(document.Original)));
            });}
        }
        public event Action<DataFileRegistration>? DataFileRequested;
        string Describe(object? value){
            if(value is ConversionPreview preview)return string.Join("\n",preview.Values.Select(v=>v.SourceId+" → "+v.TargetId+" = "+v.Value).Concat(preview.Errors.Select(e=>T("Invalid")+": "+e)).Concat(preview.Unknown.Select(e=>T("Skipped")+": "+e)).Concat(preview.Conflicts.Concat(preview.BindingConflicts).Select(c=>T("Choice required")+": "+c.TargetId)));
            if(value is ConversionResult result)return T("Applied")+": "+result.Written+" · "+T("Backup")+": "+(result.BackedUp?T("created"):T("absent"));
            if(value is DrawerImportPreview drawer)return Describe(drawer.Preview)+"\n"+T("Source SHA256")+" "+drawer.SourceSha256;
            if(value is DrawerImportResult imported)return Describe(imported.Result)+"\n"+T("Backup")+" "+imported.BackupPath;
            return value?.ToString()??T("No result.");
        }
        void Notice(string? value){if(status!=null)status.text=value??"";}
        void Guard(Action action){try{action();}catch(Exception error){Notice(error.GetBaseException().Message);}}
        void Toggle(List<KitVirtualRow> rows,string id,string title,bool value,Action change,float height=46,string? on=null,string? off=null){
            focusRows.Add(id);
            int focusRow=rows.Count+4;rows.Add(new KitVirtualRow(id,height,rect=>{
                if(!rowViews.TryGetValue(rect,out var view)){view=new ToolRow(rect);rowViews.Add(rect,view);}
                window?.Unfocusable(view.Button);window?.Unfocusable(view.Toggle.Button);view.Title.text=title;view.Button.gameObject.SetActive(false);view.Toggle.Button.gameObject.SetActive(true);
                view.Toggle.SetLabels(on??T("On"),off??T("Off"));view.Toggle.SetValue(value);view.ToggleChanged=()=>Guard(change);window?.Focusable(view.Toggle.Button,0,focusRow);
                string help=ToolHelp(id,title);view.Toggle.Tooltip(title,help);view.Title.raycastTarget=true;KitTooltip.Set(view.Title.gameObject,title,help);
            },rect=>{if(rowViews.TryGetValue(rect,out var view))window?.Unfocusable(view.Toggle.Button);}));
        }
        readonly List<string> shownRows=new List<string>();
        /// <summary>The rows of the current page as id|title, for native checks.</summary>
        public IReadOnlyList<string> VisibleRows()=>shownRows.ToArray();
        void Add(List<KitVirtualRow> rows,string id,string title,string? action=null,Action? click=null,bool usable=true,float height=46){
            shownRows.Add(id+"|"+title);
            if(action!=null&&usable&&click!=null)focusRows.Add(id);
            int focusRow=rows.Count+4;
            rows.Add(new KitVirtualRow(id,height,rect=>{
                if(!rowViews.TryGetValue(rect,out var view)){view=new ToolRow(rect);rowViews.Add(rect,view);}
                window?.Unfocusable(view.Button);window?.Unfocusable(view.Toggle.Button);view.Toggle.Button.gameObject.SetActive(false);if(action!=null)window?.Focusable(view.Button,0,focusRow);
                view.Title.text=title;view.Button.gameObject.SetActive(action!=null);view.Caption.text=action??"";view.Button.interactable=usable&&click!=null;view.Button.onClick.RemoveAllListeners();if(click!=null)view.Button.onClick.AddListener(()=>Guard(click));
                string help=ToolHelp(id,title);view.Title.raycastTarget=true;KitTooltip.Set(view.Title.gameObject,action??title,help);KitTooltip.Set(view.Button.gameObject,action??title,help);
            },rect=>{if(rowViews.TryGetValue(rect,out var view))window?.Unfocusable(view.Button);}));
        }
        sealed class ToolRow {internal readonly TMP_Text Title,Caption;internal readonly Button Button;internal readonly KitToggle Toggle;internal Action? ToggleChanged;internal ToolRow(RectTransform parent){Title=Label(parent,"Text","",new Vector2(0,-4),new Vector2(735,80));Title.textWrappingMode=TextWrappingModes.Normal;Button=AlmanacButtons.Create(parent,"Action","",()=>{},new Vector2(770,-5),new Vector2(155,32));Caption=Button.GetComponentInChildren<TMP_Text>(true);Toggle=KitToggle.Create(parent,"Toggle",false,_=>ToggleChanged?.Invoke(),new Vector2(770,-5),new Vector2(155,32),Localization.AlmanacText.T("On"),Localization.AlmanacText.T("Off"));}}
        void Button(RectTransform parent,string id,string caption,Action click,Vector2 at,Vector2 size){var button=AlmanacButtons.Create(parent,id,caption,()=>Guard(click),at,size);window?.Focusable(button,(int)(at.x/240),(int)(-at.y/40));KitTooltip.Set(button.gameObject,caption,ToolHelp(id,caption));}
        string ToolHelp(string id,string title)
        {
            string key=id.Split(':')[0],help;
            switch(key)
            {
                case "Close":help=T("Close the tools window. Only changes you explicitly applied are saved.");break;
                case "Toolprofiles":help=T("Save, preview, import or restore groups of settings. Applying a profile still checks each mod's permissions.");break;
                case "Toolconvert":help=T("Preview settings imported from supported mods before applying them. The producing mod validates each conversion.");break;
                case "Toollogging":help=T("Choose console messages and log detail. These controls affect logging, not gameplay.");break;
                case "Tooldata":case "data":help=T("Edit data files registered by installed mods. Each mod validates and saves its own files.");break;
                case "scope":help=T("Choose which mods or visible settings to include in a saved profile. This does not change their current values.");break;
                case "pick":help=T("Include or exclude this mod when saving a hand-picked profile.");break;
                case "keys":help=T("Include keyboard shortcuts when saving a profile. Imported shortcuts still use normal validation.");break;
                case "advanced":help=T("Include specialist settings in the saved profile, even when they are hidden from the ordinary settings view.");break;
                case "changed":help=T("Save only values that differ from their defaults. Turn off to include unchanged values too.");break;
                case "load":help=T("Preview this saved profile and review differences before applying any values.");break;
                case "preview":help=T("Include or exclude this value from the pending profile application. Nothing changes until Apply.");break;
                case "conversion":help=T("Read the supported source and preview its proposed changes. This action does not apply them.");break;
                case "level":case "product":help=T("Choose how much detail appears in mod logs. Higher verbosity produces more diagnostic messages.");break;
                case "filter":help=T("Filter matching console messages without changing gameplay or the underlying mod settings.");break;
                case "disk":help=T("On applies the console filter to LogOutput.log too. Off keeps the disk log complete even when console messages are filtered.");break;
                case "compat":help=T("MeadWorks parts that stay off because another installed mod covers them. Remove that mod to use them; this list is read-only.");break;
                case "source":help=T("Cycle the console filter level for this log source. The counters show written and hidden messages.");break;
                default:help=title;break;
            }
            return help==title?help:help+"\n\n"+title;
        }
        static TMP_Text Label(Transform parent,string id,string text,Vector2 at,Vector2 size){var label=KitCanvas.Text(parent,id,text,16,Color.white,TextAlignmentOptions.TopLeft,false);Position(label.rectTransform,at);label.rectTransform.sizeDelta=size;return label;}
        static RectTransform RowRect(RectTransform parent){var node=new GameObject("ToolRow",typeof(RectTransform));var rect=(RectTransform)node.transform;rect.SetParent(parent,false);return rect;}
        static void Position(RectTransform rect,Vector2 at){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=at;}
        void Release(){if(path?.Input!=null&&inputPage!=null&&inputPage!="data")inputDrafts[inputPage]=path.Text;inputPage=null;foreach(var field in fields)field.Destroy();fields.Clear();list?.Dispose();list=null;foreach(var row in rowViews.Values)row.Toggle.Dispose();rowViews.Clear();window?.Destroy();window=null;}
        public void Dispose(){Release();conversionPlans.Clear();selected.Clear();}
    }
}
