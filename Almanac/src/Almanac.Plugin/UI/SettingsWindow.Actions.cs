// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Attributes;
using ExtraPotions.Almanac.Index;
using ExtraPotions.Almanac.Runtime.Changes;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Almanac.Runtime.Utilities;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Almanac.Runtime.UI
{
    public sealed partial class SettingsWindow
    {
        ChangeJournal? journal;
        ConfigFileActions? fileActions;
        readonly List<Button> fileButtons=new List<Button>();
        string actionMessage="";
        Func<string>? actionMessageText;
        void SetActionText(string key)=>SetActionFormatter(()=>text(key));
        void SetActionFormatter(Func<string> format){actionMessageText=format;actionMessage=format();}
        void SetActionLiteral(string value){actionMessageText=null;actionMessage=value;}
        bool closeSummary;
        public event Action? QuitRequested;
        Func<ConfigEntryBase,bool>? canChangeOwner,toggleOwner;
        Func<ConfigEntryBase,string>? ownerLabel;
        public void ConfigureOwnership(Func<ConfigEntryBase,bool> canChange,Func<ConfigEntryBase,bool> toggle,Func<ConfigEntryBase,string>? label=null)
        {canChangeOwner=canChange??throw new ArgumentNullException(nameof(canChange));toggleOwner=toggle??throw new ArgumentNullException(nameof(toggle));ownerLabel=label;dirty=true;lastFingerprint="";}
        bool CanChangeOwner(ConfigEntryBase entry)
        {try{return !Hidden(entry)&&new NativeEntry(entry).Attributes.Browsable!=false&&canChangeOwner?.Invoke(entry)==true;}catch{return false;}}
        void ChangeOwner(ConfigEntryBase entry)
        {RunAction(()=>{if(!CanChangeOwner(entry)||toggleOwner?.Invoke(entry)!=true)throw new InvalidOperationException(text("This change was not allowed."));SetActionText("Owner policy submitted");});}
        void CloseWithSummary()
        {
            if(closeSummary||journal==null||journal.RestartCount==0||window?.IsOpen!=true){window?.Close();return;}
            closeSummary=true;
            var dialog=KitAlmanacDialog.Create(window,text("Changes need a restart"),new Vector2(620,320));
            if(dialog==null){closeSummary=false;window.Close();return;}
            string detail=string.Join("\n",journal.Records.Where(row=>row.Changed&&row.Restart==RestartClass.Restart).Take(8).Select(row=>row.Section+" / "+row.Key));
            KitAlmanacDialog.Label(dialog.Panel,"Changes",detail,new Vector2(20,-55),new Vector2(580,190));
            bool quit=false;dialog.Closed+=()=>{closeSummary=false;window?.Close();if(quit)QuitRequested?.Invoke();};
            var later=AlmanacButtons.Create(dialog.Panel,"Later",text("Restart later"),dialog.Dispose,new Vector2(20,-270),new Vector2(260,32));
            var leave=AlmanacButtons.Create(dialog.Panel,"Quit",text("Quit to desktop"),()=>{quit=true;dialog.Dispose();},new Vector2(300,-270),new Vector2(300,32));
            leave.interactable=QuitRequested!=null;dialog.Window.Focusable(later,0,0);dialog.Window.Focusable(leave,1,0);
            Help(later.gameObject,text("Restart later"),text("Keep accepted changes and return to the game. Restart-required changes take effect after restarting."));
            Help(leave.gameObject,text("Quit to desktop"),text("Exit the game after closing this summary so restart-required changes can take effect on the next launch."));
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(later.gameObject);
        }
        public void ConfigureChanges(ChangeJournal value)
        {
            if(journal!=null)journal.Changed-=JournalChanged;
            journal=value??throw new ArgumentNullException(nameof(value));journal.Changed+=JournalChanged;dirty=true;
        }
        public void ConfigureFileActions(ConfigFileActions value)
        {fileActions=value??throw new ArgumentNullException(nameof(value));writer.Written-=OwnWritten;writer.Written+=OwnWritten;dirty=true;}
        void JournalChanged()=>dirty=true;
        void OwnWritten(ConfirmedEntryWrite change)=>Acknowledge(change.Entry);
        void Acknowledge(ConfigEntryBase entry)
        {try{fileActions?.KeepMine(entry.ConfigFile.ConfigFilePath);}catch(Exception error){SetActionLiteral(error.GetBaseException().Message);}dirty=true;}
        bool ChangedInSession(ConfigEntryBase entry)
            =>journal!=null?journal.Records.Any(row=>row.Changed&&row.Path==entry.ConfigFile.ConfigFilePath&&row.Section==entry.Definition.Section&&row.Key==entry.Definition.Key)
                :session.TryGetValue(entry,out var before)&&before!=SafeRaw(entry);
        public bool MatchesCurrentFilter(ConfigEntryBase entry)
        {
            if(entry==null)return false;
            try
            {
                var file=discovery.Index.Find(entry.ConfigFile.ConfigFilePath);
                var group=discovery.Index.Groups.FirstOrDefault(item=>item.Files.Any(current=>current.Path==file?.Path));
                if(group==null||!discovery.IsPluginBrowsable(group.Key)||!discovery.Entries(file!.Path).Any(current=>ReferenceEquals(current,entry)))return false;
                var native=new NativeEntry(entry);var facts=Facts(entry);string reason=Locked(native,facts);
                string label=Presentation.DisplayName(native.Attributes,entry.Definition.Key);
                bool matches=Matches(group.DisplayName+" "+group.Key+" "+entry.Definition.Section+" "+entry.Definition.Key+" "+label+" "+native.Text()+" "+(native.Attributes.Description??entry.Description.Description)+" "+reason);
                if(!matches||Hidden(entry)||native.Attributes.Browsable==false||!InProductScope(group)||!TierShows(Tier(group,entry),native,file,matches))return false;
                bool changed=native.RawSerialized()!=TomlTypeConverter.ConvertToString(Presentation.ResetTarget(native.Attributes,entry.DefaultValue),entry.SettingType);
                return PassFilters(native,facts,changed,reason,group);
            }
            catch{return false;}
        }
        ConfigEntryBase[] SelectedEntries()=>discovery.Index.Groups.Where(group=>group.Key==selected)
            .SelectMany(group=>group.Files).SelectMany(file=>discovery.Entries(file.Path)).Where(MatchesCurrentFilter).ToArray();
        void UndoEntry(NativeEntry native)
        {
            var facts=Facts(native.Entry);if(facts.ConnectedAsClient&&facts.EntrySynced){SetActionText("Server changes are excluded from local undo.");dirty=true;return;}
            if(journal!=null)RunAction(()=>ShowOutcome(journal.Undo(new[]{native.Entry})));
            else if(session.TryGetValue(native.Entry,out var before))Write(native,before);
        }
        void ShowOutcome(ChangeActionResult result)
        {
            int accepted=result.Accepted,pending=result.Pending,skipped=result.Skipped.Count,failed=result.Failed;
            string reason=skipped>0?" · "+result.Skipped[0].Reason:"";
            SetActionFormatter(()=>text("Applied")+": "+accepted+" · "+text("Pending")+": "+pending+" · "+text("Skipped")+": "+skipped+" · "+text("Failed")+": "+failed+reason);dirty=true;
        }
        void RunAction(Action action)
        {try{action();}catch(Exception error){SetActionLiteral(error.GetBaseException().Message);}dirty=true;}
        void ResetSelection(IEnumerable<ConfigEntryBase> entries)
        {
            if(journal==null)return;
            RunAction(()=>
            {
                var selectedEntries=entries.Distinct().Where(entry=>!Hidden(entry)&&new NativeEntry(entry).Attributes.Browsable!=false).ToArray();
                var drawerSkips=selectedEntries.Where(failedDrawers.Contains).ToArray();
                var eligible=journal.PreviewReset(selectedEntries.Where(entry=>!failedDrawers.Contains(entry)));
                var preview=drawerSkips.Length==0?eligible:new ResetPreview(journal,eligible.Items,
                    eligible.Skipped.Concat(drawerSkips.Select(entry=>new ChangeSkip(entry,text("Custom drawer failed")))).ToArray());
                string detail=string.Join("\n",preview.Items.Take(12).Select(item=>item.Entry.Definition+": "+item.Before+" → "+item.Value));
                detail+="\n"+text("Changes")+": "+preview.Items.Count+" · "+text("Skipped")+": "+preview.Skipped.Count;
                detail+="\n"+string.Join("\n",preview.Skipped.Take(4).Select(item=>item.Entry.Definition+": "+item.Reason));
                KitAlmanacTextEditor.Confirm(window!,text("Reset preview"),detail,()=>RunAction(()=>
                {
                    if(preview.Items.Any(item=>failedDrawers.Contains(item.Entry)))throw new InvalidOperationException(text("A custom drawer failed after this preview; preview the reset again."));
                    ShowOutcome(journal.ApplyReset(preview));
                }));
            });
        }
        void FileMenu()
        {
            var group=discovery.Index.Groups.FirstOrDefault(item=>item.Key==selected);
            if(group==null)return;
            var paths=group.Files.Where(file=>discovery.LiveFile(file.Path)!=null).Select(file=>file.Path).ToArray();
            if(paths.Length==0)return;
            KitAlmanacChoice.Open(window!,text("Configuration files"),paths,Array.Empty<string>(),false,chosen=>FileActions(chosen[0]),
                label:Path.GetFileName,help:path=>text("Open actions for this configuration file.")+"\n"+path);
        }
        void FileActions(string path)
        {
            if(fileActions==null)return;
            var choices=new List<string>{"Edit as text","Keep mine","Reset filtered settings","Undo session","Undo all this session"};
            if(fileActions.CanSaveOrReload(path)){choices.Insert(1,"Reload");choices.Insert(3,"Save");}
            if(!discovery.Entries(path).Any(entry=>Facts(entry).ConnectedAsClient))choices.Add("Remove unused");
            var labels=choices.Select(FileActionLabel).ToArray();
            // The chooser callback closes its current popup. Defer opening the next popup to the next UI tick.
            deferredAction=()=>KitAlmanacChoice.Open(window!,text("File actions"),labels,Array.Empty<string>(),false,chosen=>
            {
                int picked=Array.IndexOf(labels,chosen[0]);if(picked<0)return;
                deferredAction=()=>RunAction(()=>
                {
                    switch(choices[picked])
                    {
                        case "Edit as text":EditAsText(path);break;
                        case "Reload":fileActions.Reload(path);SetActionText("Reloaded");break;
                        case "Keep mine":fileActions.KeepMine(path);SetActionText("Kept current values");break;
                        case "Save":fileActions.Save(path);SetActionText("Saved");break;
                        case "Remove unused":
                            var unused=fileActions.Unused(path);
                            KitAlmanacTextEditor.Confirm(window!,text("Remove unused keys"),string.Join("\n",unused.Take(12))+"\n"+unused.Count+" "+text("keys; a backup is retained"),()=>RunAction(()=>{fileActions.RemoveUnused(path);SetActionText("Unused keys removed");}));break;
                        case "Reset filtered settings":ResetSelection(SelectedEntries().Where(entry=>entry.ConfigFile.ConfigFilePath==path));break;
                        case "Undo session":if(journal!=null)ShowOutcome(journal.Undo(SelectedEntries().Where(entry=>entry.ConfigFile.ConfigFilePath==path&&!(Facts(entry).ConnectedAsClient&&Facts(entry).EntrySynced))));break;
                        case "Undo all this session":if(journal!=null)ShowOutcome(journal.Undo(discovery.Index.Files.SelectMany(file=>discovery.Entries(file.Path)).Where(entry=>!Hidden(entry)&&new NativeEntry(entry).Attributes.Browsable!=false&&!(Facts(entry).ConnectedAsClient&&Facts(entry).EntrySynced))));break;
                    }
                });
            });
        }
        string FileActionLabel(string id)
        {
            switch(id)
            {
                case "Edit as text":return text("Edit as text");
                case "Keep mine":return text("Keep mine");
                case "Reset filtered settings":return text("Reset filtered settings");
                case "Undo session":return text("Undo session");
                case "Undo all this session":return text("Undo all this session");
                case "Reload":return text("Reload");
                case "Save":return text("Save");
                case "Remove unused":return text("Remove unused");
                default:return id;
            }
        }
        Action? deferredAction;
        void TickActions()
        {
            var action=deferredAction;deferredAction=null;action?.Invoke();
            if(preferences?.WatchFiles.Value!=false)fileActions?.Tick(IsOpen,Time.unscaledTime);
            if(fileActions==null||heading==null)return;
            var group=discovery.Index.Groups.FirstOrDefault(item=>item.Key==selected);
            if(group?.Files.Any(file=>fileActions.ChangedOnDisk(file.Path))==true)
            {
                // Once per new message: setting dirty every frame forced a full rebuild every frame.
                var message=text("Changed on disk. Choose Reload or Keep mine in Files");
                if(actionMessage!=message){SetActionText("Changed on disk. Choose Reload or Keep mine in Files");lastFingerprint="";dirty=true;}
            }
        }
        public void FocusToolbar(){if(tools.Count>0)UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(tools[0].gameObject);}
    }
}
