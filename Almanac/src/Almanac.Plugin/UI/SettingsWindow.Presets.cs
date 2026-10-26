// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Index;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Almanac.Runtime.UI {
    public sealed partial class SettingsWindow {
        string PresetScope(AlmanacPresetChoice choice)=>text(choice.Scope==ConfigScope.Local?"Preset scope: Local":"Preset scope: Host");
        string PresetSummary(AlmanacPresetChoice choice)=>string.Format(text("Preset owner: {0}"),discovery.Index.Groups.FirstOrDefault(group=>group.Key==choice.Product)?.DisplayName??choice.Product)+" · "+PresetScope(choice);
        string PresetHelp(AlmanacPresetChoice choice)=>PresetSummary(choice)+"\n"+text("Preview every listed change before applying. Only these settings are written; other preferences stay saved.");
        void AddPresetRows(IndexGroup group,List<RowModel> rows,StringBuilder fingerprint){
            foreach(var choice in AlmanacPresetCatalog.For(group.Key)){
                string label=text(choice.Label),help=PresetHelp(choice);
                if(!Matches(group.DisplayName+" "+label+" "+help))continue;
                string refusal=PresetAvailability(choice);
                rows.Add(new RowModel{Key="preset:"+choice.Id,Title=label,Info=help,Reason=refusal,Preset=choice});
                fingerprint.Append(choice.Id).Append(label).Append(help).Append(refusal);
            }
        }
        void BindPreset(RectTransform rect,RowModel row){
            UnbindFeature(rect);var choice=row.Preset!;
            var button=AlmanacButtons.Create(rect,"Preset_"+choice.Id,row.Title,()=>OpenPreset(choice),Vector2.zero,new Vector2(Mathf.Max(100,rect.rect.width-16),32));
            string refusal=PresetAvailability(choice);
            string help=refusal.Length>0?refusal:row.Info;
            button.interactable=refusal.Length==0;Help(button.gameObject,row.Title,help);
            Label(rect,"PresetHelp",refusal.Length>0?help:PresetSummary(choice),new Vector2(8,-36),new Vector2(Mathf.Max(100,rect.rect.width-16),36),14);
            window!.Focusable(button,1,row.Index+2);
        }
        string PresetAvailability(AlmanacPresetChoice choice){
            if(choice.Scope==ConfigScope.ServerAuthoritative&&(!CruciblePlugin.IsReady||!CruciblePlugin.Session.IsAuthority||!CruciblePlugin.Session.AuthorityReady))
                return text("Host presets must be applied on the owning host.");
            try{choice.Preview(PresetEntries(choice.Product));return "";}
            catch(Exception error){return PresetFailureText(error);}
        }
        ConfigEntryBase[] PresetEntries(string product)=>discovery.Index.Groups.Where(group=>group.Key==product&&discovery.IsPluginBrowsable(group.Key))
            .SelectMany(group=>group.Files).SelectMany(file=>discovery.Entries(file.Path))
            .Where(entry=>!Hidden(entry)&&new NativeEntry(entry).Attributes.Browsable!=false).ToArray();
        void PresetFailed(Exception error){
            log?.Warning("Preset operation refused or failed: "+error);
            SetActionLiteral(PresetFailureText(error));
            dirty=true;
        }
        string PresetFailureText(Exception error){
            if(error is PresetBindingException binding){
                if(binding.Failure==PresetBindingFailure.Missing)return text("Preset unavailable: a required setting is missing.");
                if(binding.Failure==PresetBindingFailure.Ambiguous)return text("Preset unavailable: a setting has multiple bindings.");
                return text("Preset unavailable: a binding owner, file or scope does not match.");
            }
            return text(error is ConfigPresetException failure&&failure.Failure==ConfigPresetFailure.HostOnly
                ?"Host presets must be applied on the owning host.":"Preset could not be applied. Refresh the preview.");
        }
        void OpenPreset(AlmanacPresetChoice choice){
            if(!IsOpen)return;
            try{
                var preview=choice.Preview(PresetEntries(choice.Product));
                var parent=window;string selection=selected,filterAtPreview=productFilter;
                string detail=PresetHelp(choice)+"\n\n";
                detail+=string.Join("\n",preview.Rows.Select(row=>{
                    var display=EntryPresentation.Resolve(new NativeEntry(row.Entry),presentationLanguage,text);
                    return display.Label+": "+PresetValue(row.Entry,row.Before)+" → "+PresetValue(row.Entry,row.After);
                }));
                KitAlmanacTextEditor.Confirm(window!,text(choice.Label),detail,()=>{
                    try{
                        var live=PresetEntries(choice.Product);
                        if(disposed||!IsOpen||!ReferenceEquals(parent,window)||selected!=selection||productFilter!=filterAtPreview
                            ||preview.Rows.Any(row=>!live.Any(entry=>ReferenceEquals(entry,row.Entry))))
                            throw new InvalidOperationException("Preset page or live entries changed after preview.");
                        var result=preview.Apply();
                        SetActionText(result.Synchronized&&result.Verified?"Preset applied and saved.":"Preset published; verification or synchronization failed.");
                        if(result.CompletionError!=null)log?.Warning("Preset published but completion failed: "+result.CompletionError);
                        foreach(var row in preview.Rows)Acknowledge(row.Entry);
                        foreach(string path in preview.Rows.Select(row=>row.Entry.ConfigFile.ConfigFilePath).Distinct())discovery.Refresh(path);
                        dirty=true;lastFingerprint="";
                    }catch(Exception error){PresetFailed(error);}
                });
            }catch(Exception error){PresetFailed(error);}
        }
        string PresetValue(ConfigEntryBase entry,string serialized){
            if(entry.SettingType==typeof(bool)&&bool.TryParse(serialized,out var value))return text(value?"On":"Off");
            return text(serialized);
        }
    }
}
