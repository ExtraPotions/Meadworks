// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using TMPro;
using UnityEngine.UI;

namespace ExtraPotions.Almanac.Runtime.UI
{
    public sealed partial class SettingsWindow
    {
        sealed partial class RowView
        {
            void RefreshLocalizedActions()
            {
                owner.LocalizedCaption(raw,"Text");
                owner.LocalizedCaption(reset,model?.Header==true?"Reset section":"Reset");
                owner.LocalizedCaption(undo,"Undo");
                owner.LocalizedCaption(editor,"Edit...");
                toggle?.SetLabels(owner.text("On"),owner.text("Off"));
            }
        }
        void LocalizedCaption(Button? button,string key)
        {
            var label=button?.GetComponentInChildren<TMP_Text>(true);
            if(label!=null)label.text=text(key);
        }
        // The controls keep their identity, listeners and drafts; only cached presentation changes.
        void RefreshLocalizedChrome()
        {
            if(actionMessageText!=null)actionMessage=actionMessageText();
            LocalizedCaption(close,"Close");
            if(close!=null)Help(close.gameObject,text("Close"),text("Close the settings window. Accepted changes are already applied; changes requiring a restart are summarized first."));
            advanced?.SetLabels(text("Advanced on"),text("Advanced off"));
            advanced?.Tooltip(text("Advanced"),text("Show specialist settings. Search can still find matching advanced settings when this switch is off."));
            if(search!=null)
            {
                if(search.Input.placeholder is TMP_Text hint)hint.text=text("Search settings");
                Help(search.Input.gameObject,text("Search settings"),text("Match all search words, quote an exact phrase, or exclude a word with a leading minus sign."));
            }
            foreach(var button in tools)
            {
                if(button==null)continue;
                string id=button.name.StartsWith("Tool_",StringComparison.Ordinal)?button.name.Substring(5):button.name;
                switch(id)
                {
                    case "profiles":LocalizedCaption(button,"Profiles");break;
                    case "convert":LocalizedCaption(button,"Convert");break;
                    case "logging":LocalizedCaption(button,"Logging");break;
                    case "data":LocalizedCaption(button,"Data");break;
                    case "keybinds":LocalizedCaption(button,"Keybinds");break;
                    case "Files":LocalizedCaption(button,"Files");break;
                }
            }
            RefreshToolbarHelp();
            RefreshCollapseAll(selectedSections);
        }
    }
}
