// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Meadworks.Crucible;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Almanac.Attributes;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Almanac.Runtime.UI
{
    public sealed partial class SettingsWindow
    {
        string SettingOwner(NativeEntry native)
        {
            var facts=Facts(native.Entry);
            string policy=text(ConfigOwnerPresentation.Phrase(facts));
            var file=discovery.Index.Find(native.Entry.ConfigFile.ConfigFilePath);
            return policy+(file?.Owner!=null?" | "+text("Mod")+": "+file.Owner.Name:"");
        }
        string SettingHelp(NativeEntry native,string description,string current,string reason)
        {
            description=EntryPresentation.Resolve(native,CrucibleUiPlugin.Language,text).Help;
            var lines=new List<string>{string.IsNullOrWhiteSpace(description)?text("This mod did not supply a description."):description};
            var file=discovery.Index.Find(native.Entry.ConfigFile.ConfigFilePath);
            lines.Add(SettingOwner(native));
            lines.Add(text("Current")+": "+current);
            try{lines.Add(text("Default")+": "+BepInEx.Configuration.TomlTypeConverter.ConvertToString(Presentation.ResetTarget(native.Attributes,native.Entry.DefaultValue),native.Entry.SettingType));}catch{ }
            if(native.Shape.HasRange)lines.Add(text("Range")+": "+native.Shape.RangeMin.ToString("G",CultureInfo.InvariantCulture)+" - "+native.Shape.RangeMax.ToString("G",CultureInfo.InvariantCulture));
            if(!string.IsNullOrWhiteSpace(native.Attributes.Unit))lines.Add(text("Unit")+": "+native.Attributes.Unit);
            var facts=Facts(native.Entry);
            if(facts.Known)switch(facts.DeclaredScope)
            {
                case ConfigScope.Local:lines.Add(text("Each player sets this for themselves. It applies on this computer and is saved with your profile."));break;
                case ConfigScope.ServerAuthoritative:lines.Add(text("Set by the host; applies to everyone on the server. Your own value returns when you play alone."));break;
                case ConfigScope.Cheat:lines.Add(text("Cheat set by the host; it works only where the server allows it, otherwise a safe value is used."));break;
            }
            string timing=TimingNote(native,file);if(timing.Length>0)lines.Add(timing);
            string nativeKeys=NativeKeyConflicts.Describe(native.Entry.BoxedValue);
            if(nativeKeys.Length>0)lines.Add(nativeKeys);
            if(!string.IsNullOrWhiteSpace(reason))lines.Add(reason);
            return string.Join("\n",lines);
        }
        void Help(GameObject control,string title,string explanation,string action="")
            =>KitTooltip.Set(control,title,action.Length==0?explanation:action+"\n\n"+explanation);

        string ToolbarHelp(string id)
        {
            switch(id)
            {
                case "profiles":return text("Save, preview, import or restore groups of settings. Applying a profile still checks each mod's permissions.");
                case "convert":return text("Preview settings imported from supported mods before applying them. The producing mod validates each conversion.");
                case "logging":return text("Choose console messages and log detail. These controls affect logging, not gameplay.");
                case "data":return text("Edit data files registered by installed mods. Each mod validates and saves its own files.");
                case "keybinds":return text("Show keyboard shortcuts and shared-key conflicts. This replaces the current filters.");
                case "Filters":return text("Narrow the visible settings. Selected filters must all match; clear every selection to show all settings.");
                case "Pin":return text("Pin or unpin the selected mod at the top of the list. This only changes your local display order.");
                case "Files":return text("Manage the selected mod's configuration files, including reload, backups, reset and text editing. Permissions still apply.");
                default:return "";
            }
        }
        void RefreshToolbarHelp()
        {
            foreach(var button in tools)
            {
                string id=button.name.StartsWith("Tool_",StringComparison.Ordinal)?button.name.Substring(5):button.name;
                string caption=button.GetComponentInChildren<TMPro.TMP_Text>(true)?.text??id;
                if(id=="Pin")
                {
                    bool pinned=(preferences?.PinnedPlugins.Value??"").Split(',').Any(value=>ExtraPotions.Almanac.Index.LegacyPluginIds.Same(value.Trim(),selected));
                    caption=text(pinned?"Unpin":"Pin");
                    var label=button.GetComponentInChildren<TMPro.TMP_Text>(true);if(label!=null)label.text=caption;
                    button.interactable=selected.Length>0;
                }
                if(id=="Filters")
                {
                    caption=text("Filters")+(filters.Count>0?" ("+filters.Count+")":"");
                    var label=button.GetComponentInChildren<TMPro.TMP_Text>(true);if(label!=null)label.text=caption;
                }
                string explanation=ToolbarHelp(id);
                if(id=="Filters"&&filters.Count>0)explanation+="\n"+text("Active filters")+": "+string.Join(", ",filters.Select(FilterLabel));
                Help(button.gameObject,caption,explanation);
            }
        }
        string FilterLabel(string id)
        {
            string[] ids={"changed","session","locked","synced","restart","keybinds","meadworks","errors"};
            string[] names={"Changed from default","Changed this session","Locked","Synced","Restart required","Keybinds","Meadworks","Errors"};
            int index=Array.IndexOf(ids,id);return index<0?id:text(names[index]);
        }

        sealed partial class RowView
        {
            void RefreshHelp(string reason,string displayed)
            {
                var native=model!.Native!;
                string help=owner.SettingHelp(native,model.Info,displayed,reason);
                owner.Help(name!.gameObject,model.Title,help);
                if(description!=null){description.raycastTarget=true;owner.Help(description.gameObject,model.Title,help);}
                if(state!=null){state.raycastTarget=true;owner.Help(state.gameObject,model.Title,help);}
                toggle?.Tooltip(model.Title,help);
                if(field!=null)owner.Help(field.Input.gameObject,model.Title,help,owner.text("Enter a value, then confirm to apply it."));
                if(cycle!=null)owner.Help(cycle.gameObject,model.Title,help,owner.text("Choose the next available value."));
                if(choice!=null)owner.Help(choice.gameObject,model.Title,help,owner.text("Open the full list of choices."));
                if(raw!=null)owner.Help(raw.gameObject,model.Title,help,owner.text("Edit the serialized value as text. Validation and permissions still apply."));
                if(reset!=null)owner.Help(reset.gameObject,owner.text("Reset"),help,owner.text("Restore this setting's default value."));
                if(undo!=null)owner.Help(undo.gameObject,owner.text("Undo"),help,owner.text("Restore this setting's value from before this session. Remote server changes cannot be undone here."));
                if(editor!=null)owner.Help(editor.gameObject,model.Title,help,owner.text("Open the editor for this setting."));
                if(ownership!=null)owner.Help(ownership.gameObject,owner.text("Owner"),help,owner.text("Change who may edit this setting. Only the owning system can grant permission."));
                if(number!=null)
                {
                    owner.Help(number.Field.Input.gameObject,model.Title,help,owner.text("Enter a number, or use the buttons to adjust it within its allowed range."));
                    owner.Help(number.Less.gameObject,model.Title,help,owner.text("Decrease this value."));
                    owner.Help(number.More.gameObject,model.Title,help,owner.text("Increase this value."));
                    if(number.Slider!=null)owner.Help(number.Slider.gameObject,model.Title,help,owner.text("Adjust this value within its allowed range."));
                }
                foreach(var vector in vectors)owner.Help(vector.Input.gameObject,model.Title,help,owner.text("Edit this coordinate. Confirm to apply the complete value."));
                if(drawerRect!=null)foreach(var control in drawerRect.GetComponentsInChildren<Selectable>(true))
                    if(control.GetComponent<UITooltip>() is not UITooltip tooltip||string.IsNullOrWhiteSpace(tooltip.m_text))owner.Help(control.gameObject,model.Title,help);
            }
        }
    }
}
