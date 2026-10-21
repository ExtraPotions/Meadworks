// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Product glue over existing C07, KitWindow/GridNavigator and KitTextField native input APIs.
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Coexistence;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using GUIFramework;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Almanac.Runtime.Input
{
    public sealed class AlmanacInputActions
    {
        public Action Open { get; set; }=null!;
        public Action Close { get; set; }=null!;
        public Func<bool> IsOpen { get; set; }=null!;
        public Func<KitWindow?> Window { get; set; }=null!;
        public Func<KitWindow,bool>? OwnsWindow { get; set; }
        public Func<bool>? Available { get; set; }
        public Func<bool>? MenuButtons { get; set; }
        public Func<bool>? CloseTopmost { get; set; }
        public Action<int>? FocusPane { get; set; }
        public Action? FocusSearch { get; set; }
        public Action? ResetFocused { get; set; }
        public Action? Toolbar { get; set; }
        public Action<int>? NudgeFocused { get; set; }
        public Action? FocusMoved { get; set; }
        public Func<int,bool>? NavigateRow { get; set; }
        public Func<bool>? ActivateFocused { get; set; }
        public Action<string>? Notice { get; set; }
        public Func<string,string>? Text { get; set; }
    }
    public sealed class ManagerWindowState
    {
        internal ManagerWindowState(string guid,string name,bool? open){Guid=guid;Name=name;Open=open;}
        public string Guid { get; }
        public string Name { get; }
        /// <summary>Null means unavailable; never treated as a verified close.</summary>
        public bool? Open { get; }
    }
    /// <summary>A14-A16: session shortcut, read-only manager awareness, menu entry and controller actions.</summary>
    public sealed class AlmanacInput : IDisposable
    {
        static AlmanacInput? active;
        static readonly MethodInfo PadMethod=AccessTools.Method(typeof(KitWindow),"Pad",new[]{typeof(PadButton)});
        static readonly FieldInfo ControlsField=AccessTools.Field(typeof(KitWindow),"controls"),FocusField=AccessTools.Field(typeof(KitWindow),"focus");
        static readonly PadButton[] Directions={PadButton.DPadUp,PadButton.DPadDown,PadButton.DPadLeft,PadButton.DPadRight};
        readonly ConfigEntry<KeyboardShortcut> toggle;
        readonly ConfigEntry<CoexistenceMode> coexistence;
        readonly AlmanacInputActions actions;
        readonly Harmony harmony=new Harmony(AlmanacCore.Guid+".input");
        readonly AlmanacMenus menus;
        readonly HashSet<string> notices=new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string,bool?> managerStates=new Dictionary<string,bool?>(StringComparer.Ordinal);
        readonly Dictionary<string,object> managerInstances=new Dictionary<string,object>(StringComparer.Ordinal);
        KeyboardShortcut sessionKey=new KeyboardShortcut(KeyCode.None);
        float nextPoll;
        int padFrame=-1,cancelFrame=-1;
        bool disposed,explicitBinding;
        public AlmanacInput(ConfigEntry<KeyboardShortcut> toggle,ConfigEntry<CoexistenceMode> coexistence,AlmanacInputActions actions)
        {
            this.toggle=toggle??throw new ArgumentNullException(nameof(toggle));this.coexistence=coexistence??throw new ArgumentNullException(nameof(coexistence));
            this.actions=actions??throw new ArgumentNullException(nameof(actions));
            if(actions.Open==null||actions.Close==null||actions.IsOpen==null||actions.Window==null)throw new ArgumentException("Window callbacks are required.",nameof(actions));
            menus=new AlmanacMenus(ToggleOpen,()=>T("Mod settings"));
            if(!MeadworksHeadless.AllowsUi)return;
            if(active!=null)throw new InvalidOperationException("Almanac input is already installed.");
            active=this;
            try
            {
                toggle.SettingChanged+=BindingChanged;coexistence.SettingChanged+=BindingChanged;DecideKey();
                Patch(typeof(KitWindow),"Update",nameof(WindowInput));
                Patch(typeof(FejdStartup),"UpdateKeyboard",nameof(MainMenuInput));
                Patch(typeof(Menu),"Update",nameof(PauseMenuInput));
            }
            catch{Dispose();throw;}
        }
        public string EffectiveChord=>sessionKey.Serialize();
        public IReadOnlyList<ManagerWindowState> Managers { get; private set; }=Array.Empty<ManagerWindowState>();
        public event Action<ManagerWindowState>? ManagerClosed;
        bool Available=>!disposed&&MeadworksHeadless.AllowsInput&&(actions.Available?.Invoke()??true);
        bool Open=>Available&&actions.IsOpen();
        string T(string value)=>actions.Text?.Invoke(value)??value;
        void Patch(Type type,string method,string prefix)
        {harmony.Patch(AccessTools.Method(type,method),prefix:new HarmonyMethod(typeof(AlmanacInput),prefix){priority=Priority.First});}
        public void Tick()
        {
            if(!Available)return;
            menus.Tick(actions.MenuButtons?.Invoke()??true);
            if(Time.unscaledTime>=nextPoll){nextPoll=Time.unscaledTime+0.5f;DecideKey();PollManagers();}
            if(MeadworksInput.Pressed(sessionKey,InputContext.Global))ToggleOpen();
            CancelIfDown();Pad();
        }
        void BindingChanged(object sender,EventArgs args)
        {if(disposed)return;if(ReferenceEquals(sender,toggle))explicitBinding=true;DecideKey();}
        /// <summary>Call when the player explicitly confirms even an unchanged F1; ConfigEntry does not raise an event for a same-value edit.</summary>
        public void UseConfiguredKey(){if(disposed)return;explicitBinding=true;DecideKey();}
        void DecideKey()
        {
            var decision=HotkeyPolicy.Decide(toggle.Value.Serialize(),explicitBinding?CoexistenceMode.AlwaysOwnKey:coexistence.Value,HotkeyCoexistence.InstalledManagers());
            sessionKey=KeyboardShortcut.Deserialize(decision.Chord);
            if(decision.NoticeText!=null)Once(decision.NoticeText);
            if(decision.CollisionWith!=null)Once("Almanac's key "+decision.Chord+" is also used by "+decision.CollisionWith+".");
        }
        void Once(string message){if(notices.Add(message))actions.Notice?.Invoke(message);}
        public void ToggleOpen()
        {
            if(!Available)return;
            if(actions.IsOpen())actions.Close();else actions.Open();
            var window=actions.Window();if(window?.Panel!=null&&window.IsOpen)window.Panel.SetAsLastSibling();
        }
        bool Own(KitWindow window)=>Available&&(actions.OwnsWindow?.Invoke(window)??ReferenceEquals(window,actions.Window()));
        bool SelectedHere(GameObject selected)=>selected!=null&&actions.Window()?.Panel is RectTransform panel&&selected.transform.IsChildOf(panel);
        void CancelIfDown()
        {
            if(!Open||cancelFrame==Time.frameCount||MeadworksInput.Typing||KitTextField.Typing)return;
            bool pad=MeadworksInput.GamepadActive&&MeadworksInput.PadDown(PadButton.B);
            if(!pad&&!ZInput.GetKeyDown(KeyCode.Escape))return;
            cancelFrame=Time.frameCount;if(pad)MeadworksInput.PadConsume(PadButton.B);
            if(actions.CloseTopmost?.Invoke()!=true)actions.Close();
        }
        void Pad()
        {
            if(!Open||!MeadworksInput.GamepadActive||padFrame==Time.frameCount)return;
            padFrame=Time.frameCount;CancelIfDown();if(!Open||MeadworksInput.Typing||KitTextField.Typing)return;
            bool Down(PadButton button)=>MeadworksInput.PadDown(button);
            void Consume(PadButton button)=>MeadworksInput.PadConsume(button);
            if(Down(PadButton.LeftBumper)){Consume(PadButton.LeftBumper);actions.FocusPane?.Invoke(0);return;}
            if(Down(PadButton.RightBumper)){Consume(PadButton.RightBumper);actions.FocusPane?.Invoke(1);return;}
            if(Down(PadButton.Y)){Consume(PadButton.Y);actions.FocusSearch?.Invoke();return;}
            if(Down(PadButton.X)){Consume(PadButton.X);actions.ResetFocused?.Invoke();return;}
            if(Down(PadButton.Start)){Consume(PadButton.Start);actions.Toolbar?.Invoke();return;}
            if(Down(PadButton.LeftTrigger)){Consume(PadButton.LeftTrigger);actions.NudgeFocused?.Invoke(-10);return;}
            if(Down(PadButton.RightTrigger)){Consume(PadButton.RightTrigger);actions.NudgeFocused?.Invoke(10);return;}
            if(Down(PadButton.A)){Consume(PadButton.A);Activate();return;}
            foreach(var direction in Directions)
            {
                string stick=direction==PadButton.DPadUp?"JoyLStickUp":direction==PadButton.DPadDown?"JoyLStickDown":direction==PadButton.DPadLeft?"JoyLStickLeft":"JoyLStickRight";
                if(!Down(direction)&&!ZInput.GetButtonDown(stick))continue;
                Consume(direction);ZInput.ResetButtonStatus(stick);
                var selected=EventSystem.current?.currentSelectedGameObject;
                if(SelectedHere(selected!)&&selected!.GetComponent<Slider>()!=null&&(direction==PadButton.DPadLeft||direction==PadButton.DPadRight))
                    actions.NudgeFocused?.Invoke(direction==PadButton.DPadLeft?-1:1);
                else
                {
                    if((direction==PadButton.DPadUp||direction==PadButton.DPadDown)&&actions.NavigateRow?.Invoke(direction==PadButton.DPadUp?-1:1)==true)return;
                    // Reuse the kit's existing focus/GridNavigator algorithm; no second focus model.
                    SyncSelectedFocus();
                    PadMethod.Invoke(actions.Window(),new object[]{direction});actions.FocusMoved?.Invoke();
                }
                return;
            }
        }
        void SyncSelectedFocus()
        {
            var window=actions.Window();var selected=EventSystem.current?.currentSelectedGameObject;
            if(window==null||!SelectedHere(selected!))return;
            var control=selected!.GetComponent<Selectable>()??selected.GetComponentInParent<Selectable>();
            if(control==null||!(ControlsField.GetValue(window) is Dictionary<GridCell,Selectable> controls))return;
            // Pane/search callbacks and pointer clicks select a native control. Align the kit's existing
            // cursor to that registered cell before it computes the next step.
            foreach(var pair in controls)if(pair.Value==control){FocusField.SetValue(window,(GridCell?)pair.Key);break;}
        }
        void Activate()
        {
            if(actions.ActivateFocused?.Invoke()==true)return;
            GameObject selected=EventSystem.current?.currentSelectedGameObject!;if(!SelectedHere(selected))return;
            if(selected.GetComponent<Button>() is Button button&&button.IsInteractable()){button.onClick.Invoke();return;}
            if(selected.GetComponent<GuiInputField>() is GuiInputField input&&input.IsInteractable())
            {
                bool opened=false;
                try{opened=input.OpenKeyboard();}catch{input.ActivateInputField();}
                if(!opened)Once(KitText.Text(KitText.KeyboardUnavailable,MeadworksTranslations.GameLanguage()));
                return;
            }
            if(EventSystem.current!=null)ExecuteEvents.Execute(selected,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
        }
        void PollManagers()
        {
            var current=new List<ManagerWindowState>();
            foreach(var manager in KnownManagers.All)
            {
                if(!Chainloader.PluginInfos.TryGetValue(manager.Guid,out var info)||info.Instance==null)continue;
                if(!managerInstances.TryGetValue(manager.Guid,out var previousInstance)||!ReferenceEquals(previousInstance,info.Instance))
                {managerStates.Remove(manager.Guid);managerInstances[manager.Guid]=info.Instance;}
                bool? open=null;
                try
                {
                    // ConfigDrawers 1.1.1 exposes static IsOpen; CM and its fork expose DisplayingWindow.
                    string property=manager.Guid=="vapok.bepinex.configdrawers"?"IsOpen":"DisplayingWindow";
                    var member=info.Instance.GetType().GetProperty(property,BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static);
                    if(member?.PropertyType==typeof(bool)&&member.GetGetMethod()!=null)open=(bool)member.GetValue(member.GetGetMethod()!.IsStatic?null:info.Instance,null);
                }
                catch{open=null;}
                var state=new ManagerWindowState(manager.Guid,info.Metadata?.Name??manager.Name,open);current.Add(state);
                if(managerStates.TryGetValue(manager.Guid,out var previous)&&previous==true&&open==false)ManagerClosed?.Invoke(state);
                managerStates[manager.Guid]=open;
            }
            foreach(var guid in new List<string>(managerStates.Keys))
                if(!current.Exists(state=>state.Guid==guid)){managerStates.Remove(guid);managerInstances.Remove(guid);}
            Managers=current.AsReadOnly();
        }
        static bool WindowInput(KitWindow __instance)
        {
            var owner=active;if(owner==null||!owner.Own(__instance)||!__instance.IsOpen)return true;
            if(ReferenceEquals(__instance,owner.actions.Window())){owner.CancelIfDown();owner.Pad();}return false;
        }
        static bool MainMenuInput()
        {var owner=active;if(owner==null)return true;owner.CancelIfDown();return !owner.Open&&owner.cancelFrame!=Time.frameCount;}
        static bool PauseMenuInput(Menu __instance)
        {
            var owner=active;if(owner==null)return true;owner.CancelIfDown();
            if(!owner.Open&&owner.cancelFrame!=Time.frameCount)return true;
            // Skipping Menu.Update blocks its Esc handling, but Menu.IsVisible() reads these counters: keep vanilla's bookkeeping.
            if(__instance.m_root.gameObject.activeSelf){__instance.m_hiddenFrames=0;__instance.m_shownFrames++;}
            else{__instance.m_shownFrames=0;__instance.m_hiddenFrames++;}
            return false;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            toggle.SettingChanged-=BindingChanged;coexistence.SettingChanged-=BindingChanged;
            if(ReferenceEquals(active,this))active=null;
            harmony.UnpatchSelf();menus.Dispose();ManagerClosed=null;
        }
    }
}
