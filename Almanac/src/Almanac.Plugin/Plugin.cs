// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Coexistence;
using ExtraPotions.Almanac.Runtime.Discovery;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Almanac.Runtime.Locks;
using ExtraPotions.Almanac.Runtime.UI;
using ExtraPotions.Almanac.Runtime.Profiles;
using ExtraPotions.Almanac.Runtime.Input;
using ExtraPotions.Almanac.Runtime.Utilities;
using ExtraPotions.Almanac.Runtime.Changes;
using ExtraPotions.Almanac.Runtime.Localization;
using ExtraPotions.Almanac.Display;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;
using HarmonyLib;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Almanac.Runtime
{
    /// <summary>
    /// Almanac (Runestone): one in-game window for every installed plugin's configuration, sync-lock aware.
    /// Client-optional (roster peer classes): works on any server; major.minor must match only when both peers run Almanac.
    /// Almanac depends only on BepInEx and Crucible; Jotunn, ServerSync, ConditionalConfigSync and other managers are read by name.
    /// Native discovery, typed editing, profiles and product utility pages use the shared kit and owning config routes.
    /// A headless process registers without constructing the discovery, UI or input services.
    /// </summary>
    [BepInPlugin(AlmanacCore.Guid, AlmanacCore.Name, AlmanacCore.Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [BepInDependency(CrucibleUiPlugin.Guid, CrucibleUiPlugin.Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ProductRegistration? registration;
        private MeadworksLog? log;
        private ConfigEntry<KeyboardShortcut>? toggleKey;
        private ConfigEntry<CoexistenceMode>? coexistence;
        private ConfigDiscovery? discovery;
        private LockResolver? locks;
        private SettingsWindow? window;
        private AlmanacPreferences? preferences;
        private ConfigProfiles? profiles;
        private AlmanacInput? input;
        private ToolsWindow? tools;
        private ConfigFileActions? fileActions;
        private IDisposable? drawerConversion;
        private IDisposable? menuSettings;
        private EntryWriter? entryWriter;
        private ChangeJournal? journal;
        private readonly PauseLatch pause=new PauseLatch();
        private float nextDialogText;
        public static ConfigProfiles? Profiles { get; private set; }
        public static SettingsWindow? SettingsWindow { get; private set; }

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        /// <summary>This process may build Almanac UI (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }

        /// <summary>The chord Almanac listens for this session (BepInEx shortcut text); null until decided or on a headless process.</summary>
        public static string? ToggleChord { get; private set; }

        private void Awake()
        {
            log = MeadworksLog.For(Logger, AlmanacCore.Name);
            try
            {
                if (!CruciblePlugin.IsReady)
                    throw new InvalidOperationException("Required Crucible is not ready");
                registration = CruciblePlugin.Services.Register(new ProductMetadata(AlmanacCore.Guid, AlmanacCore.Name,
                    new System.Version(AlmanacCore.Version), new System.Version(0, 1), new System.Version(0, 1),
                    MultiplayerPolicy.Optional, AlmanacCore.Family));
                AllowsUi = MeadworksHeadless.AllowsUi;
                toggleKey = MeadworksConfig.Bind(Config,"General", "Toggle Key", new KeyboardShortcut(KeyCode.None),
                    "Optional shortcut for opening Almanac. No key is bound by default because the game menus provide Mod settings.");
                coexistence = MeadworksConfig.Bind(Config,"Compatibility", "Coexistence", CoexistenceMode.Auto,
                    "Auto: avoid another config manager's F1. AlwaysOwnKey: always use Toggle Key and only warn on a shared key.");
                preferences=new AlmanacPreferences(Config);AlmanacSyntax.Preferences=preferences;
                // A rejected catalog leaves the interface in English; it never blocks Almanac.
                if(AllowsUi)try{AlmanacText.Initialize(Info.Metadata,Logger);}catch(Exception error){log.Warning("Almanac translations unavailable: "+error.GetBaseException().Message);}
                if(AllowsUi){discovery = new ConfigDiscovery(message=>log.Warning(message),log);discovery.Initialize();}
                Ready = true;
                log.Info(AllowsUi ? "Almanac registered."
                    : "Almanac registered on a headless process; no UI is created.");
            }
            catch (Exception error)
            {
                discovery?.Dispose();discovery=null;
                registration?.Dispose();
                registration = null;
                Ready = false;
                log.Error("Almanac initialization rejected: " + error.GetType().Name + ": " + error.Message);
            }
        }

        /// <summary>Runs after every plugin's Awake, so other managers' settings are bound. Connects native discovery, tools and input.</summary>
        private void Start()
        {
            if (!Ready || discovery == null) return;
            try
            {
                discovery.Discover();
                if(!AllowsUi || toggleKey == null || coexistence == null)return;
                locks = new LockResolver();
                profiles=new ConfigProfiles(discovery,locks.Resolve,Paths.ConfigPath,AlmanacCore.Version,Application.version,
                    ()=>preferences?.BackupCount.Value??10,locks.HiddenByServerPolicy,message=>log?.Warning(message));
                Profiles=profiles;
                entryWriter=new EntryWriter(locks.Resolve,message=>log?.Warning(message));
                journal=new ChangeJournal(entryWriter,discovery,locks.Resolve,Paths.ConfigPath,preferences!,log);
                profiles.Written+=journal.RecordConfirmed;
                window = new SettingsWindow(discovery,entryWriter,locks.Resolve,locks.HiddenByServerPolicy,AlmanacText.T,logger:log);
                SettingsWindow=window;
                menuSettings=MeadworksMenuSettings.Register(guid=>Ready&&AllowsUi&&window.CanOpenProduct(guid),guid=>window.OpenProduct(guid));
                window.ConfigurePreferences(preferences!);
                window.ConfigureChanges(journal);
                window.ConfigureOwnership(locks.CanChangeOwnership,locks.ToggleOwnership,entry=>LockResolver.OwnershipLabel(entry)!);
                window.QuitRequested+=QuitFromNativeMenu;
                fileActions=new ConfigFileActions(discovery,locks.Resolve,Paths.ConfigPath,()=>preferences!.BackupCount.Value);
                window.ConfigureFileActions(fileActions);
                tools=new ToolsWindow(profiles,discovery,()=>window.SelectedPlugin,window.MatchesCurrentFilter,AlmanacText.T);
                tools.Scale=()=>WindowScale.Factor(preferences!.WindowScale.Value);
                window.ToolRequested+=tool=>tools.Open(tool);
                drawerConversion=new ConfigDrawersPreferences(preferences!.DefaultDockPosition,preferences.DrawerWidth,preferences.WindowOpacity,preferences.ShowAdvanced,preferences.WatchFiles,locks.Resolve,entryWriter).Register();
                bool OwnsWindow(KitWindow candidate)=>ReferenceEquals(candidate,window.Window)||ReferenceEquals(candidate,tools.Window)
                    ||candidate.Panel!=null&&(window.Window?.Panel!=null&&candidate.Panel.IsChildOf(window.Window.Panel)||tools.Window?.Panel!=null&&candidate.Panel.IsChildOf(tools.Window.Panel));
                bool ModalOpen()=>KitAlmanacDialog.Active!=null&&OwnsWindow(KitAlmanacDialog.Active.Window);
                input=new AlmanacInput(toggleKey,coexistence,new AlmanacInputActions{
                    Open=window.Open,Close=()=>{tools.Close();window.Close();},IsOpen=()=>window.IsOpen||tools.IsOpen,Window=()=>ModalOpen()?KitAlmanacDialog.Active.Window:tools.IsOpen?tools.Window:window.Window,OwnsWindow=OwnsWindow,
                    Available=()=>Ready&&AllowsUi,MenuButtons=()=>preferences!.MenuButtons.Value,
                    CloseTopmost=()=>{if(KitAlmanacDialog.CloseTopmost())return true;if(tools.IsOpen){tools.Close();return true;}return window.CloseTopmost();},
                    FocusPane=pane=>{if(ModalOpen())return;if(tools.IsOpen)tools.FocusToolbar();else window.FocusPane(pane);},FocusSearch=()=>{if(ModalOpen())return;if(tools.IsOpen)tools.FocusSearch();else window.FocusSearch();},ResetFocused=()=>{if(!ModalOpen()&&!tools.IsOpen)window.ResetFocused();},
                    Toolbar=()=>{if(ModalOpen())return;if(tools.IsOpen)tools.FocusToolbar();else window.FocusToolbar();},NudgeFocused=value=>{if(!ModalOpen()&&!tools.IsOpen)window.NudgeFocused(value);},FocusMoved=()=>{if(!tools.IsOpen)window.FocusMoved();},ActivateFocused=()=>!ModalOpen()&&!tools.IsOpen&&window.ActivateFocused(),
                    NavigateRow=delta=>!ModalOpen()&&(tools.IsOpen?tools.NavigateRow(delta):window.NavigateRow(delta)),
                    Notice=message=>log?.Info(message),Text=AlmanacText.T
                });
                ToggleChord=input.EffectiveChord;
                input.ManagerClosed+=manager=>window.Refresh();
            }
            catch (Exception error)
            {
                ToggleChord = null;
                log?.Warning("Almanac interface initialization failed: " + error);
            }
        }

        private void Update()
        {
            if(!Ready)return;
            if(window==null){discovery?.Tick(false);return;}
            journal?.Tick();AlmanacText.Tick();
            window.Tick();
            tools?.Tick();input?.Tick();ToggleChord=input?.EffectiveChord;
            TickParity();
            if(preferences?.WatchFiles.Value==true)fileActions?.Tick(window.IsOpen,UnityEngine.Time.unscaledTime);
        }

        /// <summary>Pause-while-open and kit dialog captions.</summary>
        private void TickParity()
        {
            bool open=window!.IsOpen||tools?.IsOpen==true;
            try
            {
                bool wanted=open&&preferences?.PauseGame.Value==true&&Player.m_localPlayer!=null;
                Apply(pause.Tick(wanted,Game.m_pause,Menu.IsVisible()));
            }
            catch(Exception error){log?.Warning("Almanac pause handling failed: "+error.GetBaseException().Message);}
            if(open&&Time.unscaledTime>=nextDialogText)
            {
                nextDialogText=Time.unscaledTime+0.25f;
                var dialog=KitAlmanacDialog.Active;
                if(dialog?.Panel!=null&&(OwnedBy(dialog,window.Window)||OwnedBy(dialog,tools?.Window)))AlmanacText.LocalizeDialog(dialog.Panel);
            }
        }

        private static bool OwnedBy(KitAlmanacDialog dialog,KitWindow? owner)
            =>owner?.Panel!=null&&(ReferenceEquals(dialog.Window,owner)||dialog.Panel.IsChildOf(owner.Panel));

        private static void Apply(PauseAction action)
        {
            if(action==PauseAction.Pause)Game.Pause();
            else if(action==PauseAction.Unpause)Game.Unpause();
        }

        private void QuitFromNativeMenu()
        {
            // Explicit restart-summary action follows the game's current quit/storage checks.
            if(Menu.instance!=null){AccessTools.Method(typeof(Menu),"OnQuitYes",Type.EmptyTypes)?.Invoke(Menu.instance,null);return;}
            if(FejdStartup.instance!=null)AccessTools.Method(typeof(FejdStartup),"OnAbort",Type.EmptyTypes)?.Invoke(FejdStartup.instance,null);
        }

        private void OnDestroy()
        {
            menuSettings?.Dispose();menuSettings=null;
            Ready = false;
            try{Apply(pause.Release(Game.m_pause,Menu.IsVisible()));}catch { }
            AlmanacText.Shutdown();
            input?.Dispose();input=null;drawerConversion?.Dispose();drawerConversion=null;tools?.Dispose();tools=null;fileActions=null;
            SettingsWindow=null;window?.Dispose();window=null;
            if(profiles!=null&&journal!=null)profiles.Written-=journal.RecordConfirmed;
            journal?.Dispose();journal=null;entryWriter=null;
            Profiles=null;profiles=null;
            locks?.Dispose();locks=null;discovery?.Dispose();discovery=null;
            registration?.Dispose();
            registration = null;
        }
    }
}
