// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;
using ExtraPotions.Almanac.Runtime.Localization;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
namespace ExtraPotions.Almanac.Runtime {
    public enum DockSide { Left, Right, Floating }
    sealed class ConfigurationManagerAttributes {
        public bool? Browsable,IsAdvanced;
        public string? DispName;
    }
    public sealed class AlmanacPreferences {
        public readonly ConfigEntry<bool> MenuButtons,ShowAdvanced,RememberWindow,ShowUnboundFiles,ShowEmptyPlugins,WatchFiles,PauseGame,HighlightChanges,SyntaxHighlighting;
        public readonly ConfigEntry<string> PinnedPlugins,DescriptionLines,RestartPhrases,LastPlugin,WindowScale;
        public readonly ConfigEntry<int> JournalSizeKb,BackupCount;
        public readonly ConfigEntry<float> WindowOpacity,DrawerWidth;
        public readonly ConfigEntry<DockSide> DefaultDockPosition;
        public AlmanacPreferences(ConfigFile config){
            MenuButtons=MeadworksConfig.Bind(config,"General","Menu Buttons",true,"Show Mod settings in the main and pause menus.");
            ShowAdvanced=MeadworksConfig.Bind(config,"General","Show Advanced",false,new ConfigDescription("Controlled by the Advanced switch at the top of Almanac. Matching search results may include advanced entries.",null,new ConfigurationManagerAttributes{Browsable=false}));
            RememberWindow=MeadworksConfig.Bind(config,"General","Remember Window",true,"Reopen the settings window on the mod you viewed last.");
            PinnedPlugins=MeadworksConfig.Bind(config,"General","Pinned Plugins","",new ConfigDescription("Pinned mod identifiers, separated by commas. Use Pin or Unpin in the toolbar for ordinary changes; this field allows advanced manual editing.",null,new ConfigurationManagerAttributes{IsAdvanced=true}));
            const string descriptionHelp="Choose whether setting explanations also appear below each row. Hover help remains available in either mode.";
            DescriptionLines=MeadworksConfig.Bind(config,"Display","Description Lines","Tooltip",new ConfigDescription(descriptionHelp,new AcceptableValueList<string>("Tooltip","Inline"),new ConfigurationManagerAttributes{DispName="Setting descriptions"},
                new ConfigPresentation<string>(AlmanacCore.Guid,(_,language)=>new ConfigPresentationText(label:AlmanacText.T("Setting descriptions"),help:AlmanacText.T(descriptionHelp)))));
            ShowUnboundFiles=MeadworksConfig.Bind(config,"Display","Show Unbound Files",true,"List configuration files that no running mod has loaded, as read-only text.");
            ShowEmptyPlugins=MeadworksConfig.Bind(config,"Display","Show Empty Plugins",false,"Show plugins with no visible settings.");
            RestartPhrases=MeadworksConfig.Bind(config,"Changes","Restart Phrases","",new ConfigDescription("Additional phrases indicating a restart is required, separated by commas.",null,new ConfigurationManagerAttributes{IsAdvanced=true}));
            WatchFiles=MeadworksConfig.Bind(config,"Changes","Watch Files On Disk",true,"Report external configuration file changes without overwriting unsaved edits.");
            JournalSizeKb=MeadworksConfig.Bind(config,"Changes","Journal Size KB",1024,new ConfigDescription("Maximum session change journal size in KiB.",new AcceptableValueRange<int>(64,8192),new ConfigurationManagerAttributes{IsAdvanced=true}));
            BackupCount=MeadworksConfig.Bind(config,"Profiles","Backup Count",10,new ConfigDescription("Retain this many backups per file.",new AcceptableValueRange<int>(1,100),new ConfigurationManagerAttributes{IsAdvanced=true}));
            LastPlugin=MeadworksConfig.Bind(config,"Window","Last Plugin","",new ConfigDescription("Remembered selected plugin GUID.",null,new ConfigurationManagerAttributes{Browsable=false}));
            DefaultDockPosition=MeadworksConfig.Bind(config,"Window","Default Dock Position",DockSide.Floating,new ConfigDescription("Retained ConfigDrawers import preference. Almanac currently uses a floating window; this value does not dock it.",null,new ConfigurationManagerAttributes{IsAdvanced=true,DispName="Imported dock preference"}));
            DrawerWidth=MeadworksConfig.Bind(config,"Window","Drawer Width",480f,new ConfigDescription("Width of the mod list in interface canvas units. The settings pane uses the remaining window space.",new AcceptableValueRange<float>(360,720),new ConfigurationManagerAttributes{DispName="Product list width"}));
            PauseGame=MeadworksConfig.Bind(config,"Window","Pause Game",false,"Pause the game while Almanac is open, where the game can pause (single player). Never unpauses a pause Almanac did not make.");
            HighlightChanges=MeadworksConfig.Bind(config,"Display","Highlight Changes",true,"Color and label settings changed from their default or during this session.");
            SyntaxHighlighting=MeadworksConfig.Bind(config,"Display","Syntax Highlighting",true,"Color keys, values, sections and comments in Almanac's text editors for configuration, YAML and JSON. Local to this game: servers never set it. Default: on.");
            WindowOpacity=MeadworksConfig.Bind(config,"Window","Window Opacity",1f,new ConfigDescription("How see-through the settings window is. 1 is fully solid.",new AcceptableValueRange<float>(0.2f,1f)));
            WindowScale=MeadworksConfig.Bind(config,"Window","Window Scale",ExtraPotions.Almanac.Display.WindowScale.Default,new ConfigDescription("Size of the Almanac settings and tools windows, applied as soon as it changes. 100% is the original size; a larger size stays inside the screen. Local to this game: servers never set it. Default: 100%.",new AcceptableValueList<string>(ExtraPotions.Almanac.Display.WindowScale.Presets)));
        }
    }
}
