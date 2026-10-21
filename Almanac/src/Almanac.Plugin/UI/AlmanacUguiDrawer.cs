// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original host for the ConfigDrawers native uGUI drawer convention (behavior from BepInEx.ConfigDrawers 1.1.1,
// Vapok, MIT: a CustomUguiDrawer delegate on ConfigurationManagerAttributes, called with a builder scope). No ConfigDrawers
// code is copied; the scope draws with Crucible kit controls.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Almanac.Runtime.UI
{
    /// <summary>
    /// The builder a mod's uGUI drawer receives. Member names and parameters equal ConfigDrawers' IUguiDrawerScope so a
    /// mod's own copy of that interface is served through <see cref="AlmanacUguiScopeProxy"/>. Controls flow left to right
    /// inside Horizontal/Row groups and one per line otherwise.
    /// </summary>
    public sealed class AlmanacUguiScope
    {
        const float Line=30,Gap=4;
        readonly RectTransform root;readonly float width;readonly Action<Exception> failed;
        readonly Stack<bool> horizontal=new Stack<bool>();
        float x,y,lineHeight=Line;int count;
        internal AlmanacUguiScope(RectTransform root,float width,bool readOnly,Action<Exception> failed){this.root=root;this.width=width;IsReadOnly=readOnly;this.failed=failed;}
        public Transform Container=>root;
        public bool IsReadOnly { get; }
        /// <summary>Height the drawer used, for the row.</summary>
        internal float Height=>-y+(x>0?lineHeight:0);
        public IDisposable Horizontal(float spacing=4f)=>Group(true);
        public IDisposable Vertical(float spacing=4f)=>Group(false);
        public IDisposable Box(Color? backgroundColor=null,float padding=4f,float spacing=4f)=>Group(false);
        public IDisposable Row(Color? backgroundColor=null,float padding=4f,float spacing=4f)=>Group(true);
        public void Label(string text,float width=-1f)
        {var label=KitAlmanacDialog.Label(root,Name("Label"),text??"",Vector2.zero,Vector2.one,14);label.alignment=TMPro.TextAlignmentOptions.MidlineLeft;Place(label.rectTransform,width<0?Math.Min(160,Free):width);}
        public void TextField(string value,Action<string> onCommit,float width=-1f)
        {
            var field=KitTextField.Create(root,Name("Field"),KitFieldMode.Text,Vector2.zero,Vector2.one);field.Text=value??"";field.SetUsable(!IsReadOnly);
            field.Ended+=edit=>{if(edit.Commit&&!IsReadOnly)Safe(()=>onCommit?.Invoke(edit.Text));};Place(field.Rect,width<0?Math.Min(200,Free):width);
        }
        public void Button(string text,Action onClick,float width=50f)=>Button(text,onClick,width,null);
        public void Button(string text,Action onClick,float width,string? tooltip)
        {
            var button=AlmanacButtons.Create(root,Name("Button"),text??"",()=>{if(!IsReadOnly)Safe(()=>onClick?.Invoke());},Vector2.zero,Vector2.one);button.interactable=!IsReadOnly;
            if(!string.IsNullOrEmpty(tooltip))KitTooltip.Set(button.gameObject,text??"",tooltip!);
            Place((RectTransform)button.transform,Math.Max(width,AlmanacButtons.CaptionWidth(button)+16));
        }
        public void Slider(float value,float min,float max,Action<float> onChanged,float width=120f,string? format=null)
        {
            float current=value,size=Math.Max(100,width);string Text(float v)=>v.ToString(format??"R",CultureInfo.InvariantCulture);
            KitAlmanacNumber? number=null;
            number=KitAlmanacNumber.Create(root,Name("Slider"),new Vector2(At().x,At().y),new Vector2(size,30),Text(value),
                text=>float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var v)&&v>=min&&v<=max,
                text=>{if(IsReadOnly||!float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var v))return;current=v;number?.Set(Text(v),v);Safe(()=>onChanged?.Invoke(v));},
                step=>Text(Mathf.Clamp(current+step*(max-min)/20f,min,max)),min,max,(max-min)/100f);
            number.SetUsable(!IsReadOnly);
            Advance(size,number.Slider!=null?62:Line);
        }
        public void Toggle(bool value,string label,Action<bool> onChanged)
        {
            var toggle=KitToggle.Create(root,Name("Toggle"),value,on=>{if(!IsReadOnly)Safe(()=>onChanged?.Invoke(on));},Vector2.zero,new Vector2(80,Line),Localization.AlmanacText.T("On"),Localization.AlmanacText.T("Off"));
            toggle.SetUsable(!IsReadOnly);
            bool flow=Flowing;if(!flow)horizontal.Push(true);
            Place((RectTransform)toggle.Button.transform,80);
            if(!string.IsNullOrEmpty(label))Label(label);
            if(!flow){horizontal.Pop();NewLine();}
        }
        public void Space(float pixels){if(Flowing)x+=Math.Max(0,pixels);else{NewLine();y-=Math.Max(0,pixels);}}
        public void Separator(float height=1f,Color? color=null)
        {
            NewLine();var line=new GameObject(Name("Separator"),typeof(RectTransform),typeof(Image));line.layer=root.gameObject.layer;line.transform.SetParent(root,false);
            line.GetComponent<Image>().color=color??new Color(.78f,.67f,.45f,.6f);line.GetComponent<Image>().raycastTarget=false;
            KitAlmanacDialog.Place((RectTransform)line.transform,new Vector2(0,y-2),new Vector2(width,Math.Max(1,height)));y-=Math.Max(1,height)+4;
        }

        bool Flowing=>horizontal.Count>0&&horizontal.Peek();
        float Free=>Math.Max(40,width-x);
        string Name(string kind)=>"Ugui"+kind+(count++).ToString(CultureInfo.InvariantCulture);
        Vector2 At(){if(!Flowing)NewLine();return new Vector2(x,y);}
        IDisposable Group(bool flow){NewLine();horizontal.Push(flow);return new Closer(()=>{if(horizontal.Count>0)horizontal.Pop();NewLine();});}
        void NewLine(){if(x>0){y-=lineHeight+Gap;x=0;lineHeight=Line;}}
        void Place(RectTransform rect,float size){var at=At();KitAlmanacDialog.Place(rect,at,new Vector2(size,Line));Advance(size,Line);}
        void Advance(float size,float height){x+=size+Gap;lineHeight=Math.Max(lineHeight,height);if(!Flowing)NewLine();}
        void Safe(Action action){try{action();}catch(Exception error){failed(error);}}
        sealed class Closer:IDisposable{readonly Action close;bool done;internal Closer(Action close){this.close=close;}public void Dispose(){if(done)return;done=true;close();}}

        /// <summary>
        /// Calls a mod's drawer the way ConfigDrawers does: no parameter, a Transform (the container), the scope or the
        /// mod's own copy of the scope interface, optionally followed by the ConfigEntryBase.
        /// </summary>
        internal static void Invoke(Delegate drawer,AlmanacUguiScope scope,ConfigEntryBase entry)
        {
            var parameters=drawer.Method.GetParameters();
            if(parameters.Length==0){drawer.DynamicInvoke();return;}
            var type=parameters[0].ParameterType;
            object argument=type==typeof(Transform)?scope.Container:type.IsAssignableFrom(typeof(AlmanacUguiScope))?scope:
                type.IsInterface?AlmanacUguiScopeProxy.Create(type,scope):throw new NotSupportedException("Unsupported uGUI drawer parameter "+type.FullName+".");
            if(parameters.Length==2)drawer.DynamicInvoke(argument,entry);else drawer.DynamicInvoke(argument);
        }
    }

    /// <summary>Implements a mod's own copy of the scope interface by forwarding each call to <see cref="AlmanacUguiScope"/> by name and parameter types.</summary>
    public class AlmanacUguiScopeProxy:DispatchProxy
    {
        AlmanacUguiScope? target;
        internal static object Create(Type contract,AlmanacUguiScope scope)
        {
            var proxy=typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create))!.MakeGenericMethod(contract,typeof(AlmanacUguiScopeProxy)).Invoke(null,null)!;
            ((AlmanacUguiScopeProxy)proxy).target=scope;return proxy;
        }
        protected override object? Invoke(MethodInfo? method,object?[]? args)
        {
            if(method==null||target==null)throw new InvalidOperationException("Scope proxy is not bound.");
            var types=method.GetParameters().Select(p=>p.ParameterType).ToArray();
            var match=typeof(AlmanacUguiScope).GetMethod(method.Name,BindingFlags.Public|BindingFlags.Instance,null,types,null)
                ??throw new MissingMethodException("Almanac's uGUI scope has no "+method.Name+"("+string.Join(", ",types.Select(t=>t.Name))+").");
            return match.Invoke(target,args);
        }
    }
}
