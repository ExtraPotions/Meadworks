// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// A05 native presentation. All controls use Crucible.UI; values write only through EntryWriter.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Attributes;
using ExtraPotions.Almanac.Editors;
using ExtraPotions.Almanac.Index;
using ExtraPotions.Almanac.Runtime.Discovery;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Almanac.Writing;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Almanac.Runtime.UI
{
    /// <summary>Main-thread settings book. Discovery/writer/resolvers remain owned by the plugin.</summary>
    public sealed partial class SettingsWindow : IDisposable
    {
        readonly ConfigDiscovery discovery;
        readonly MeadworksLog? log;
        int timingFiles,timingEntries,timingRows;
        readonly EntryWriter writer;
        readonly Func<ConfigEntryBase,NativeWriteFacts> resolve;
        readonly Func<ConfigEntryBase,bool> hidden;
        readonly Func<string,string> text;
        readonly Func<string,string,bool>? searchMatch;
        readonly Dictionary<RectTransform,RowView> views=new Dictionary<RectTransform,RowView>();
        readonly Dictionary<ConfigEntryBase,string> session=new Dictionary<ConfigEntryBase,string>();
        readonly Dictionary<ConfigEntryBase,WriteState> writes=new Dictionary<ConfigEntryBase,WriteState>();
        readonly Dictionary<ConfigEntryBase,string> codecErrors=new Dictionary<ConfigEntryBase,string>();
        readonly HashSet<ConfigEntryBase> failedDrawers=new HashSet<ConfigEntryBase>();
        readonly Dictionary<string,RowModel> rowModels=new Dictionary<string,RowModel>();
        readonly Dictionary<string,KitVirtualRow> settingRows=new Dictionary<string,KitVirtualRow>();
        KitWindow? window;
        KitTextField? search;
        KitToggle? advanced;
        KitVirtualList? plugins,settings;
        TMP_Text? heading,footer;
        List<Button> tabs=new List<Button>();
        string selected="",queryText="",lastFingerprint="",presentationLanguage="";
        string productFilter="";
        SettingsQuery query=new SettingsQuery("");
        string filter="all";
        bool showAdvanced,dirty=true,disposed;
        float nextPoll,nextRebuild;
        const float RebuildFallback=3f;
        long revision;

        public SettingsWindow(ConfigDiscovery discovery,EntryWriter writer,
            Func<ConfigEntryBase,NativeWriteFacts> resolveNativeWriteFacts,
            Func<ConfigEntryBase,bool> hiddenByServerPolicy,Func<string,string>? translate=null,
            Func<string,string,bool>? matchesSearch=null,MeadworksLog? logger=null)
        {
            this.discovery=discovery??throw new ArgumentNullException(nameof(discovery));
            this.writer=writer??throw new ArgumentNullException(nameof(writer));
            resolve=resolveNativeWriteFacts??throw new ArgumentNullException(nameof(resolveNativeWriteFacts));
            hidden=hiddenByServerPolicy??throw new ArgumentNullException(nameof(hiddenByServerPolicy));
            text=translate??(value=>value);searchMatch=matchesSearch;log=logger;
            discovery.Changed+=Changed;
        }
        public KitWindow? Window=>window;
        public bool IsOpen=>window?.IsOpen==true;
        public event Action? Closed;
        public bool Initialize()
        {
            if(disposed)return false;
            if(window!=null&&window.Panel!=null)return true;
            ReleaseUi();
            window=KitWindow.Create("Almanac",new Vector2(1100,760),typing:false);
            if(window==null)return false;
            var panel=window.Panel;
            title=Label(panel,"Title","Almanac",new Vector2(20,-15),new Vector2(250,32),24);if(KitCanvas.Heading!=null)title.font=KitCanvas.Heading;
            search=KitTextField.Create(panel,"Search",KitFieldMode.Search,new Vector2(320,-16),new Vector2(475,32),text("Search settings"));
            search.Changed+=value=>{queryText=value;query=new SettingsQuery(value);dirty=true;};
            Help(search.Input.gameObject,text("Search settings"),text("Match all search words, quote an exact phrase, or exclude a word with a leading minus sign."));
            window.Focusable(search.Input,1,0);
            advanced=KitToggle.Create(panel,"Advanced",showAdvanced,value=>{showAdvanced=value;if(preferences!=null)Preference(preferences.ShowAdvanced,value);dirty=true;},new Vector2(810,-16),new Vector2(195,32),text("Advanced on"),text("Advanced off"));
            window.Focusable(advanced.Button,2,0);
            advanced.Tooltip(text("Advanced"),text("Show specialist settings. Search can still find matching advanced settings when this switch is off."));
            close=AlmanacButtons.Create(panel,"Close",text("Close"),Close,new Vector2(1015,-16),new Vector2(65,32));
            window.Focusable(close,3,0);
            Help(close.gameObject,text("Close"),text("Close the settings window. Accepted changes are already applied; changes requiring a restart are summarized first."));
            BuildToolbar(panel);
            BuildCollapseAll(panel);
            plugins=KitVirtualList.Create(panel,"Plugins",new Vector2(290,608),RowRect);
            Position(plugins.Root,new Vector2(20,-100));
            settings=KitVirtualList.Create(panel,"Settings",new Vector2(760,575),RowRect);
            Position(settings.Root,new Vector2(320,-133));
            heading=Label(panel,"Selected","",new Vector2(320,-102),new Vector2(760,28),19);if(KitCanvas.Heading!=null)heading.font=KitCanvas.Heading;
            footer=Label(panel,"Status","",new Vector2(20,-720),new Vector2(1060,28),15);footer.textWrappingMode=TextWrappingModes.Normal;
            window.Opened+=()=>{dirty=true;nextPoll=0;HideMainMenu();Fit();};
            window.Closed+=()=>{RestoreMainMenu();Closed?.Invoke();};
            ConfigureCanvas();Reflow(panel.rect.size);Fit();
            dirty=true;return true;
        }
        public void Open(){CancelFeatureReturn();productFilter="";essentialView=false;serverExpanded.Clear();lastFingerprint="";dirty=true;if(Initialize())window!.Open();}
        public bool CanOpenProduct(string guid)=>!disposed&&guid==AlmanacCore.Guid||!disposed&&discovery.IsPluginBrowsable(guid)&&discovery.Index.Groups.Any(group=>group.Key==guid);
        public void OpenProduct(string guid)
        {
            // Almanac's own entry is the all-mods browser, not a view filtered to Almanac's settings.
            if(guid==AlmanacCore.Guid){Open();return;}
            if(!CanOpenProduct(guid)||!Initialize())return;
            CancelFeatureReturn();
            productFilter=guid;essentialView=true;serverExpanded.Clear();queryText="";query=new SettingsQuery("");search!.Text="";
            filters.Clear();SelectGroup(guid);lastFingerprint="";window!.Open();
        }
        public void Close()=>CloseWithSummary();
        public void Refresh(){dirty=true;nextPoll=0;}
        void Changed(ConfigFile file,ConfigEntryBase? entry)=>dirty=true;
        void SetFilter(string value)
        {
            filters.Clear();if(value!="all")filters.Add(value);dirty=true;
        }
        public void Tick()
        {
            if(disposed)return;
            if(window!=null&&window.Panel==null)ReleaseUi();
            discovery.Tick(IsOpen);
            TickFeatureReturn();
            if(!IsOpen)return;
            string currentLanguage=CrucibleUiPlugin.Language;
            if(!string.Equals(presentationLanguage,currentLanguage,StringComparison.Ordinal)){presentationLanguage=currentLanguage;RefreshLocalizedChrome();RefreshChoiceDialogTitle();dirty=true;}
            SyncPreferences();
            TickActions();
            TickWorldEdits();
            if(dirty||Time.unscaledTime>=nextPoll)
            {
                nextPoll=Time.unscaledTime+0.3f;
                // Visible facts refresh while editing, without replacing a focused field's draft.
                NativeKeyConflicts.Refresh();RefreshChordClashes();
                foreach(var view in views.Values)view.RefreshEffective();
                foreach(var view in worldViews.Values)view.Refresh();
                // The full model walks every entry of every plugin: only on a change, plus a slow pass for lock and
                // connection state that raises no event (running it three times a second froze the frame rate).
                if(dirty||Time.unscaledTime>=nextRebuild){nextRebuild=Time.unscaledTime+RebuildFallback;Rebuild();dirty=false;}
            }
            plugins?.Tick();settings?.Tick();
        }
        bool Matches(string facts)=>searchMatch!=null?searchMatch(queryText,facts):query.Matches(facts);
        bool Searching=>!query.Empty;
        NativeWriteFacts Facts(ConfigEntryBase entry)
        {
            try{return resolve(entry)??new NativeWriteFacts();}
            catch(Exception error){return new NativeWriteFacts{Reason=error.GetBaseException().Message};}
        }
        bool Hidden(ConfigEntryBase entry)
        {try{return hidden(entry)||!EntryDependencies.Evaluate(entry).Visible;}catch{return true;}} // An unknown server visibility decision cannot expose a row.
        static string Locked(NativeEntry entry,NativeWriteFacts facts)
        {
            var a=entry.Attributes;
            if(entry.Tags.Errors.Count>0)return string.Join("; ",entry.Tags.Errors);
            if(a.ReadOnly==true||a.CharterReadOnly||a.GenericReadOnly)return LockReasonPolicy.ForReadOnlyTag(facts.EntrySynced,facts.Known,facts.Editable,facts.Reason,"The mod marks this setting read-only.");
            if(!facts.Known)return facts.Reason??"Sync state unknown";
            if(!facts.Editable)return facts.Reason??"This setting is locked here.";
            if((facts.RequiresOwnerWrite||(facts.ConnectedAsClient&&facts.EntrySynced))&&facts.OwnerWrite==null)return "No verified owner write path.";
            var dependency=EntryDependencies.Evaluate(entry.Entry);
            if(!dependency.Visible||!dependency.Enabled)return dependency.Reason;
            return "";
        }
        string Serialize(NativeEntry entry,object? value)
        {
            if(EditorSelector.Codec(entry.Shape,entry.Attributes)==CodecChoice.ModDelegates)
                return (string?)((Delegate)entry.Attributes.ObjToStr!).DynamicInvoke(value)??"";
            return TomlTypeConverter.ConvertToString(value,entry.Entry.SettingType);
        }
        string Status(NativeEntry entry)
        {
            if(!writes.TryGetValue(entry.Entry,out var state))return "";
            if(state.Pending)
            {
                bool confirmed=false;try{confirmed=state.Confirmed?.Invoke()==true;}catch { }
                if(confirmed)
                {
                    state.Pending=false;state.Message="";state.MessageKey="Applied";
                    journal?.RecordConfirmed(new ConfirmedEntryWrite(entry.Entry,state.Before,entry.RawSerialized(),"OwnerReceipt"));
                }
                else if(Time.unscaledTime-state.Submitted>30)state.MessageKey="Not confirmed yet.";
            }
            return state.MessageKey.Length>0?text(state.MessageKey):state.Message;
        }
        void Write(NativeEntry entry,string value)
        {
            // Visibility is rechecked too: a row can be hidden by server policy after it was drawn.
            if(Hidden(entry.Entry)||NativeTags.Read(entry.Entry.Description.Tags).Attributes.Browsable==false||!discovery.IsPluginBrowsable(discovery.Index.Find(entry.Entry.ConfigFile.ConfigFilePath)?.Owner?.Guid??""))return;
            string before=entry.RawSerialized();var result=writer.TryWrite(entry.Entry,value);
            // An optimistic same-value assignment does not replace an outstanding owner receipt.
            if(writes.TryGetValue(entry.Entry,out var outstanding)&&outstanding.Pending&&
                result.Decision?.Verdict==WriteVerdict.NoChange&&result.Serialized==outstanding.Requested)
            {Status(entry);dirty=true;return;}
            writes[entry.Entry]=new WriteState{Before=before,Requested=result.Serialized??"",Pending=result.PendingAuthority,
                Confirmed=result.IsConfirmed,Submitted=Time.unscaledTime,
                Error=!result.Accepted&&!result.PendingAuthority,
                Message=result.Error??result.Decision?.Reason??"",
                MessageKey=result.PendingAuthority?"Waiting for the owner...":result.Accepted?(result.WasClamped?"Applied within the allowed range":"Applied"):result.Error==null&&result.Decision?.Reason==null?"Edit refused":""};
            if(result.Accepted)Acknowledge(entry.Entry);
            foreach(var view in views.Values)view.RefreshEffective();
            dirty=true;
        }
        void Reset(NativeEntry entry)
        {
            if(failedDrawers.Contains(entry.Entry))
            {writes[entry.Entry]=new WriteState{Error=true,MessageKey="Reset skipped: this entry's custom drawer failed."};dirty=true;return;}
            WriteValue(entry,Presentation.ResetTarget(entry.Attributes,entry.Entry.DefaultValue));
        }
        void WriteValue(NativeEntry entry,object? value)
        {
            try{Write(entry,Serialize(entry,value));}
            catch(Exception error){writes[entry.Entry]=new WriteState{Error=true,Message=error.GetBaseException().Message};dirty=true;}
        }
        void Rebuild()
        {
            var timing=new StallTiming(log);
            long before=revision;
            timingFiles=timingEntries=timingRows=0;
            try{RebuildModel();}
            finally
            {
                if(timing.Enabled)timing.Finish().Complete("Almanac.model", "open="+IsOpen+" productPage="+essentialView
                    +" files="+timingFiles+" entries="+timingEntries+" rows="+timingRows+" presentationChanged="+(revision!=before));
            }
        }
        void RebuildModel()
        {
            NativeKeyConflicts.Refresh();
            var groups=new List<(IndexGroup Group,List<RowModel> Rows)>();
            var fingerprint=new System.Text.StringBuilder(presentationLanguage+"|"+selected+"|"+queryText+"|"+string.Join(",",filters.OrderBy(value=>value))+"|"+showAdvanced+"|"+TierFingerprint()+"|"+rightWidth+"|"+string.Join(",",collapsed.OrderBy(value=>value)));
            foreach(var group in OrderedGroups())
            {
                if(!InProductScope(group))continue;
                if(!discovery.IsPluginBrowsable(group.Key))continue;
                var rows=new List<RowModel>();var folded=new List<RowModel>();
                if(essentialView&&group.Key==productFilter)rows.Add(AllSettingsRow(group));
                AddFeatureRows(group,rows,fingerprint);
                AddPresetRows(group,rows,fingerprint);
                int serverAt=rows.Count; // The "Set by the server" line sits above the settings, where it is seen without scrolling.
                foreach(var file in group.Files)
                {
                    timingFiles++;
                    if(file.Kind==FileKind.UnboundConfig||file.Kind==FileKind.DataFile)
                    {
                        if(essentialView)continue; // The mod's page lists everyday settings only; files stay in the full view.
                        if(file.Kind==FileKind.UnboundConfig&&preferences?.ShowUnboundFiles.Value==false)continue;
                        if(file.Kind==FileKind.UnboundConfig)
                            try{var info=new FileInfo(file.Path);fingerprint.Append(file.Path).Append(info.LastWriteTimeUtc.Ticks).Append(info.Exists?info.Length:0);}catch { }
                        if(filters.Count==0&&Matches(group.DisplayName+" "+group.Key+" "+file.Path))
                            rows.Add(new RowModel{Key="disk:"+file.Path,Title=file.DisplayName,File=file,Info=file.Kind==FileKind.UnboundConfig?text("Unbound configuration (read-only)"):text("Registered data file: open it from the data tool")});
                        continue;
                    }
                    var entries=new List<RowModel>();
                    foreach(var handle in discovery.Entries(file.Path))
                    {
                        timingEntries++;
                        try
                        {
                            var native=new NativeEntry(handle);var facts=Facts(handle);var reason=Locked(native,facts);
                            bool serverHidden=Hidden(handle);
                            var display=EntryPresentation.Resolve(native,presentationLanguage,text);
                            string name=display.Label;
                            string value="",codecError="",description=display.Help;
                            // Resolve visibility before a failed codec can create an error row.
                            if(serverHidden||native.Attributes.Browsable==false)continue;
                            try
                            {
                                value=native.Text();
                                if(codecErrors.TryGetValue(handle,out var oldError))
                                {
                                    codecErrors.Remove(handle);
                                    if(writes.TryGetValue(handle,out var oldState)&&!oldState.Pending&&oldState.Message==oldError)writes.Remove(handle);
                                }
                            }
                            catch(Exception error)
                            {
                                codecError=error.GetBaseException().Message;
                                value=text("Read-only: value converter failed")+" | "+codecError;
                            }
                            bool match=Matches(group.DisplayName+" "+group.Key+" "+handle.Definition.Section+" "+handle.Definition.Key+" "+name+" "+display.Section+" "+description+" "+value+" "+reason);
                            var tier=Tier(group,handle);
                            if(!match||!TierShows(tier,native,file,match))continue;
                            if(codecError.Length>0)
                            {
                                codecErrors[handle]=codecError;
                                if(writes.TryGetValue(handle,out var oldState)&&oldState.Pending)oldState.Error=true;
                                else writes[handle]=new WriteState{Error=true,Message=codecError};
                            }
                            if(!session.ContainsKey(handle))session[handle]=SafeRaw(handle);
                            bool changed;
                            try{changed=native.RawSerialized()!=TomlTypeConverter.ConvertToString(Presentation.ResetTarget(native.Attributes,handle.DefaultValue),handle.SettingType);}
                            catch{changed=false;}
                            if(!PassFilters(native,facts,changed,reason,group))continue;
                            var model=new RowModel{Key="entry:"+file.Path+"|"+handle.Definition.Section+"|"+handle.Definition.Key,Title=name,Native=codecError.Length==0?native:null,
                                Attributes=native.Attributes,Section=handle.Definition.Section,ShownSection=display.Section,CodecError=codecError,File=file,Info=description,Reason=reason,
                                FileOnly=tier==SettingTier.FileOnly};
                            if(ServerFolds(facts))
                            {
                                // Host-owned rows of a non-admin player fold into one "Set by the server" line.
                                model.ServerSet=true;model.Title=display.Section+" / "+name;folded.Add(model);
                                fingerprint.Append(model.Key).Append(value).Append(display.Label);
                                continue;
                            }
                            entries.Add(model);fingerprint.Append(model.Key).Append(value).Append(reason).Append(Status(native))
                                .Append(display.Label).Append(display.Section).Append(display.Help);
                        }
                        catch { } // A failed converter/tag must not bypass hidden/advanced entry rules.
                    }
                    foreach(var section in entries.GroupBy(row=>Presentation.Heading(row.Native?.Attributes??row.Attributes??new EntryAttributes(),row.Native?.Entry.Definition.Section??row.Section)))
                    {
                        string sectionKey="heading:"+file.Path+"|"+section.Key;
                        string sectionTitle=EntryPresentation.SectionTitle(text(section.Key),section.Select(row=>row.ShownSection));
                        // A mod's MeadWorks menu page names only the section; the full view keeps the file for disambiguation.
                        rows.Add(new RowModel{Key=sectionKey,Title=essentialView?sectionTitle:file.DisplayName+" / "+sectionTitle,Section=section.Key,File=file,Header=true});
                        if(Searching||!collapsed.Contains(sectionKey))rows.AddRange(Presentation.Sort(section,row=>row.Native?.Attributes.Order??row.Attributes?.Order,row=>row.Title));
                    }
                    foreach(var orphan in discovery.Orphans(file.Path))
                        // Keys no mod binds are file leftovers: never on a mod's page, and in the full view only with Advanced on.
                        if(!essentialView&&showAdvanced&&filters.Count==0&&Matches(group.DisplayName+" "+orphan.Key+" "+orphan.Value))
                            rows.Add(new RowModel{Key="orphan:"+file.Path+"|"+orphan.Key,Title=text("Unused key")+" "+orphan.Key,Info=orphan.Value+"\n"+text("No plugin bound this key; read-only.")});
                }
                AddServerRows(group,rows,folded,serverAt);
                AddWorldRows(group,rows,fingerprint);
                if(rows.Count>0||preferences?.ShowEmptyPlugins.Value==true&&Matches(group.DisplayName+" "+group.Key))
                {
                    groups.Add((group,rows));
                    timingRows+=rows.Count;
                    foreach(var row in rows)fingerprint.Append(row.Key).Append(row.Title).Append(row.Info);
                }
            }
            MergeCompanions(groups);
            if(!groups.Any(item=>item.Group.Key==selected))selected=groups.FirstOrDefault().Group?.Key??"";
            string current=fingerprint.Append(selected).Append(string.Join("|",groups.Select(g=>g.Group.Key+":"+g.Rows.Count))).ToString();
            if(current==lastFingerprint)return;
            lastFingerprint=current;revision++;
            var left=new List<KitVirtualRow>();
            string priorFamily="";
            for(int i=0;i<groups.Count;i++)
            {
                var item=groups[i];int index=i;
                bool meadworks=item.Group.Key.StartsWith("com.meadworks.",StringComparison.Ordinal);string family=meadworks?"Meadworks":"Other plugins";
                if(family!=priorFamily){priorFamily=family;string label=meadworks?text("Meadworks"):text("Other plugins");left.Add(new KitVirtualRow("family:"+family+"#"+i,30,rect=>BindInfo(rect,label,""),Clear));}
                left.Add(new KitVirtualRow(item.Group.Key,40,rect=>BindPlugin(rect,item.Group,index),Clear));
            }
            plugins!.SetRows(left,revision);
            var selectedGroup=groups.FirstOrDefault(g=>g.Group.Key==selected);
            heading!.text=selectedGroup.Group?.DisplayName??text("No matching settings");
            RefreshCollapseAll(selectedGroup.Rows?.Where(row=>row.Header).Select(row=>row.Key)??Enumerable.Empty<string>());
            var right=new List<KitVirtualRow>();
            var displayRows=selectedGroup.Rows??new List<RowModel>();
            if(filters.Contains("keybinds"))
            {
                displayRows=groups.SelectMany(g=>g.Rows.Where(r=>r.Native!=null).Select(r=>{r.Title=g.Group.DisplayName+" / "+r.Title;return r;}))
                    .OrderBy(r=>r.Native!.RawSerialized(),StringComparer.OrdinalIgnoreCase).ToList();
                foreach(var row in displayRows)row.NativeConflict=NativeKeyConflicts.Describe(row.Native!.Entry.BoxedValue);
                heading.text=text("Keybinds (shared chords are informational)");
            }
            foreach(var row in displayRows)
            {
                if(row.Preset!=null)
                {
                    row.Index=right.Count;
                    right.Add(new KitVirtualRow(row.Key,76,rect=>BindPreset(rect,row),UnbindFeature));
                    continue;
                }
                if(row.Action.Length>0)
                {
                    row.Index=right.Count;
                    right.Add(new KitVirtualRow(row.Key,76,rect=>BindTierAction(rect,row),UnbindFeature));
                    continue;
                }
                if(row.FeatureId.Length>0)
                {
                    row.Index=right.Count;
                    right.Add(new KitVirtualRow(row.Key,76,rect=>BindFeature(rect,row),UnbindFeature));
                    continue;
                }
                if(row.World!=null)
                {
                    row.Index=right.Count;rowModels[row.Key]=row;
                    float height=(rightWidth<620?144:112)+(new NativeEntry(row.World.Config).Shape.HasRange?40:0);
                    if(!settingRows.TryGetValue(row.Key,out var virtualRow)||virtualRow.Height!=height)
                    {
                        string key=row.Key;
                        settingRows[key]=virtualRow=new KitVirtualRow(key,height,rect=>BindWorld(rect,rowModels[key]),UnbindWorld);
                    }
                    right.Add(virtualRow);
                    continue;
                }
                if(row.WorldHeading)
                {
                    right.Add(new KitVirtualRow(row.Key,62,rect=>BindInfo(rect,row.Title,row.Info),Clear));
                    continue;
                }
                if(row.File?.Kind==FileKind.UnboundConfig||row.File?.Kind==FileKind.DataFile)
                {
                    right.Add(new KitVirtualRow(row.Key,62,rect=>BindInfo(rect,row.Title,row.Info),Clear));
                    // Live bound files are never exposed as raw disk text (Browsable/CCS hiding remains intact).
                    if(row.File.Kind==FileKind.UnboundConfig)AddDiskRows(right,row.File);
                }
                else
                {
                    row.Index=right.Count;rowModels[row.Key]=row;
                    float height=RowHeight(row);
                    if(!settingRows.TryGetValue(row.Key,out var virtualRow)||virtualRow.Height!=height)
                    {
                        string key=row.Key;
                        settingRows[key]=virtualRow=new KitVirtualRow(key,height,rect=>BindSetting(rect,rowModels[key]),UnbindSetting);
                    }
                    right.Add(virtualRow);
                }
            }
            settings!.SetRows(right,revision);
            if(lastSelected!=selected){settings.Scroll.verticalNormalizedPosition=1;lastSelected=selected;}
            shownRows=displayRows.ToArray();
            displayedKeys=right.Where(row=>rowModels.TryGetValue(row.Key,out var model)&&!model.Header&&(model.Native!=null||model.World!=null)).Select(row=>row.Key).ToArray();
            var retained=new HashSet<string>(right.Select(row=>row.Key));
            foreach(var key in settingRows.Keys.Where(key=>!retained.Contains(key)).ToArray())
            {settingRows.Remove(key);rowModels.Remove(key);}
            foreach(var key in rowModels.Keys.Where(key=>!retained.Contains(key)).ToArray())rowModels.Remove(key);
            int pending=writes.Values.Count(w=>w.Pending)+worldWrites.Values.Count(w=>w.Error.Length==0&&w.Watch.Pending),changedCount=journal?.Count??session.Count(pair=>SafeRaw(pair.Key)!=pair.Value);
            footer!.text=text("Changes are applied through each mod's owner")+" | "+text("Settings changed this session")+": "+changedCount+
                (pending>0?" | "+text("Pending owner responses")+": "+pending:"")+
                (journal?.RestartCount>0?" | "+text("Need a restart")+": "+journal.RestartCount:"")+
                (actionMessage.Length>0?" | "+actionMessage:"");
            RefreshToolbarHelp();
        }
        static string SafeRaw(ConfigEntryBase entry){try{return new NativeEntry(entry).RawSerialized();}catch{return "";}}
        void BindPlugin(RectTransform rect,IndexGroup group,int index)
        {
            foreach(var control in rect.GetComponentsInChildren<Selectable>(true))window?.Unfocusable(control);
            Clear(rect);var entries=group.Files.SelectMany(file=>discovery.Entries(file.Path)
                .Where(entry=>MatchesKnownEntry(group,file,new NativeEntry(entry)))).ToArray();
            // Companion rows share the product's current visibility and reset scope.
            if(essentialView&&group.Key==productFilter)
                entries=OrderedGroups().Where(owner=>owner.Key==group.Key||SettingTierRules.Companion(productFilter,owner.Key))
                    .SelectMany(owner=>owner.Files.SelectMany(file=>discovery.Entries(file.Path)
                        .Where(entry=>MatchesKnownEntry(owner,file,new NativeEntry(entry))))).ToArray();
            int locked=entries.Count(entry=>Locked(new NativeEntry(entry),Facts(entry)).Length>0),changed=entries.Count(ChangedInSession);
            string caption=group.DisplayName+" ("+entries.Length+")"+(locked>0?" | "+text("Locked")+": "+locked:"")+(changed>0?" | "+text("Changed")+": "+changed:"");
            var button=AlmanacButtons.Create(rect,"Plugin",caption,()=>SelectGroup(group.Key),Vector2.zero,new Vector2(Mathf.Max(100,rect.rect.width-20),36));
            GameButtons.Tint(button,selected==group.Key);KitTooltip.Set(button.gameObject,group.DisplayName,Blurb(group));
            window!.Focusable(button,0,index+2);
        }
        readonly Dictionary<string,string> blurbs=new Dictionary<string,string>(StringComparer.Ordinal);
        /// <summary>What the mod does, read once per plugin: never file I/O on every bind.</summary>
        string Blurb(IndexGroup group)
        {
            if(blurbs.TryGetValue(group.Key,out var known))return known;
            string manifest="",assembly="",version="";
            if(BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(group.Key,out var info)&&info!=null)
            {
                version=info.Metadata?.Version?.ToString()??"";
                try
                {
                    // r2modman and Thunderstore keep manifest.json in the package folder, at or above the DLL; never above plugins.
                    var plugins=Path.GetFullPath(BepInEx.Paths.PluginPath);
                    for(var folder=Path.GetDirectoryName(info.Location);!string.IsNullOrEmpty(folder)&&manifest.Length==0;folder=Path.GetDirectoryName(folder))
                    {
                        var full=Path.GetFullPath(folder);
                        if(string.Equals(full.TrimEnd('\\','/'),plugins.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase)||!full.StartsWith(plugins,StringComparison.OrdinalIgnoreCase))break;
                        var file=Path.Combine(full,"manifest.json");
                        if(File.Exists(file)&&new FileInfo(file).Length<64*1024)manifest=ExtraPotions.Almanac.Display.PluginBlurb.FromManifest(File.ReadAllText(file));
                    }
                }
                catch { } // An unreadable manifest falls back to the assembly description.
                try{assembly=(info.Instance!=null?info.Instance.GetType().Assembly:null)?.GetCustomAttributes(typeof(System.Reflection.AssemblyDescriptionAttribute),false).OfType<System.Reflection.AssemblyDescriptionAttribute>().FirstOrDefault()?.Description??"";}
                catch { }
            }
            return blurbs[group.Key]=ExtraPotions.Almanac.Display.PluginBlurb.Text(manifest,assembly,group.DisplayName,version);
        }
        void BindInfo(RectTransform rect,string title,string info)
        {Clear(rect);Label(rect,"Name",title,new Vector2(8,-3),new Vector2(724,25),17);Label(rect,"Info",info,new Vector2(8,-28),new Vector2(724,32),14);}
        void BindSetting(RectTransform rect,RowModel model)
        {
            if(!views.TryGetValue(rect,out var view)){Clear(rect);views[rect]=view=new RowView(this,rect);}
            view.Bind(model);
        }
        void UnbindSetting(RectTransform rect)
        {if(views.TryGetValue(rect,out var view)){view.Dispose();views.Remove(rect);}Clear(rect);}
        void AddDiskRows(List<KitVirtualRow> rows,IndexedFile file)
        {
            try
            {
                if(new FileInfo(file.Path).Length>1024*1024){AddLine(text("File exceeds the 1 MiB preview limit."),0);return;}
                using(var stream=File.OpenText(file.Path))
                {
                    int i=0;string? line;
                    while(i<4096&&(line=stream.ReadLine())!=null)AddLine(line,i++);
                    if(!stream.EndOfStream)AddLine(text("Preview truncated after 4096 lines."),i);
                }
            }
            catch(Exception error){AddLine(error.GetBaseException().Message,0);}
            void AddLine(string line,int i)
            {
                rows.Add(new KitVirtualRow("raw:"+file.Path+":"+i,28,rect=>{Clear(rect);Label(rect,"Raw",line,new Vector2(8,-2),new Vector2(724,24),14);},Clear));
            }
        }
        static RectTransform RowRect(RectTransform parent)
        {
            var node=new GameObject("Almanac_row",typeof(RectTransform));node.layer=parent.gameObject.layer;
            var rect=(RectTransform)node.transform;rect.SetParent(parent,false);return rect;
        }
        static void Position(RectTransform rect,Vector2 position)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;}
        static TMP_Text Label(Transform parent,string name,string words,Vector2 at,Vector2 size,float font)
        {
            var label=KitCanvas.Text(parent,name,words,font,Color.white,TextAlignmentOptions.TopLeft,true);
            label.richText=false;label.overflowMode=TextOverflowModes.Truncate;Position(label.rectTransform,at);label.rectTransform.sizeDelta=size;return label;
        }
        static void Clear(RectTransform rect)
        {
            foreach(Transform child in rect){child.gameObject.SetActive(false);child.name="retired";UnityEngine.Object.Destroy(child.gameObject);}
        }
        void ReleaseUi()
        {
            CancelFeatureReturn();
            while(window?.Panel!=null&&KitAlmanacDialog.Active?.Panel!=null&&KitAlmanacDialog.Active.Panel.IsChildOf(window.Panel))KitAlmanacDialog.CloseTopmost();
            window?.Close();
            foreach(var view in views.Values)view.Dispose();views.Clear();
            ReleaseWorldUi();
            plugins?.Dispose();settings?.Dispose();search?.Destroy();advanced?.Dispose();window?.Destroy();
            plugins=settings=null;search=null;advanced=null;window=null;measure=null;heading=footer=title=null;close=null;collapseAll=null;tools.Clear();fileButtons.Clear();tabs.Clear();settingRows.Clear();rowModels.Clear();lastFingerprint="";dirty=true;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;discovery.Changed-=Changed;if(journal!=null)journal.Changed-=JournalChanged;writer.Written-=OwnWritten;ReleaseUi();session.Clear();writes.Clear();worldWrites.Clear();codecErrors.Clear();failedDrawers.Clear();
        }
        sealed class WriteState{internal string Before="",Requested="",Message="",MessageKey="";internal bool Pending,Error;internal Func<bool>? Confirmed;internal float Submitted;}
        sealed class RowModel
        {internal string Key="",Title="",Info="",Reason="",Section="",ShownSection="",CodecError="",NativeConflict="",FeatureId="",Action="";internal int Index;internal float DescriptionExtra;internal bool Header,WorldHeading,FileOnly,ServerSet;internal NativeEntry? Native;internal EntryAttributes? Attributes;internal IndexedFile? File;internal WorldSettingMetadata? World;internal AlmanacPresetChoice? Preset;}
        sealed partial class RowView : IDisposable
        {
            readonly SettingsWindow owner;readonly RectTransform rect;
            RowModel? model;KitTextField? field;KitToggle? toggle;Button? cycle,reset,raw;
            TMP_Text? name,description,state;bool rawMode;
            internal RowView(SettingsWindow owner,RectTransform rect){this.owner=owner;this.rect=rect;}
            internal void Bind(RowModel value)
            {
                bool same=model?.Native?.Entry!=null&&ReferenceEquals(model.Native.Entry,value.Native?.Entry);
                model=value;
                if(!same){rawMode=false;drawerErrors=0;drawerFailed=value.Native!=null&&owner.failedDrawers.Contains(value.Native.Entry);Build();}
                RegisterFocus();
                RefreshLocalizedActions();Reflow();RefreshEffective();
            }
            void Build()
            {
                UnregisterFocus();ReleaseEditors();field?.Destroy();field=null;toggle?.Dispose();toggle=null;Clear(rect);cycle=reset=raw=null;
                name=Label(rect,"Name",model!.Title,new Vector2(8,-4),new Vector2(320,30),model.Header?18:16);
                if(model.Header)
                {
                    cycle=AlmanacButtons.Create(rect,"Collapse",(owner.collapsed.Contains(model.Key)?"+ ":"- ")+model.Title,()=>{if(!owner.collapsed.Remove(model.Key))owner.collapsed.Add(model.Key);owner.dirty=true;},Vector2.zero,new Vector2(500,30));name.gameObject.SetActive(false);
                    reset=AlmanacButtons.Create(rect,"ResetSection",owner.text("Reset section"),()=>owner.ResetSelection(owner.discovery.Entries(model.File!.Path).Where(entry=>Presentation.Heading(new NativeEntry(entry).Attributes,entry.Definition.Section)==model.Section)),Vector2.zero,new Vector2(150,30));
                    owner.Help(cycle.gameObject,model.Title,owner.text("Collapse or expand this settings section."));
                    owner.Help(reset.gameObject,owner.text("Reset section"),owner.text("Preview restoring this section's settings to their defaults. Locked and hidden settings remain protected."));return;
                }
                description=Label(rect,"Description",model.Info,new Vector2(8,-40),new Vector2(710,36),14);
                state=Label(rect,"State","",new Vector2(8,-80),new Vector2(710,26),14);
                if(model.Native==null)
                {
                    name.raycastTarget=true;owner.Help(name.gameObject,model.Title,model.Info);
                    description.raycastTarget=true;owner.Help(description.gameObject,model.Title,model.Info);
                    if(model.CodecError.Length>0)
                    {state.text=owner.text("Read-only: value converter failed")+" | "+model.CodecError;name.raycastTarget=true;KitTooltip.Set(name.gameObject,model.Title,model.Info+"\n"+state.text);}
                    return;
                }
                var native=model.Native;var kind=drawerFailed?EditorSelector.Fallback(native.Shape,native.Attributes,native.Text()).Kind:native.Editor.Kind;
                if(!rawMode&&BuildAdvanced(native,kind)){ }
                else if(!rawMode&&kind==EditorKind.Toggle)
                    toggle=KitToggle.Create(rect,"Value",native.Entry.BoxedValue is bool b&&b,value=>owner.WriteValue(model!.Native!,value),new Vector2(350,-3),new Vector2(220,30),owner.text("On"),owner.text("Off"));
                else if(!rawMode&&(kind==EditorKind.Cycle||kind==EditorKind.Dropdown))
                {cycle=AlmanacButtons.Create(rect,"Value",native.Text(),Cycle,new Vector2(350,-3),new Vector2(220,30));choice=AlmanacButtons.Create(rect,"Choices","...",()=>owner.Choices(model!.Native!,false),Vector2.zero,new Vector2(28,30));}
                else
                {
                    field=KitTextField.Create(rect,"Value",KitFieldMode.Text,new Vector2(350,-3),new Vector2(220,30));
                    field.Validator=value=>model!.Native!.Parse(InputText(value)).Valid;
                    field.Ended+=edit=>{if(edit.Commit)owner.Write(model!.Native!,InputText(edit.Text));};
                }
                if(Presentation.ShowsResetButton(native.Attributes))
                    reset=AlmanacButtons.Create(rect,"Reset",owner.text("Reset"),()=>owner.Reset(model!.Native!),new Vector2(648,-3),new Vector2(72,30));
                if(EditorSelector.CanEditAsText(native.Shape,native.Attributes))
                    raw=AlmanacButtons.Create(rect,"TextMode",owner.text("Text"),()=>owner.TextEditor(model!.Native!,true),new Vector2(578,-3),new Vector2(64,30));
                RegisterFocus();
                Reflow();
            }
            void RegisterFocus()
            {
                int y=2+model!.Index;
                if(field!=null)owner.window!.Focusable(field.Input,1,y);
                if(toggle!=null)owner.window!.Focusable(toggle.Button,1,y);
                if(cycle!=null)owner.window!.Focusable(cycle,1,y);
                if(raw!=null)owner.window!.Focusable(raw,2,y);
                if(reset!=null)owner.window!.Focusable(reset,3,y);
                FocusEditors(y);
            }
            bool Basic(NativeEntry native)
            {
                var kind=drawerFailed?EditorSelector.Fallback(native.Shape,native.Attributes,native.Text()).Kind:native.Editor.Kind;
                return kind==EditorKind.Toggle||kind==EditorKind.Cycle||kind==EditorKind.Dropdown||kind==EditorKind.Spinner||kind==EditorKind.Slider||kind==EditorKind.TextField||kind==EditorKind.RawText;
            }
            string DisplayText(NativeEntry native)=>!rawMode&&native.Shape.Kind==SettingKind.String&&EditorSelector.Codec(native.Shape,native.Attributes)!=CodecChoice.ModDelegates
                ?native.Entry.BoxedValue as string??"":native.Text();
            string InputText(string value)
            {
                var native=model!.Native!;
                return !rawMode&&native.Shape.Kind==SettingKind.String&&EditorSelector.Codec(native.Shape,native.Attributes)!=CodecChoice.ModDelegates
                    ?TomlTypeConverter.ConvertToString(value,typeof(string)):value;
            }
            void Cycle()
            {
                var native=model!.Native!;
                var options=native.Shape.AcceptableList??native.Shape.EnumNames;
                if(options==null||options.Count==0)return;
                int index=options.ToList().IndexOf(native.RawSerialized());
                var next=TomlTypeConverter.ConvertToValue(options[(index+1)%options.Count],native.Entry.SettingType);
                owner.WriteValue(native,next);
            }
            internal void RefreshEffective()
            {
                if(model?.Native==null)return;
                var native=model.Native;
                if(owner.Hidden(native.Entry)||native.Attributes.Browsable==false){rect.gameObject.SetActive(false);owner.dirty=true;return;}
                try
                {
                    var facts=owner.Facts(native.Entry);string reason=Locked(native,facts);
                    // Expanded "Set by the server" rows are read-only for this player.
                    if(model.ServerSet&&reason.Length==0)reason=owner.text("Set by the server.");
                    string lockedReason=reason;
                    bool can=reason.Length==0&&(rawMode||Basic(native)||HasAdvanced)&&EditorSelector.Codec(native.Shape,native.Attributes)!=CodecChoice.None;
                    var selected=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
                    if(!can && selected && selected.transform.IsChildOf(rect))UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                    if(reason.Length==0&&!can)reason=owner.text("Edit this value as text.");
                    string displayed=DisplayText(native);
                    // A single-line kit field clips its input. Never allow committing a clipped long/multiline value.
                    if(field!=null&&(displayed.Length>KitInputLayout.TextLength||displayed.IndexOfAny(new[]{'\n','\r','\t'})>=0))
                    {can=false;reason=owner.text("This value needs the multiline editor; this preview is read-only.");}
                    if(field!=null){field.SetUsable(can);if(!field.Focused)field.Text=displayed;KitTooltip.Set(field.Input.gameObject,model.Title,reason+"\n"+displayed);}
                    toggle?.SetUsable(can,reason);if(toggle!=null)toggle.SetValue(native.Entry.BoxedValue is bool b&&b);
                    if(cycle!=null){cycle.interactable=can;var caption=cycle.GetComponentInChildren<TMP_Text>(true);if(caption!=null)caption.text=native.Text();KitTooltip.Set(cycle.gameObject,model.Title,reason);}
                    if(reset!=null){reset.interactable=lockedReason.Length==0;KitTooltip.Set(reset.gameObject,owner.text("Reset"),reason);}
                    if(raw!=null)raw.interactable=lockedReason.Length==0;
                    RefreshEditors(can);
                    // Expanded server rows only show the value: no Text, Reset or Undo buttons.
                    raw?.gameObject.SetActive(!model.ServerSet);reset?.gameObject.SetActive(!model.ServerSet);undo?.gameObject.SetActive(!model.ServerSet);
                    model.NativeConflict=NativeKeyConflicts.Describe(native.Entry.BoxedValue);
                    name!.text=model.Title;description!.text=owner.DescriptionText(model);
                    string draftError=DraftError(native);
                    state!.text=reason.Length>0?reason:draftError.Length>0?draftError:owner.Status(native)+(facts.EntrySynced?" | "+owner.text("Synced"):"")+
                        Suffix(owner.TimingNote(native,model.File))+Suffix(owner.ChordClash(native.Entry))+(model.NativeConflict.Length>0?" | "+model.NativeConflict:"")+owner.MarkText(native);
                    state.text=owner.SettingOwner(native)+(state.text.Length>0?" | "+state.text:"")+(model.FileOnly?" | "+owner.text("Normally edited in the config file"):"");
                    name.raycastTarget=true;KitTooltip.Set(name.gameObject,model.Title,native.Entry.Definition+"\n"+model.Info+"\n"+reason);
                    RefreshHelp(reason,displayed);
                }
                catch(Exception error){if(state!=null)state.text=error.GetBaseException().Message;field?.SetUsable(false);toggle?.SetUsable(false);if(cycle!=null)cycle.interactable=false;if(reset!=null)reset.interactable=false;if(raw!=null)raw.interactable=false;if(undo!=null)undo.interactable=false;}
            }
            /// <summary>Usable controls in this bound row (active and interactable), by object name.</summary>
            internal IEnumerable<string> UsableControls()=>rect.GetComponentsInChildren<Selectable>(false).Where(control=>control.gameObject.activeInHierarchy&&control.IsInteractable()).Select(control=>control.name);
            internal bool ServerSet=>model?.ServerSet==true;
            internal string RowKey=>model?.Key??"";
            public void Dispose(){UnregisterFocus();ReleaseEditors();field?.Destroy();field=null;toggle?.Dispose();toggle=null;model=null;}
        }
    }
}
