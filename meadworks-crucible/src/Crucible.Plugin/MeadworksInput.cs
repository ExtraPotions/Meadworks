using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.EventSystems;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Thin Unity/Valheim adapter for the Core input rules (<see cref="HotkeyRules"/>, <see cref="InputGate"/>,
    /// <see cref="GamepadMap"/>). Shared by every product so typing and window blocking agree suite-wide.
    /// </summary>
    public static class MeadworksInput {
        static readonly List<Func<bool>> typingWindows=new List<Func<bool>>();
        static readonly List<Func<bool>> modWindows=new List<Func<bool>>();
        static readonly Dictionary<ConfigEntry<KeyboardShortcut>,HotkeyChord> chords=new Dictionary<ConfigEntry<KeyboardShortcut>,HotkeyChord>();
        static readonly HashSet<ConfigEntry<KeyboardShortcut>> watched=new HashSet<ConfigEntry<KeyboardShortcut>>();
        static readonly Dictionary<string,KeyCode> keyCodes=new Dictionary<string,KeyCode>(StringComparer.Ordinal);
        static readonly Func<string,bool> down=key=>TryKey(key,out var code)&&Input.GetKeyDown(code);
        // Shift, Ctrl and Alt held only when ZInput and Input both agree (either can stick after Alt+Tab), other keys
        // through Input alone: HotkeyRules.KeyHeld, ValheimModLibs stuck-Alt fix. ZInput is read only for modifiers.
        static readonly Func<string,bool> held=key=>TryKey(key,out var code)&&(HotkeyRules.ReadsThroughGame(key)
            ?HotkeyRules.KeyHeld(key,ZInput.GetKey(code,false),Input.GetKey(code))
            :Input.GetKey(code));

        /// <summary>A mod window with its own text editing; while open no hotkey fires.</summary>
        public static void AddTypingWindow(Func<bool> isOpen){if(isOpen!=null)typingWindows.Add(isOpen);}
        /// <summary>A mod window treated like the game's own windows; while open gameplay keys are blocked.</summary>
        public static void AddModWindow(Func<bool> isOpen){if(isOpen!=null)modWindows.Add(isOpen);}
        public static void RemoveWindow(Func<bool> isOpen){typingWindows.Remove(isOpen);modWindows.Remove(isOpen);}

        public static InputFacts Facts()=>new InputFacts{
            ChatFocused=Chat.instance!=null&&Chat.instance.HasFocus(),
            ConsoleVisible=global::Console.IsVisible(),
            TextInputVisible=TextInput.IsVisible(),
            InputFieldSelected=InputFieldSelected(),
            TypingWindowOpen=AnyOpen(typingWindows),
            ModWindowOpen=AnyOpen(modWindows),
            MenuVisible=Menu.IsVisible(),
            InventoryVisible=InventoryGui.IsVisible(),
            OtherGameUiVisible=StoreGui.IsVisible()||Minimap.IsOpen()||Hud.IsPieceSelectionVisible(),
        };
        public static bool Typing=>Facts().Typing;
        public static bool Allows(InputContext context)=>MeadworksHeadless.AllowsInput&&InputGate.Allows(Facts(),context);

        public static HotkeyChord Chord(KeyboardShortcut shortcut)=>HotkeyChord.TryParse(shortcut.Serialize(),out var chord)?chord:HotkeyChord.None;
        public static HotkeyChord Chord(ConfigEntry<KeyboardShortcut> entry){
            if(chords.TryGetValue(entry,out var chord))return chord;
            if(watched.Add(entry))entry.SettingChanged+=(sender,args)=>chords.Remove(entry);
            chord=Chord(entry.Value);chords[entry]=chord;return chord;
        }
        /// <summary>Shortcut went down this frame under the adopted OpenKeep rules, allowed in <paramref name="context"/>.</summary>
        public static bool Pressed(ConfigEntry<KeyboardShortcut>? entry,InputContext context=InputContext.Gameplay){
            if(entry==null||!MeadworksHeadless.AllowsInput)return false;var chord=Chord(entry);if(chord.IsNone)return false;
            var facts=Facts();return InputGate.Allows(facts,context)&&HotkeyRules.Pressed(chord,facts.Typing,down,held);
        }
        /// <summary>A session-selected chord, including coexistence fallbacks, without binding or saving a second setting.</summary>
        public static bool Pressed(KeyboardShortcut shortcut,InputContext context=InputContext.Gameplay){
            if(!MeadworksHeadless.AllowsInput)return false;var chord=Chord(shortcut);if(chord.IsNone)return false;
            var facts=Facts();return InputGate.Allows(facts,context)&&HotkeyRules.Pressed(chord,facts.Typing,down,held);
        }
        public static bool Held(ConfigEntry<KeyboardShortcut>? entry,InputContext context=InputContext.Gameplay){
            if(entry==null||!MeadworksHeadless.AllowsInput)return false;var chord=Chord(entry);if(chord.IsNone)return false;
            var facts=Facts();return InputGate.Allows(facts,context)&&HotkeyRules.Held(chord,facts.Typing,held);
        }
        public static string Label(ConfigEntry<KeyboardShortcut> entry)=>KeyLabels.Short(Chord(entry));

        /// <summary>Shift held, ZInput and Input agreeing as hotkeys do (kit modifier clicks, KX02 and KX04).</summary>
        public static bool ShiftHeld=>MeadworksHeadless.AllowsInput&&(held("LeftShift")||held("RightShift"));
        /// <summary>Ctrl held, ZInput and Input agreeing as hotkeys do (kit modifier clicks, KX02 and KX04).</summary>
        public static bool CtrlHeld=>MeadworksHeadless.AllowsInput&&(held("LeftControl")||held("RightControl"));

        public static bool GamepadActive=>MeadworksHeadless.AllowsInput&&ZInput.IsGamepadActive();
        public static bool PadDown(PadButton button)=>MeadworksHeadless.AllowsInput&&ZInput.GetButtonDown(GamepadMap.ZInputName(button));
        public static bool PadHeld(PadButton button)=>MeadworksHeadless.AllowsInput&&ZInput.GetButton(GamepadMap.ZInputName(button));
        /// <summary>Consumes a button press so the game does not also act on it (e.g. B closing a pop-up before the inventory).</summary>
        public static void PadConsume(PadButton button)=>ZInput.ResetButtonStatus(GamepadMap.ZInputName(button));

        static bool TryKey(string name,out KeyCode code){
            if(keyCodes.TryGetValue(name,out code))return true;
            if(!Enum.TryParse(name,false,out code))return false;
            keyCodes[name]=code;return true;
        }
        static bool InputFieldSelected(){
            var system=EventSystem.current;var selected=system!=null?system.currentSelectedGameObject:null;
            if(selected==null)return false;
            // TMP_InputField is looked up by name: Crucible does not reference Unity.TextMeshPro.
            return selected.GetComponent<UnityEngine.UI.InputField>()!=null||selected.GetComponent("TMP_InputField")!=null;
        }
        static bool AnyOpen(List<Func<bool>> windows){
            foreach(var open in windows){try{if(open())return true;}catch(Exception){/* a failing check counts as closed, as in the adopted GameWindow */}}
            return false;
        }
    }
}
