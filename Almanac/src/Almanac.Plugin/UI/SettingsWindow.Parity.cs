// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Config-manager parity: changed-value highlighting, collapse/expand all and the lock-aware cfg text editor.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Attributes;
using ExtraPotions.Almanac.Display;
using ExtraPotions.Almanac.Editors;
using ExtraPotions.Almanac.Files;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Almanac.Runtime.UI
{
    public sealed partial class SettingsWindow
    {
        static readonly Color ModifiedColor=new Color(1f,0.8f,0.4f),SessionColor=new Color(0.55f,0.9f,0.55f);
        Button? collapseAll;
        string[] selectedSections=Array.Empty<string>();

        ChangeMark Mark(NativeEntry native)
        {
            try
            {
                var entry=native.Entry;
                string current=native.RawSerialized();
                string reset=TomlTypeConverter.ConvertToString(Presentation.ResetTarget(native.Attributes,entry.DefaultValue),entry.SettingType);
                string? before=null;
                if(journal!=null)before=journal.Records.FirstOrDefault(row=>row.Path==entry.ConfigFile.ConfigFilePath&&row.Section==entry.Definition.Section&&row.Key==entry.Definition.Key)?.BeforeSession;
                else if(session.TryGetValue(entry,out var held))before=held;
                return ChangeMarks.Of(current,reset,before);
            }
            catch{return ChangeMark.Default;}
        }
        internal Color MarkColor(NativeEntry native)
        {
            if(preferences?.HighlightChanges.Value==false)return Color.white;
            switch(Mark(native)){case ChangeMark.ChangedThisSession:return SessionColor;case ChangeMark.Modified:return ModifiedColor;default:return Color.white;}
        }
        /// <summary>Text companion to the color, so the state is readable without color vision.</summary>
        internal string MarkText(NativeEntry native)
        {
            if(preferences?.HighlightChanges.Value==false)return "";
            switch(Mark(native)){case ChangeMark.ChangedThisSession:return " · "+text("Changed this session");case ChangeMark.Modified:return " · "+text("Changed from default");default:return "";}
        }

        void BuildCollapseAll(RectTransform panel)
        {
            collapseAll=AlmanacButtons.Create(panel,"CollapseAll",text("Collapse all"),ToggleCollapseAll,Vector2.zero,new Vector2(130,28));
            Help(collapseAll.gameObject,text("Collapse all"),text("Collapse or expand every settings section for the selected mod. Search results remain expanded."));
            window!.Focusable(collapseAll,4,0);
        }
        void ToggleCollapseAll()
        {
            if(selectedSections.Length==0)return;
            if(selectedSections.All(collapsed.Contains))foreach(var key in selectedSections)collapsed.Remove(key);
            else foreach(var key in selectedSections)collapsed.Add(key);
            dirty=true;
        }
        void RefreshCollapseAll(IEnumerable<string> sections)
        {
            selectedSections=sections.ToArray();
            if(collapseAll==null)return;
            collapseAll.gameObject.SetActive(selectedSections.Length>1&&!Searching);
            var caption=collapseAll.GetComponentInChildren<TMP_Text>(true);
            if(caption!=null)caption.text=selectedSections.Length>0&&selectedSections.All(collapsed.Contains)?text("Expand all"):text("Collapse all");
        }

        /// <summary>
        /// Text view of one live file's shown settings. Saving never writes the file as text: each changed value goes
        /// through the same owner writer as the row editors, so locks, hidden settings and validation still apply.
        /// </summary>
        void EditAsText(string path)
        {
            var file=discovery.Index.Find(path);
            if(file==null||discovery.LiveFile(path)==null||!discovery.IsPluginBrowsable(file.Owner?.Guid??"")){SetActionText("This file is not a live configuration.");return;}
            var shown=new Dictionary<string,ConfigEntryBase>(StringComparer.Ordinal);
            var rows=new List<CfgRow>();
            foreach(var entry in discovery.Entries(path))
            {
                try
                {
                    var native=new NativeEntry(entry);
                    if(Hidden(entry)||native.Attributes.Browsable==false||failedDrawers.Contains(entry))continue;
                    string reason=Locked(native,Facts(entry));
                    shown[entry.Definition.Section+"\n"+entry.Definition.Key]=entry;
                    rows.Add(new CfgRow(entry.Definition.Section,entry.Definition.Key,native.RawSerialized(),reason.Length>0?text("Locked")+": "+reason:null));
                }
                catch { } // A failed converter keeps the entry out of the text view.
            }
            if(rows.Count==0){SetActionText("No editable settings in this file.");return;}
            string Validate(string value)
            {
                var plan=CfgText.Plan(rows,CfgText.Parse(value));
                return plan.Errors.Count>0?string.Join("\n",plan.Errors.Take(3)):"";
            }
            string title=text("Edit as text")+": "+Path.GetFileName(path);
            KitAlmanacTextEditor.Open(window!,title,CfgText.Render(rows),Validate,value=>
            {
                var plan=CfgText.Plan(rows,CfgText.Parse(value));
                if(plan.Errors.Count>0)throw new InvalidOperationException(plan.Errors[0]);
                int applied=0,skipped=0;
                foreach(var change in plan.Changes)
                {
                    if(!shown.TryGetValue(change.Section+"\n"+change.Key,out var entry)){skipped++;continue;}
                    var native=new NativeEntry(entry);
                    // Recheck at apply time: authority, visibility and the value can change while the editor is open.
                    if(Hidden(entry)||Locked(native,Facts(entry)).Length>0||native.RawSerialized()!=change.Before){skipped++;continue;}
                    Write(native,change.Value);
                    if(writes.TryGetValue(entry,out var state)&&state.Error)skipped++;else applied++;
                }
                SetActionFormatter(()=>text("Applied")+": "+applied+" · "+text("Skipped")+": "+skipped);dirty=true;
            },false,AlmanacSyntax.Options(SyntaxKind.Cfg,window!,title,false));
        }
    }
}
