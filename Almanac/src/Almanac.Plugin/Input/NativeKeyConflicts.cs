// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Coexistence;
using UnityEngine;
namespace ExtraPotions.Almanac.Runtime.UI
{
    static class NativeKeyConflicts
    {
        static readonly FieldInfo? buttons=typeof(ZInput).GetField("m_buttons",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly MethodInfo? keyPath=typeof(ZInput).GetMethod("KeyCodeToPath",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(KeyCode),typeof(bool)},null);
        static readonly List<KeyValuePair<string,string>> bindings=new List<KeyValuePair<string,string>>();
        static bool available;
        static float next;
        internal static void Refresh()
        {
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;bindings.Clear();available=false;
            try
            {
                if(ZInput.instance==null || keyPath==null || !(buttons?.GetValue(ZInput.instance) is Dictionary<string,ZInput.ButtonDef> live))return;
                foreach(var pair in live)
                {
                    string path=pair.Value.GetActionPath();
                    if(!string.IsNullOrEmpty(path) && path.StartsWith("<Keyboard>/",StringComparison.OrdinalIgnoreCase))bindings.Add(new KeyValuePair<string,string>(pair.Key,path));
                }
                available=true;
            }
            catch{bindings.Clear();}
        }
        internal static string Describe(object value)
        {
            KeyCode key;bool modifiers;
            if(value is KeyboardShortcut shortcut){key=shortcut.MainKey;modifiers=shortcut.Modifiers.Any();}
            else if(value is KeyCode code){key=code;modifiers=false;}
            else return "";
            if(key==KeyCode.None)return "";
            Refresh();
            string path="";
            try{path=keyPath?.Invoke(null,new object[]{key,false}) as string??"";}catch{ }
            if(path.Length==0)return "Game key bindings are unavailable; native conflicts were not checked.";
            return KeyConflictPolicy.Describe(path,modifiers,bindings,available);
        }
    }
}
