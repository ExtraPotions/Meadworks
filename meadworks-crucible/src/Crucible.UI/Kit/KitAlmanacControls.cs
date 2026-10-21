// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// AX06/07/08: native controls composed with the existing sheet/kit factories.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ExtraPotions.Meadworks.Crucible.UI.WindowInput;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>A transient editor on the existing kit window. Escape/B closes this editor before its parent.</summary>
    public sealed class KitAlmanacDialog : IDisposable
    {
        static KitAlmanacDialog active;
        KitAlmanacDialog previous;
        public static KitAlmanacDialog Active=>active;
        public KitWindow Window {get;}
        public KitWindow Parent {get;}
        public RectTransform Panel=>Window.Panel;
        readonly List<Action> cleanup=new List<Action>();
        bool disposed;
        public Func<bool> TypingField {get;set;}
        public event Action Closed;
        public event Action Tick;
        internal void UpdateEditor()=>Tick?.Invoke();
        KitAlmanacDialog(KitWindow parent,KitWindow window){Parent=parent;Window=window;window.Closed+=Dispose;}
        public static KitAlmanacDialog Create(KitWindow parent,string title,Vector2 size)
        {
            if(active!=null&&!ReferenceEquals(active.Window,parent))active.Dispose();
            if(parent?.Panel==null||!parent.IsOpen)return null;
            var window=KitWindow.Create("editor",size,parent.Panel,typing:false);
            if(window==null)return null;
            window.HideHud=parent.HideHud;
            var dialog=new KitAlmanacDialog(parent,window){previous=active};active=dialog;
            // The editor is drawn inside its parent window; a translucent parent (Almanac Window Opacity) must not show through it.
            window.Panel.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups=true;
            var heading=Label(dialog.Panel,"Title",title,new Vector2(16,-12),new Vector2(size.x-32,32),20);
            if(KitCanvas.Heading!=null)heading.font=KitCanvas.Heading;
            var lifetime=dialog.Panel.gameObject.AddComponent<KitAlmanacDialogLifetime>();lifetime.Dialog=dialog;
            window.Open();return dialog;
        }
        public void Own(Action release)=>cleanup.Add(release);
        GameObject shade;
        /// <summary>
        /// Darken the whole screen behind this editor as the game does behind a form (<see cref="KitPopupShade"/>); call it for
        /// forms, not for yes/no confirmations, which the game leaves unshaded. The shade goes when the editor closes.
        /// </summary>
        public void Dim()
        {
            var host=Panel!=null?Panel.parent:null;var canvas=Panel!=null?Panel.GetComponentInParent<Canvas>()?.rootCanvas:null;
            if(disposed||shade!=null||host==null||canvas==null)return;
            shade=new GameObject("Dim",typeof(RectTransform),typeof(Image),typeof(CanvasGroup));shade.layer=Panel.gameObject.layer;
            var rect=(RectTransform)shade.transform;rect.SetParent(host,false);rect.SetSiblingIndex(Panel.GetSiblingIndex());
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.position=canvas.transform.position;rect.sizeDelta=((RectTransform)canvas.transform).rect.size*2;
            shade.GetComponent<Image>().color=KitUnity.Colour(KitPopupShade.Colour);shade.GetComponent<CanvasGroup>().ignoreParentGroups=true;
            var made=shade;Own(()=>KitUnity.Retire(made));
        }
        public void Dispose()
        {
            if(disposed)return;
            if(active!=null&&!ReferenceEquals(active,this)&&active.Panel!=null&&active.Panel.IsChildOf(Panel))active.Dispose();
            disposed=true;if(ReferenceEquals(active,this))active=previous!=null&&!previous.disposed?previous:null;
            foreach(var release in cleanup)try{release();}catch(Exception error){CrucibleUiPlugin.Log?.Debug("Kit editor cleanup: "+error.GetType().Name);}
            cleanup.Clear();Window.Closed-=Dispose;Window.Close();Window.Destroy();Closed?.Invoke();
        }
        public static bool CloseTopmost(){if(active==null)return false;active.Dispose();return true;}
        public static TMP_Text Label(Transform parent,string name,string words,Vector2 at,Vector2 size,float font=16)
        {
            var label=KitCanvas.Text(parent,name,words,font,Color.white,TextAlignmentOptions.TopLeft,true);
            label.richText=false;Place(label.rectTransform,at,size);return label;
        }
        public static void Place(RectTransform rect,Vector2 at,Vector2 size)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=at;rect.sizeDelta=size;}
        [HarmonyPatch(typeof(GameWindow),nameof(GameWindow.CloseOpen))]
        static class EscapeFirst
        {
            static bool Prefix(ref bool __result)
            {
                if(active==null)return true;
                // Native field Escape restores/leaves the edit first; an editor closes on the next Escape.
                if(!KitTextField.Typing&&active.TypingField?.Invoke()!=true)active.Dispose();__result=true;return false;
            }
        }
        [HarmonyPatch(typeof(Chat),nameof(Chat.HasFocus))]
        static class DialogTyping
        {static void Postfix(ref bool __result){if(active?.TypingField?.Invoke()==true)__result=true;}}
        [HarmonyPatch(typeof(KitWindow),"Pad")]
        static class ParentPad
        {
            static bool Prefix(KitWindow __instance,PadButton button)
            {
                if(active==null)return true;
                if(ReferenceEquals(active.Window,__instance))
                {
                    var selected=EventSystem.current?.currentSelectedGameObject;
                    var field=selected!=null?selected.GetComponent<GUIFramework.GuiInputField>():null;
                    if(field!=null&&field.isFocused&&KitPad.ActionOf(button)==KitPadAction.Close)
                    {MeadworksInput.PadConsume(button);field.DeactivateInputField();return false;}
                    if(field!=null&&KitPad.ActionOf(button)==KitPadAction.Activate)
                    {MeadworksInput.PadConsume(button);if(field.readOnly||!field.OpenKeyboard())field.ActivateInputField();return false;}
                    var slider=selected!=null?selected.GetComponent<Slider>():null;
                    if(slider!=null&&(button==PadButton.DPadLeft||button==PadButton.DPadRight))
                    {MeadworksInput.PadConsume(button);slider.GetComponent<KitAlmanacSliderRelease>()?.Step?.Invoke(button==PadButton.DPadLeft?-1:1);return false;}
                    return true;
                }
                if(__instance.Panel==null||!active.Panel.IsChildOf(__instance.Panel))return true;
                if(KitPad.ActionOf(button)==KitPadAction.Close){MeadworksInput.PadConsume(button);active.Dispose();}
                return false;
            }
        }
    }
    public sealed class KitAlmanacDialogLifetime : MonoBehaviour
    {
        internal KitAlmanacDialog Dialog;
        void Update(){if(Dialog==null)return;if(!Dialog.Parent.IsOpen)Dialog.Dispose();else Dialog.UpdateEditor();}
        void OnDestroy(){Dialog?.Dispose();Dialog=null;}
    }

    /// <summary>Signed/decimal number field, step buttons, and native Slider for bounded values.</summary>
    public sealed class KitAlmanacNumber : IDisposable
    {
        public KitTextField Field {get;}
        public Button Less {get;}
        public Button More {get;}
        public Slider Slider {get;}
        readonly Action<string> commit;
        readonly Func<int,string> step;
        readonly Func<double,string> serialize;
        readonly double min,max,increment;
        readonly bool percent;
        readonly TMP_Text label;
        bool setting,previewing;
        readonly KitNumberEditBaseline editBaseline=new KitNumberEditBaseline();
        KitAlmanacNumber(KitTextField field,Button less,Button more,Slider slider,TMP_Text label,
            Action<string> commit,Func<int,string> step,double min,double max,double increment,bool percent,Func<double,string> serialize)
        {Field=field;Less=less;More=more;Slider=slider;this.label=label;this.commit=commit;this.step=step;this.min=min;this.max=max;this.increment=increment;this.percent=percent;this.serialize=serialize;}
        public static KitAlmanacNumber Create(RectTransform parent,string name,Vector2 at,Vector2 size,
            string value,Func<string,bool> valid,Action<string> commit,Func<int,string> step,
            double? min=null,double? max=null,double increment=1,bool percent=false,Func<double,string> serialize=null)
        {
            KitAlmanacNumber made=null;
            var less=KitButton.Create(parent,name+"Less","−",()=>made?.Step(-1),at,new Vector2(28,30));
            var field=KitTextField.Create(parent,name+"Field",KitFieldMode.Text,at+new Vector2(32,0),new Vector2(size.x-64,30));
            var more=KitButton.Create(parent,name+"More","+",()=>made?.Step(1),at+new Vector2(size.x-28,0),new Vector2(28,30));
            Slider slider=null;TMP_Text label=null;
            if(min.HasValue&&max.HasValue&&max>min)
            {
                var source=Resources.FindObjectsOfTypeAll<Slider>().FirstOrDefault(s=>s!=null&&!s.name.StartsWith("Crucible_",StringComparison.Ordinal));
                if(source!=null)
                {
                    var copy=UnityEngine.Object.Instantiate(source.gameObject,parent,false);copy.name="Crucible_"+name+"Slider";
                    slider=copy.GetComponent<Slider>();slider.onValueChanged=new Slider.SliderEvent();
                    slider.minValue=0;slider.maxValue=1;slider.wholeNumbers=false;
                    KitAlmanacDialog.Place((RectTransform)copy.transform,at+new Vector2(0,-40),new Vector2(size.x-60,22));copy.SetActive(true);
                }
                else slider=NativeSlider(parent,name,at+new Vector2(0,-40),new Vector2(size.x-60,22));
                label=KitAlmanacDialog.Label(parent,name+"Percent","",at+new Vector2(size.x-56,-40),new Vector2(56,24),14);
            }
            made=new KitAlmanacNumber(field,less,more,slider,label,commit,step,min??0,max??1,increment,percent,serialize);
            field.Validator=valid;field.Text=value;field.Ended+=edit=>{if(made.editBaseline.ShouldCommit(edit.Commit,edit.Text))commit(edit.Text);};
            if(slider!=null)
            {
                slider.onValueChanged.AddListener(v=>made.Preview(v));
                var end=slider.gameObject.AddComponent<KitAlmanacSliderRelease>();end.Released=made.CommitSlider;end.Step=made.Step;
            }
            made.Set(value);return made;
        }
        public void Step(int direction){if(Field.Input.IsInteractable())try{commit(step(direction));}catch(Exception error){CrucibleUiPlugin.Log?.Debug("Numeric step refused: "+error.GetBaseException().Message);}}
        static Slider NativeSlider(RectTransform parent,string name,Vector2 at,Vector2 size)
        {
            var go=new GameObject("Crucible_"+name+"Slider",typeof(RectTransform),typeof(Image),typeof(Slider));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;KitAlmanacDialog.Place(rect,at,size);go.GetComponent<Image>().color=new Color(.12f,.12f,.12f,.9f);
            var handleGo=new GameObject("Handle",typeof(RectTransform),typeof(Image));handleGo.layer=parent.gameObject.layer;handleGo.transform.SetParent(rect,false);
            var handle=(RectTransform)handleGo.transform;handle.sizeDelta=new Vector2(16,size.y);handleGo.GetComponent<Image>().color=new Color(.78f,.67f,.45f,1);
            Paint(go.GetComponent<Image>(),KitArt.Cell);Paint(handleGo.GetComponent<Image>(),KitArt.Button);
            go.AddComponent<KitAlmanacSliderArt>().Handle=handleGo.GetComponent<Image>();
            var slider=go.GetComponent<Slider>();slider.handleRect=handle;slider.targetGraphic=handleGo.GetComponent<Image>();slider.direction=Slider.Direction.LeftToRight;slider.minValue=0;slider.maxValue=1;slider.wholeNumbers=false;return slider;
        }
        // The kit's painted cell and button art; the flat colors above stay only when the art is missing.
        internal static void Paint(Image image,Sprite sprite){if(sprite==null)return;image.sprite=sprite;image.type=sprite.border==Vector4.zero?Image.Type.Simple:Image.Type.Sliced;image.color=Color.white;}
        public void Place(Vector2 at,float width)
        {
            width=Math.Max(100,width);
            KitAlmanacDialog.Place((RectTransform)Less.transform,at,new Vector2(28,30));
            KitAlmanacDialog.Place(Field.Rect,at+new Vector2(32,0),new Vector2(width-64,30));
            KitAlmanacDialog.Place((RectTransform)More.transform,at+new Vector2(width-28,0),new Vector2(28,30));
            if(Slider!=null)KitAlmanacDialog.Place((RectTransform)Slider.transform,at+new Vector2(0,-40),new Vector2(width-60,22));
            if(label!=null)KitAlmanacDialog.Place(label.rectTransform,at+new Vector2(width-56,-40),new Vector2(56,24));
        }
        void Preview(float fraction)
        {
            if(setting)return;previewing=true;
            double value=min+(max-min)*fraction;
            if(increment>0)value=min+Math.Round((value-min)/increment)*increment;
            value=Math.Max(min,Math.Min(max,value));
            Field.Text=serialize!=null?serialize(value):value.ToString("R",CultureInfo.InvariantCulture);
            if(label!=null)label.text=percent?(fraction*100).ToString("0.#",CultureInfo.InvariantCulture)+"%":"";
        }
        void CommitSlider(){if(Slider!=null&&Slider.IsInteractable()){previewing=false;commit(Field.Text);}}
        public void Set(string value,double? current=null)
        {
            if(previewing)return;
            if(editBaseline.Observe(value,Field.Focused))Field.Text=value;
            double number;if(current.HasValue)number=current.Value;else if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number))return;
            if(Slider==null)return;
            setting=true;Slider.SetValueWithoutNotify((float)Math.Max(0,Math.Min(1,(number-min)/(max-min))));setting=false;
            if(label!=null)label.text=percent?(Slider.value*100).ToString("0.#",CultureInfo.InvariantCulture)+"%":"";
        }
        public void SetUsable(bool value){Field.SetUsable(value);Less.interactable=More.interactable=value;if(Slider!=null)Slider.interactable=value;}
        public void Dispose(){Field.Destroy();if(Less!=null)UnityEngine.Object.Destroy(Less.gameObject);if(More!=null)UnityEngine.Object.Destroy(More.gameObject);if(Slider!=null)UnityEngine.Object.Destroy(Slider.gameObject);if(label!=null)UnityEngine.Object.Destroy(label.gameObject);}
    }
    // The handle's button art follows the Timber/Brown look while the dialog stays open.
    sealed class KitAlmanacSliderArt : MonoBehaviour
    {
        internal Image Handle;
        void Awake()=>KitAppearance.Changed+=Repaint;
        void OnDestroy()=>KitAppearance.Changed-=Repaint;
        void Repaint(){if(Handle!=null)KitAlmanacNumber.Paint(Handle,KitArt.Button);}
    }
    public sealed class KitAlmanacSliderRelease : MonoBehaviour,IPointerUpHandler,IEndDragHandler
    {
        internal Action Released;
        internal Action<int> Step;
        int frame=-1;void Release(){if(frame==Time.frameCount)return;frame=Time.frameCount;Released?.Invoke();}
        public void OnPointerUp(PointerEventData data)=>Release();
        public void OnEndDrag(PointerEventData data)=>Release();
    }
    public static class KitAlmanacChoice
    {
        // Ten 38-unit rows show at once (user request, October 20, 2026; it was eight in a 310-unit list); longer lists scroll.
        const int ShownRows=10;
        const float RowHeight=38f,ListHeight=ShownRows*RowHeight,ListTop=-55f,ButtonsY=ListTop-ListHeight-10f;
        public static void Open(KitWindow parent,string title,IReadOnlyList<string> options,IReadOnlyCollection<string> selected,bool multiple,Action<IReadOnlyList<string>> commit)
            =>Open(parent,title,options,selected,multiple,commit,null,null);
        public static void Open(KitWindow parent,string title,IReadOnlyList<string> options,IReadOnlyCollection<string> selected,bool multiple,Action<IReadOnlyList<string>> commit,Func<string,string> label=null,Func<string,string> help=null)
        {
            var dialog=KitAlmanacDialog.Create(parent,title,new Vector2(450,-ButtonsY+65f));if(dialog==null)return;
            var chosen=new HashSet<string>(selected??Array.Empty<string>(),StringComparer.Ordinal);
            var toggles=new Dictionary<RectTransform,KitToggle>();
            bool wrote=false,cancelled=false;
            dialog.Own(()=>{foreach(var toggle in toggles.Values)toggle.Dispose();toggles.Clear();});
            dialog.Closed+=()=>{if(multiple&&!wrote&&!cancelled)commit(options.Where(chosen.Contains).ToArray());};
            var list=KitVirtualList.Create(dialog.Panel,"Choices",new Vector2(410,ListHeight),p=>{var go=new GameObject("Choice",typeof(RectTransform));go.transform.SetParent(p,false);return (RectTransform)go.transform;});
            KitAlmanacDialog.Place(list.Root,new Vector2(20,ListTop),new Vector2(410,ListHeight));dialog.Own(list.Dispose);
            dialog.Tick+=list.Tick;
            var rows=new List<KitVirtualRow>();
            for(int i=0;i<options.Count;i++)
            {
                string value=options[i];int row=i;string caption=label?.Invoke(value)??value;
                string detail=help?.Invoke(value)??((multiple?"On includes this choice; off excludes it. Apply or close to confirm. Cancel keeps the previous choices.":"Choose this value for "+title+".")+"\n"+value);
                rows.Add(new KitVirtualRow(value+"#"+i,RowHeight,rect=>
                {
                    if(multiple)
                    {
                        if(!toggles.TryGetValue(rect,out var toggle))
                        {toggle=KitToggle.Create(rect,"Choice",chosen.Contains(value),on=>{if(on)chosen.Add(value);else chosen.Remove(value);},Vector2.zero,new Vector2(390,32),caption,caption);toggle.Tooltip(caption,detail);toggles[rect]=toggle;}
                        toggle.SetValue(chosen.Contains(value));dialog.Window.Focusable(toggle.Button,0,row);
                    }
                    else
                    {
                        var button=KitButton.Create(rect,"Choice",caption,()=>{dialog.Dispose();commit(new[]{value});},Vector2.zero,new Vector2(390,32));KitTooltip.Set(button.gameObject,caption,detail);dialog.Window.Focusable(button,0,row);
                    }
                },rect=>{foreach(var control in rect.GetComponentsInChildren<Selectable>(true))dialog.Window.Unfocusable(control);if(toggles.TryGetValue(rect,out var toggle)){toggle.Dispose();toggles.Remove(rect);}foreach(Transform child in rect){child.gameObject.SetActive(false);child.name="retired";UnityEngine.Object.Destroy(child.gameObject);}}));
            }
            list.SetRows(rows,1);
            if(multiple)
            {
                var none=KitButton.Create(dialog.Panel,"None","None",()=>{chosen.Clear();list.SetRows(rows,Time.frameCount+2);},new Vector2(20,ButtonsY),new Vector2(75,32));
                var all=KitButton.Create(dialog.Panel,"All","All",()=>{chosen.Clear();foreach(var option in options)chosen.Add(option);list.SetRows(rows,Time.frameCount+2);},new Vector2(100,ButtonsY),new Vector2(75,32));
                var apply=KitButton.Create(dialog.Panel,"Apply","Apply",()=>{wrote=true;var values=options.Where(chosen.Contains).ToArray();dialog.Dispose();commit(values);},new Vector2(260,ButtonsY),new Vector2(75,32));
                KitTooltip.Set(none.gameObject,"None","Clear every selection in this window. Apply or close to confirm; Cancel keeps the previous choices.");
                KitTooltip.Set(all.gameObject,"All","Select every choice in this window. Apply or close to confirm; Cancel keeps the previous choices.");
                KitTooltip.Set(apply.gameObject,"Apply","Confirm the selected values and close this window.");
                dialog.Window.Focusable(none,0,options.Count+1);dialog.Window.Focusable(all,1,options.Count+1);dialog.Window.Focusable(apply,2,options.Count+1);
            }
            var cancel=KitButton.Create(dialog.Panel,"Cancel","Cancel",()=>{cancelled=true;dialog.Dispose();},new Vector2(340,ButtonsY),new Vector2(90,32));KitTooltip.Set(cancel.gameObject,"Cancel","Close this window without applying these choices.");dialog.Window.Focusable(cancel,3,options.Count+1);
        }
    }
    public sealed class KitAlmanacKeyCapture : MonoBehaviour
    {
        KitAlmanacDialog dialog;bool shortcut;Action<KeyboardShortcut> commit;KeyCode main;KeyCode[] modifiers;int start;
        public static void Open(KitWindow parent,string title,bool shortcut,Action<KeyboardShortcut> commit)
        {
            var dialog=KitAlmanacDialog.Create(parent,title,new Vector2(470,160));if(dialog==null)return;
            KitAlmanacDialog.Label(dialog.Panel,"Hint",KitText.Text(KitText.KeyCapture,CrucibleUiPlugin.Language),new Vector2(18,-60),new Vector2(435,60));
            var capture=dialog.Panel.gameObject.AddComponent<KitAlmanacKeyCapture>();capture.dialog=dialog;capture.shortcut=shortcut;capture.commit=commit;capture.start=Time.frameCount;
        }
        static bool Modifier(KeyCode key)=>key==KeyCode.LeftShift||key==KeyCode.RightShift||key==KeyCode.LeftControl||key==KeyCode.RightControl||key==KeyCode.LeftAlt||key==KeyCode.RightAlt||key==KeyCode.LeftCommand||key==KeyCode.RightCommand;
        void Update()
        {
            if(dialog==null||Time.frameCount<=start)return;
            if(main!=KeyCode.None){if(!Input.GetKey(main)){commit(new KeyboardShortcut(main,modifiers));dialog.Dispose();}return;}
            foreach(KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if(!Input.GetKeyDown(key)||key==KeyCode.None)continue;
                if(key==KeyCode.Escape){dialog.Dispose();return;}
                if(key==KeyCode.Backspace){commit(new KeyboardShortcut(KeyCode.None));dialog.Dispose();return;}
                if(shortcut&&Modifier(key))continue;
                main=key;modifiers=shortcut?Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>().Where(k=>Modifier(k)&&Input.GetKey(k)).ToArray():Array.Empty<KeyCode>();
                if(!shortcut){commit(new KeyboardShortcut(key));dialog.Dispose();}return;
            }
        }
    }
}
