// Adapted for Crucible from OpenKeep ValheimModLibs/Hotkeys/Hotkey.cs and KeyNames.cs
// (https://github.com/geraldjglasgow/ValheimMods, revision 31d8ee35ac4ccfd14c83aa930e4b602ac4dfa230).
// OpenKeep - a Valheim mod. Copyright (C) 2026 Gerald Glasgow.
// Modifications Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; see licenses/GPL-3.0.txt and docs/third-party-notices.md.
// Change: the Unity-bound rules are restated over key names so they are testable without Unity.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// A keyboard shortcut as BepInEx serialises <c>KeyboardShortcut</c>: main key first, then modifiers, joined by "+".
    /// Key names are Unity <c>KeyCode</c> names, canonicalised; "Ctrl", "Shift", "Alt" and bare digits are accepted aliases.
    /// </summary>
    public sealed class HotkeyChord:IEquatable<HotkeyChord> {
        public static HotkeyChord None { get; }=new HotkeyChord("None",Array.Empty<string>());
        public string Main { get; }
        public IReadOnlyList<string> Modifiers { get; }
        public bool IsNone=>Main=="None";
        HotkeyChord(string main,string[] modifiers){Main=main;Modifiers=new ReadOnlyCollection<string>(modifiers);}

        public static HotkeyChord Parse(string? text){
            if(string.IsNullOrWhiteSpace(text))return None;
            var parts=text!.Split('+').Select(p=>p.Trim()).ToArray();
            if(parts.Any(p=>p.Length==0))throw new FormatException("Empty key in shortcut: "+text);
            var keys=new string[parts.Length];
            for(int i=0;i<parts.Length;i++)if(!TryCanonical(parts[i],out keys[i]))throw new FormatException("Unknown key: "+parts[i]);
            if(keys[0]=="None"){if(keys.Length>1)throw new FormatException("A shortcut without a main key cannot have modifiers.");return None;}
            if(keys.Skip(1).Contains("None"))throw new FormatException("None is not a modifier.");
            if(keys.Distinct(StringComparer.Ordinal).Count()!=keys.Length)throw new FormatException("Repeated key in shortcut: "+text);
            return new HotkeyChord(keys[0],keys.Skip(1).ToArray());
        }
        public static bool TryParse(string? text,out HotkeyChord chord){try{chord=Parse(text);return true;}catch(FormatException){chord=None;return false;}}
        public override string ToString()=>IsNone?"None":string.Join(" + ",new[]{Main}.Concat(Modifiers));

        public bool Equals(HotkeyChord? other)=>other!=null&&Main==other.Main&&Modifiers.Count==other.Modifiers.Count&&!Modifiers.Except(other.Modifiers,StringComparer.Ordinal).Any();
        public override bool Equals(object? obj)=>Equals(obj as HotkeyChord);
        public override int GetHashCode(){int hash=StringComparer.Ordinal.GetHashCode(Main);foreach(var modifier in Modifiers)hash^=StringComparer.Ordinal.GetHashCode(modifier)*31;return hash;}
        public static bool operator==(HotkeyChord? left,HotkeyChord? right)=>ReferenceEquals(left,right)||left is not null&&left.Equals(right);
        public static bool operator!=(HotkeyChord? left,HotkeyChord? right)=>!(left==right);

        static readonly Dictionary<string,string> Known=BuildKnown();
        public static bool IsKnownKey(string? name)=>name!=null&&Known.ContainsKey(name.Trim());
        static bool TryCanonical(string name,out string canonical){
            if(Known.TryGetValue(name,out canonical!))return true;
            canonical="";return false;
        }
        static Dictionary<string,string> BuildKnown(){
            var names=new List<string>{"None","Backspace","Delete","Tab","Clear","Return","Pause","Escape","Space","UpArrow","DownArrow","RightArrow","LeftArrow","Insert","Home","End","PageUp","PageDown",
                "Exclaim","DoubleQuote","Hash","Dollar","Percent","Ampersand","Quote","LeftParen","RightParen","Asterisk","Plus","Comma","Minus","Period","Slash","Colon","Semicolon","Less","Equals","Greater","Question","At",
                "LeftBracket","Backslash","RightBracket","Caret","Underscore","BackQuote","LeftCurlyBracket","Pipe","RightCurlyBracket","Tilde","Numlock","CapsLock","ScrollLock",
                "RightShift","LeftShift","RightControl","LeftControl","RightAlt","LeftAlt","RightCommand","RightApple","RightMeta","LeftCommand","LeftApple","LeftMeta","LeftWindows","RightWindows","AltGr","Help","Print","SysReq","Break","Menu",
                "KeypadPeriod","KeypadDivide","KeypadMultiply","KeypadMinus","KeypadPlus","KeypadEnter","KeypadEquals"};
            for(char c='A';c<='Z';c++)names.Add(c.ToString());
            for(int i=0;i<=9;i++){names.Add("Alpha"+i);names.Add("Keypad"+i);}
            for(int i=1;i<=15;i++)names.Add("F"+i);
            for(int i=0;i<=6;i++)names.Add("Mouse"+i);
            for(int b=0;b<=19;b++){names.Add("JoystickButton"+b);for(int j=1;j<=8;j++)names.Add("Joystick"+j+"Button"+b);}
            var known=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(var name in names)known[name]=name;
            known["Ctrl"]="LeftControl";known["Control"]="LeftControl";known["Shift"]="LeftShift";known["Alt"]="LeftAlt";known["Cmd"]="LeftCommand";known["Command"]="LeftCommand";known["Esc"]="Escape";known["Enter"]="Return";
            for(int i=0;i<=9;i++)known[i.ToString()]="Alpha"+i;
            return known;
        }
    }

    /// <summary>Short key-cap names (adopted KeyNames): modifiers first, side dropped, digits bare, joined with "+".</summary>
    public static class KeyLabels {
        public static string Short(HotkeyChord chord){
            if(chord==null||chord.IsNone)return "";
            return string.Join("+",chord.Modifiers.Select(Short).Concat(new[]{Short(chord.Main)}));
        }
        public static string Short(string key){
            if(key.StartsWith("Alpha",StringComparison.Ordinal)&&key.Length==6)return key.Substring(5);
            string side=key.StartsWith("Left",StringComparison.Ordinal)?key.Substring(4):key.StartsWith("Right",StringComparison.Ordinal)?key.Substring(5):"";
            switch(side){case "Shift":case "Alt":case "Command":return side;case "Control":return "Ctrl";default:return key;}
        }
    }

    /// <summary>One frame of keyboard state by key name (case-insensitive) plus whether the player is typing.</summary>
    public sealed class HotkeyFrame {
        readonly HashSet<string> down,held;
        public bool Typing { get; }
        public HotkeyFrame(IEnumerable<string> down,IEnumerable<string> held,bool typing){
            this.down=new HashSet<string>(down??Array.Empty<string>(),StringComparer.OrdinalIgnoreCase);
            this.held=new HashSet<string>(held??Array.Empty<string>(),StringComparer.OrdinalIgnoreCase);Typing=typing;
        }
        public bool Down(string key)=>down.Contains(key);
        public bool Held(string key)=>held.Contains(key);
    }

    /// <summary>
    /// The adopted OpenKeep shortcut rules. With modifiers: main key down this frame, all modifiers held and no other
    /// Shift, Ctrl or Alt; other keys may be held (walking with W). Single key: down with no Shift, Ctrl or Alt held, so Z
    /// never fires with Ctrl+Z. Nothing counts while typing; None never fires.
    /// </summary>
    public static class HotkeyRules {
        public static IReadOnlyList<string> ModifierKeys { get; }=Array.AsReadOnly(new[]{"LeftShift","RightShift","LeftControl","RightControl","LeftAlt","RightAlt"});
        /// <summary>
        /// Shift, Ctrl and Alt are read through the game's own input (<c>ZInput.GetKey(key, false)</c>) as well as Unity's
        /// old <c>Input</c> (see <see cref="KeyHeld"/>). Every other key is read through <c>Input</c> alone.
        /// </summary>
        public static bool ReadsThroughGame(string? key)=>key!=null&&((IList<string>)ModifierKeys).Contains(key);
        /// <summary>
        /// A key held now. Shift, Ctrl and Alt count only when the game's input and Unity's old <c>Input</c> both say so:
        /// either can keep a modifier released in another window held (Alt+Tab out, Alt+Z to an overlay, back with the
        /// mouse), and a modifier really held reads held in both (ValheimModLibs Hotkey stuck-Alt fix). Every other key
        /// follows <c>Input</c> alone.
        /// </summary>
        public static bool KeyHeld(string? key,bool gameHeld,bool unityHeld)=>key!=null&&(ReadsThroughGame(key)?gameHeld&&unityHeld:unityHeld);
        public static bool Pressed(HotkeyChord chord,HotkeyFrame frame)=>Pressed(chord,frame.Typing,frame.Down,frame.Held);
        public static bool Held(HotkeyChord chord,HotkeyFrame frame)=>Held(chord,frame.Typing,frame.Held);
        /// <summary>Delegate form for adapters that query Unity directly without building a frame.</summary>
        public static bool Pressed(HotkeyChord chord,bool typing,Func<string,bool> isDown,Func<string,bool> isHeld){
            if(chord==null||chord.IsNone||typing||!isDown(chord.Main))return false;
            foreach(var modifier in chord.Modifiers)if(!isHeld(modifier))return false;
            foreach(var modifier in ModifierKeys){
                if(modifier==chord.Main||Contains(chord.Modifiers,modifier))continue;
                if(isHeld(modifier))return false;
            }
            return true;
        }
        public static bool Held(HotkeyChord chord,bool typing,Func<string,bool> isHeld){
            if(chord==null||chord.IsNone||typing||!isHeld(chord.Main))return false;
            foreach(var modifier in chord.Modifiers)if(!isHeld(modifier))return false;
            return true;
        }
        /// <summary>Every key an adapter must sample for these chords, including the modifier exclusion set.</summary>
        public static IReadOnlyList<string> KeysToPoll(IEnumerable<HotkeyChord> chords){
            var keys=new SortedSet<string>(ModifierKeys,StringComparer.Ordinal);
            foreach(var chord in chords){if(chord==null||chord.IsNone)continue;keys.Add(chord.Main);foreach(var modifier in chord.Modifiers)keys.Add(modifier);}
            return keys.ToArray();
        }
        static bool Contains(IReadOnlyList<string> list,string value){for(int i=0;i<list.Count;i++)if(list[i]==value)return true;return false;}
    }
}
