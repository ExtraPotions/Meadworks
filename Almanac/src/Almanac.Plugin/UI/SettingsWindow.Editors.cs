// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Attributes;
using ExtraPotions.Almanac.Display;
using ExtraPotions.Almanac.Editors;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Almanac.Runtime.UI
{
    public sealed partial class SettingsWindow
    {
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        NativeEntry? choiceEntry;
        KitAlmanacDialog? choiceDialog;
        static string Chord(NativeEntry native)
        {
            if(native.Entry.BoxedValue is KeyboardShortcut shortcut)return shortcut.MainKey==KeyCode.None?"":shortcut.MainKey+":"+string.Join(",",shortcut.Modifiers.OrderBy(key=>(int)key));
            return native.Entry.BoxedValue is KeyCode key&&key!=KeyCode.None?key+":":"";
        }
        void TextEditor(NativeEntry native,bool raw)
        {
            bool decoded=!raw&&native.Shape.Kind==SettingKind.String&&EditorSelector.Codec(native.Shape,native.Attributes)!=CodecChoice.ModDelegates;
            string current=decoded?native.Entry.BoxedValue as string??"":native.Text();
            string Encoded(string value)=>decoded?TomlTypeConverter.ConvertToString(value,typeof(string)):value;
            string title=Presentation.DisplayName(native.Attributes,native.Entry.Definition.Key);
            // JSON-shaped values get a live structure check; YAML-shaped values are only colored, since plain prose can look like YAML.
            var kind=TextSyntax.Detect(null,current);
            KitAlmanacTextEditor.Open(window!,title,current,
                value=>{string error=native.Parse(Encoded(value)).Error??"";return error.Length==0&&kind==SyntaxKind.Json?AlmanacSyntax.Error(value,kind):error;},
                value=>Write(native,Encoded(value)),Locked(native,Facts(native.Entry)).Length>0,
                AlmanacSyntax.Options(kind,window!,title,AlmanacSyntax.HasTable(current)));
            FollowDependency(native);
        }
        void Choices(NativeEntry native,bool flags)
        {
            var options=native.Shape.AcceptableList??native.Shape.EnumNames;
            var selected=new List<string>();
            if(flags)
            {
                var value=(Enum)native.Entry.BoxedValue;
                foreach(string option in options)
                {var bit=(Enum)Enum.Parse(native.Entry.SettingType,option);if(!bit.Equals(Enum.ToObject(native.Entry.SettingType,0))&&value.HasFlag(bit))selected.Add(option);}
            }
            else selected.Add(native.RawSerialized());
            // A mod can show a translated caption while the choice itself stays the stored identifier (Mantle trinket presets).
            Func<string,string> label=null;
            if(!flags&&native.Attributes.ObjToStr is Delegate show)
                label=option=>{try{return (string)show.DynamicInvoke(TomlTypeConverter.ConvertToValue(option,native.Entry.SettingType))??option;}catch{return option;}};
            // The heading is the same localized setting name as the row, and follows a language change while this list stays open.
            string title=ChoiceDialogTitle(native);
            KitAlmanacChoice.Open(window!,title,options,selected,flags,values=>
            {
                string zero=options.FirstOrDefault(option=>Enum.Parse(native.Entry.SettingType,option).Equals(Enum.ToObject(native.Entry.SettingType,0)))??"0";
                string text=flags?values.Count>0?string.Join(", ",values):zero:values[0];
                WriteValue(native,TomlTypeConverter.ConvertToValue(text,native.Entry.SettingType));
            },label);
            BindChoiceDialog(native,title);
            FollowDependency(native);
        }
        string ChoiceDialogTitle(NativeEntry native)
        {
            string language=presentationLanguage.Length>0?presentationLanguage:CrucibleUiPlugin.Language;
            return EntryPresentation.ChoiceDialogTitle(native,language,text);
        }
        void BindChoiceDialog(NativeEntry native,string title)
        {
            var dialog=KitAlmanacDialog.Active;
            if(dialog==null||dialog.Parent!=window||dialog.Panel==null)return;
            var heading=dialog.Panel.Find("Title");
            var shown=heading!=null?heading.GetComponent<TMPro.TMP_Text>():null;
            if(shown==null||shown.text!=title)return;
            if(!ReferenceEquals(choiceDialog,dialog))
            {
                if(choiceDialog!=null)choiceDialog.Closed-=ClearChoiceDialog;
                choiceDialog=dialog;
                dialog.Closed+=ClearChoiceDialog;
            }
            choiceEntry=native;
        }
        void ClearChoiceDialog()
        {
            if(choiceDialog!=null)choiceDialog.Closed-=ClearChoiceDialog;
            choiceDialog=null;
            choiceEntry=null;
        }
        void RefreshChoiceDialogTitle()
        {
            var entry=choiceEntry;
            var dialog=choiceDialog;
            if(entry==null||dialog==null)return;
            var panel=dialog.Panel;
            if(panel==null){ClearChoiceDialog();return;}
            var heading=panel.Find("Title");
            var label=heading!=null?heading.GetComponent<TMPro.TMP_Text>():null;
            if(label==null)return;
            string title=ChoiceDialogTitle(entry);
            if(label.text!=title)label.text=title;
        }
        void ListEditor(NativeEntry native)
        {
            string? separator=native.Attributes.ListSeparator;
            Type element=separator!=null?typeof(string):native.Entry.SettingType.IsArray?native.Entry.SettingType.GetElementType()!:native.Entry.SettingType.GetGenericArguments().FirstOrDefault()??typeof(object);
            if(!TomlTypeConverter.CanConvert(element)){TextEditor(native,true);return;}
            var values=separator!=null?(native.Entry.BoxedValue as string??"").Split(new[]{separator},StringSplitOptions.None).ToList():
                (native.Entry.BoxedValue as IEnumerable)?.Cast<object>().Select(value=>TomlTypeConverter.ConvertToString(value,element)).ToList()??new List<string>();
            string Error(string value)
            {
                if(separator!=null)return "";
                try{TomlTypeConverter.ConvertToValue(value,element);return "";}catch(Exception error){return error.GetBaseException().Message;}
            }
            void ApplyItems(IReadOnlyList<string> items)
            {
                try
                {
                    if(separator!=null){WriteValue(native,string.Join(separator,items));return;}
                    var converted=items.Select(value=>TomlTypeConverter.ConvertToValue(value,element)).ToArray();
                    if(native.Entry.SettingType.IsArray){var array=Array.CreateInstance(element,items.Count);for(int i=0;i<items.Count;i++)array.SetValue(converted[i],i);WriteValue(native,array);}
                    else{var list=(IList)Activator.CreateInstance(native.Entry.SettingType)!;foreach(var value in converted)list.Add(value);WriteValue(native,list);}
                }
                catch(Exception error){writes[native.Entry]=new WriteState{Error=true,Message=error.GetBaseException().Message};dirty=true;}
            }
            void OpenList(IReadOnlyList<string> start)=>KitAlmanacList.Open(window!,native.Entry.Definition.Key,start,Error,ApplyItems,(parent,rect,value,changed)=>
            {
                if(element==typeof(bool))
                {var toggle=KitToggle.Create(rect,"Value",bool.TryParse(value,out bool on)&&on,state=>changed(state?"true":"false"),Vector2.zero,new Vector2(410,32),text("On"),text("Off"));toggle.Tooltip(native.Entry.Definition.Key,SettingHelp(native,native.Attributes.Description??native.Entry.Description.Description,value,""));return toggle.Dispose;}
                if(element.IsEnum)
                {Button? button=null;button=AlmanacButtons.Create(rect,"Value",value,()=>KitAlmanacChoice.Open(parent,element.Name,Enum.GetNames(element),new[]{value},false,chosen=>{changed(chosen[0]);var caption=button!.GetComponentInChildren<TMPro.TMP_Text>();if(caption!=null)caption.text=chosen[0];}),Vector2.zero,new Vector2(410,32));Help(button.gameObject,native.Entry.Definition.Key,SettingHelp(native,native.Attributes.Description??native.Entry.Description.Description,value,""));return ()=>UnityEngine.Object.Destroy(button.gameObject);}
                if(value.Length>KitInputLayout.TextLength||value.IndexOfAny(new[]{'\r','\n','\t'})>=0)
                {var button=AlmanacButtons.Create(rect,"Value",text("Edit text..."),()=>KitAlmanacTextEditor.Open(parent,element.Name,value,Error,changed),Vector2.zero,new Vector2(410,32));Help(button.gameObject,native.Entry.Definition.Key,SettingHelp(native,native.Attributes.Description??native.Entry.Description.Description,value,""));return ()=>UnityEngine.Object.Destroy(button.gameObject);}
                var field=KitTextField.Create(rect,"Value",KitFieldMode.Text,Vector2.zero,new Vector2(410,32));field.Text=value;field.Validator=text=>Error(text).Length==0;field.Ended+=edit=>{if(edit.Commit)changed(edit.Text);};Help(field.Input.gameObject,native.Entry.Definition.Key,SettingHelp(native,native.Attributes.Description??native.Entry.Description.Description,value,""));return field.Destroy;
            });
            // Rows of equal columns (recipe pairs, drop rows) open as a table first; "List view" keeps the plain list.
            var shape=element==typeof(string)?TableText.Detect(values,separator):null;
            if(shape!=null)AlmanacSyntax.OpenTable(window!,native.Entry.Definition.Key,shape,separator,Error,ApplyItems,OpenList);
            else OpenList(values);
            FollowDependency(native);
        }
        void FollowDependency(NativeEntry native)
        {
            var dialog=KitAlmanacDialog.Active;
            if(dialog==null||dialog.Parent!=window)return;
            void GuardEditor()
            {
                var dependency=EntryDependencies.Evaluate(native.Entry);
                if(Hidden(native.Entry)||!dependency.Enabled)dialog.Dispose();
            }
            dialog.Tick+=GuardEditor;
            dialog.Closed+=()=>dialog.Tick-=GuardEditor;
        }
        sealed partial class RowView
        {
            KitAlmanacNumber? number;
            readonly List<KitTextField> vectors=new List<KitTextField>();
            readonly List<TMPro.TMP_Text> vectorLabels=new List<TMPro.TMP_Text>();
            KitAlmanacDrawer? drawer;
            RectTransform? drawerRect;
            AlmanacUguiScope? ugui;string uguiValue="";bool uguiReadOnly;
            Button? editor,undo,choice,ownership;
            TMPro.TMP_Text? unit;
            int drawerErrors;bool drawerFailed;
            bool HasAdvanced=>number!=null||vectors.Count>0||editor!=null||drawer!=null||ugui!=null;
            bool BuildAdvanced(NativeEntry native,EditorKind kind)
            {
                if(kind==EditorKind.Slider||kind==EditorKind.Spinner)
                {
                    var choice=EditorSelector.Fallback(native.Shape,native.Attributes,native.Text());
                    number=KitAlmanacNumber.Create(rect,"Number",new Vector2(350,-3),new Vector2(220,30),native.Text(),text=>native.Parse(text).Valid,text=>owner.Write(model!.Native!,text),Step,
                        native.Shape.HasRange?native.Shape.RangeMin:(double?)null,native.Shape.HasRange?native.Shape.RangeMax:(double?)null,choice.Step??1,choice.Percent,
                        value=>owner.Serialize(native,Convert.ChangeType(value,native.Entry.SettingType,Inv)));
                    if(!string.IsNullOrEmpty(native.Attributes.Unit))
                    {unit=Label(rect,"Unit",native.Attributes.Unit!,Vector2.zero,new Vector2(48,30),14);unit.raycastTarget=true;KitTooltip.Set(unit.gameObject,owner.text("Unit"),native.Attributes.Unit!);}
                    return true;
                }
                if(kind==EditorKind.VectorRow)
                {
                    int count=EditorSelector.Fallback(native.Shape,native.Attributes,native.Text()).Fields;
                    string[] labels={"X","Y","Z","W"};
                    for(int i=0;i<count;i++)
                    {
                        int component=i;var field=KitTextField.Create(rect,"Component"+i,KitFieldMode.Text,new Vector2(350+i*55,-3),new Vector2(50,30));
                        field.Validator=text=>float.TryParse(text,NumberStyles.Float,Inv,out float value)&&!float.IsNaN(value)&&!float.IsInfinity(value);
                        field.Ended+=edit=>{if(edit.Commit&&field.Valid)ChangeVector(component,float.Parse(edit.Text,Inv));};vectors.Add(field);
                        vectorLabels.Add(Label(rect,"Label"+i,labels[i],Vector2.zero,new Vector2(18,30),14));
                    }
                    return true;
                }
                if(kind==EditorKind.UguiDrawer&&!drawerFailed)
                {
                    try{BuildUgui(native);name!.gameObject.SetActive(Presentation.ShowsSettingName(native.Attributes,true));return true;}
                    catch(Exception error){UguiFailed(native,error);return BuildAdvanced(native,EditorSelector.Fallback(native.Shape,native.Attributes,native.Text()).Kind);}
                }
                if(kind==EditorKind.CustomDrawer&&!drawerFailed)
                {
                    var node=new GameObject("CustomDrawer",typeof(RectTransform));node.transform.SetParent(rect,false);drawerRect=(RectTransform)node.transform;
                    try
                    {
                        drawer=KitAlmanacDrawer.Create(drawerRect,native.Entry,()=>native.CustomDrawer!.DynamicInvoke(native.Entry),
                            ()=>KitAlmanacDialog.Active==null&&!owner.Hidden(native.Entry)&&Locked(new NativeEntry(native.Entry),owner.Facts(native.Entry)).Length==0,
                            value=>owner.WriteValue(new NativeEntry(native.Entry),value),error=>
                            {
                                owner.writes[native.Entry]=new WriteState{Error=true,Message=error.Message};owner.dirty=true;
                                if(++drawerErrors>=2){drawerFailed=true;owner.failedDrawers.Add(native.Entry);drawer?.Dispose();drawer=null;Build();}
                            });
                        name!.gameObject.SetActive(Presentation.ShowsSettingName(native.Attributes,true));return true;
                    }
                    catch(Exception error){drawerFailed=true;owner.failedDrawers.Add(native.Entry);owner.writes[native.Entry]=new WriteState{Error=true,Message=error.GetBaseException().Message};owner.dirty=true;return BuildAdvanced(native,EditorSelector.Fallback(native.Shape,native.Attributes,native.Text()).Kind);}
                }
                if(kind==EditorKind.FlagsDropdown||kind==EditorKind.KeyCapture||kind==EditorKind.ShortcutCapture||kind==EditorKind.ColorPicker||kind==EditorKind.ListEditor||kind==EditorKind.MultilineText)
                {editor=AlmanacButtons.Create(rect,"Edit",owner.text("Edit..."),OpenEditor,new Vector2(350,-3),new Vector2(220,30));return true;}
                return false;
            }
            /// <summary>Builds the mod's uGUI drawer into a clipped area of the row; read-only while the entry is locked.</summary>
            void BuildUgui(NativeEntry native)
            {
                if(drawerRect!=null)UnityEngine.Object.Destroy(drawerRect.gameObject);
                var node=new GameObject("UguiDrawer",typeof(RectTransform),typeof(RectMask2D));node.transform.SetParent(rect,false);drawerRect=(RectTransform)node.transform;
                drawerRect.anchorMin=drawerRect.anchorMax=drawerRect.pivot=new Vector2(0,1);
                uguiReadOnly=native.Attributes.ReadOnly==true||Locked(native,owner.Facts(native.Entry)).Length>0;uguiValue=native.RawSerialized();
                var area=new GameObject("Content",typeof(RectTransform));area.transform.SetParent(drawerRect,false);var content=(RectTransform)area.transform;
                content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=new Vector2(owner.rightWidth-20,70);
                // shortcut: the drawer gets a fixed 70 px area (like the IMGUI host); taller drawers are clipped, measure first if one needs more.
                ugui=new AlmanacUguiScope(content,Math.Max(220,owner.rightWidth-380),uguiReadOnly,error=>UguiFailed(native,error));
                AlmanacUguiScope.Invoke(native.UguiDrawer!,ugui,native.Entry);
                if(drawerRect!=null)Reflow();
            }
            void UguiFailed(NativeEntry native,Exception error)
            {
                owner.writes[native.Entry]=new WriteState{Error=true,Message=error.GetBaseException().Message};owner.dirty=true;
                if(++drawerErrors>=2||ugui==null){drawerFailed=true;owner.failedDrawers.Add(native.Entry);}
            }
            string Step(int direction)
            {
                var native=model!.Native!;decimal step=(decimal)(native.Attributes.Step??(native.Shape.HasRange?(native.Shape.RangeMax-native.Shape.RangeMin)/100:1));
                if(native.Shape.Kind==SettingKind.Integer)step=Math.Max(1,step);
                var next=Convert.ToDecimal(native.Entry.BoxedValue,Inv)+step*direction;
                var value=Convert.ChangeType(next,native.Entry.SettingType,Inv);return owner.Serialize(native,value);
            }
            void OpenEditor()
            {
                var native=model!.Native!;var kind=drawerFailed?EditorSelector.Fallback(native.Shape,native.Attributes,native.Text()).Kind:native.Editor.Kind;
                if(rawMode||kind==EditorKind.MultilineText){owner.TextEditor(native,rawMode);return;}
                if(kind==EditorKind.FlagsDropdown||kind==EditorKind.Cycle||kind==EditorKind.Dropdown){owner.Choices(native,kind==EditorKind.FlagsDropdown);return;}
                if(kind==EditorKind.KeyCapture||kind==EditorKind.ShortcutCapture)
                {if(Locked(native,owner.Facts(native.Entry)).Length>0)return;KitAlmanacKeyCapture.Open(owner.window!,model.Title,kind==EditorKind.ShortcutCapture,chord=>owner.WriteValue(native,kind==EditorKind.ShortcutCapture?(object)chord:chord.MainKey));owner.FollowDependency(native);return;}
                if(kind==EditorKind.ColorPicker){if(Locked(native,owner.Facts(native.Entry)).Length>0)return;KitAlmanacColor.Open(owner.window!,model.Title,(Color)native.Entry.BoxedValue,value=>owner.WriteValue(native,value));owner.FollowDependency(native);return;}
                if(kind==EditorKind.ListEditor){owner.ListEditor(native);return;}
                owner.TextEditor(native,true);
            }
            float[] Vector()
            {
                object value=model!.Native!.Entry.BoxedValue;
                if(value is Vector2 a)return new[]{a.x,a.y};if(value is Vector3 b)return new[]{b.x,b.y,b.z};if(value is Vector4 c)return new[]{c.x,c.y,c.z,c.w};var q=(Quaternion)value;return new[]{q.x,q.y,q.z,q.w};
            }
            void ChangeVector(int component,float value)
            {
                var values=Vector();values[component]=value;var kind=model!.Native!.Shape.Kind;
                owner.WriteValue(model.Native,kind==SettingKind.Vector2?(object)new Vector2(values[0],values[1]):kind==SettingKind.Vector3?new Vector3(values[0],values[1],values[2]):kind==SettingKind.Vector4?new Vector4(values[0],values[1],values[2],values[3]):new Quaternion(values[0],values[1],values[2],values[3]));
            }
            internal void Reflow()
            {
                if(model==null||name==null)return;
                float width=owner.rightWidth-20;bool stacked=owner.rightWidth<620;
                if(undo==null&&model.Native!=null&&!model.Header)undo=AlmanacButtons.Create(rect,"Undo",owner.text("Undo"),()=>owner.UndoEntry(model.Native),Vector2.zero,new Vector2(RowButtons.Undo,30));
                // Each button as wide as its translated caption; the name column gives up the room first.
                float rawWidth=RowButtons.Width(RowButtons.Text,AlmanacButtons.CaptionWidth(raw)),resetWidth=RowButtons.Width(RowButtons.Reset,AlmanacButtons.CaptionWidth(reset)),undoWidth=RowButtons.Width(RowButtons.Undo,AlmanacButtons.CaptionWidth(undo));
                float buttons=RowButtons.Cluster(rawWidth,resetWidth,undoWidth);var columns=stacked?(X:8f,Value:Mathf.Max(100,width-8-buttons)):RowButtons.Columns(width,buttons);
                float x=columns.X,y=stacked?-38:-3,valueWidth=columns.Value;
                Set(name.rectTransform,new Vector2(8,-4),new Vector2(stacked?width:Mathf.Max(220,x-20),30));
                if(model.Header)
                {
                    float sectionWidth=RowButtons.Width(144,AlmanacButtons.CaptionWidth(reset));
                    Set(name.rectTransform,new Vector2(8,-4),new Vector2(width,30));if(cycle!=null)Set((RectTransform)cycle.transform,new Vector2(8,-2),new Vector2(Mathf.Max(120,width-sectionWidth-12),30));if(reset!=null)Set((RectTransform)reset.transform,new Vector2(width-sectionWidth,-2),new Vector2(sectionWidth,30));return;
                }
                if(field!=null)Set(field.Rect,new Vector2(x,y),new Vector2(valueWidth,30));
                if(toggle!=null)Set((RectTransform)toggle.Button.transform,new Vector2(x,y),new Vector2(Mathf.Clamp(valueWidth,80,110),30));
                if(cycle!=null)Set((RectTransform)cycle.transform,new Vector2(x,y),new Vector2(valueWidth,30));
                if(choice!=null){Set((RectTransform)cycle!.transform,new Vector2(x,y),new Vector2(Mathf.Max(50,valueWidth-32),30));Set((RectTransform)choice.transform,new Vector2(x+valueWidth-28,y),new Vector2(28,30));}
                if(editor!=null)Set((RectTransform)editor.transform,new Vector2(x,y),new Vector2(valueWidth,30));
                float at=x+valueWidth+6;
                if(raw!=null)Set((RectTransform)raw.transform,new Vector2(at,y),new Vector2(rawWidth,30));at+=rawWidth+4;
                if(reset!=null)Set((RectTransform)reset.transform,new Vector2(at,y),new Vector2(resetWidth,30));at+=resetWidth+4;
                if(undo!=null)Set((RectTransform)undo.transform,new Vector2(at,y),new Vector2(undoWidth,30));
                number?.Place(new Vector2(x,y),valueWidth);
                if(unit!=null&&number!=null)
                {
                    float unitWidth=Mathf.Min(48,Mathf.Max(0,valueWidth-100));
                    number.Field.Rect.sizeDelta=new Vector2(Mathf.Max(36,valueWidth-64-unitWidth),30);
                    Set(unit.rectTransform,new Vector2(x+valueWidth-32-unitWidth,y),new Vector2(unitWidth,30));
                }
                for(int i=0;i<vectors.Count;i++)
                {
                    float start=x+i*(valueWidth/vectors.Count);
                    Set(vectorLabels[i].rectTransform,new Vector2(start,y),new Vector2(18,30));
                    Set(vectors[i].Rect,new Vector2(start+18,y),new Vector2(Mathf.Max(20,valueWidth/vectors.Count-22),30));
                }
                if(drawerRect!=null)Set(drawerRect,new Vector2(x,y),new Vector2(valueWidth,70));
                float extra=number?.Slider!=null||drawer!=null||ugui!=null?40:0;
                float grown=owner.InlineDescriptions?model.DescriptionExtra:0;
                if(description!=null){Set(description.rectTransform,new Vector2(8,stacked?-76-extra:-40-extra),new Vector2(width,36+grown));description.gameObject.SetActive(owner.InlineDescriptions);}
                float descriptionSpace=(owner.InlineDescriptions?0:40)-grown;
                if(state!=null)Set(state.rectTransform,new Vector2(8,(stacked?-116-extra:-80-extra)+descriptionSpace),new Vector2(width,24));
                if(model.Native!=null&&ownership==null&&owner.CanChangeOwner(model.Native.Entry))ownership=AlmanacButtons.Create(rect,"Owner",owner.text("Owner"),()=>owner.ChangeOwner(model.Native.Entry),Vector2.zero,new Vector2(140,24));
                if(ownership!=null)
                {Set((RectTransform)ownership.transform,new Vector2(width-142,(stacked?-116-extra:-80-extra)+descriptionSpace),new Vector2(140,24));if(state!=null)state.rectTransform.sizeDelta=new Vector2(Math.Max(100,width-154),24);}
            }
            void RefreshEditors(bool can)
            {
                var native=model!.Native!;
                // A drawer shows the value it was built with: rebuild it after an outside change or a lock change.
                if(ugui!=null&&!drawerFailed&&(native.RawSerialized()!=uguiValue||(!can||native.Attributes.ReadOnly==true)!=uguiReadOnly))
                {try{BuildUgui(native);}catch(Exception error){UguiFailed(native,error);if(drawerFailed)Build();}}
                if(name!=null){var tag=SafeTagColor(native,"EntryColor");name.color=tag==Color.white?owner.MarkColor(native):tag;}
                if(description!=null)description.color=SafeTagColor(native,"DescriptionColor");
                if(number!=null){number.SetUsable(can);number.Set(model!.Native!.Text(),Convert.ToDouble(model.Native.Entry.BoxedValue,Inv));}
                var values=vectors.Count>0?Vector():Array.Empty<float>();for(int i=0;i<vectors.Count;i++){vectors[i].SetUsable(can);if(!vectors[i].Focused)vectors[i].Text=values[i].ToString("R",Inv);}
                if(editor!=null)editor.interactable=can;
                if(choice!=null)choice.interactable=can;
                if(undo!=null)undo.interactable=can&&owner.ChangedInSession(model!.Native!.Entry)&&!(owner.Facts(model.Native.Entry).ConnectedAsClient&&owner.Facts(model.Native.Entry).EntrySynced);
                if(ownership!=null){ownership.interactable=owner.CanChangeOwner(model!.Native!.Entry);var caption=ownership.GetComponentInChildren<TMPro.TMP_Text>();if(caption!=null)caption.text=owner.ownerLabel?.Invoke(model.Native.Entry)??(owner.Facts(model.Native.Entry).EntrySynced?owner.text("Owner: Host or server"):owner.text("Owner: Each player"));}
            }
            Color SafeTagColor(NativeEntry native,string member)
            {
                object? raw=native.Tags.First(member);
                Color tint;
                if(raw is Color color)tint=color;else if(raw is Color32 bytes)tint=bytes;else return Color.white;
                bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
                if(!Finite(tint.r)||!Finite(tint.g)||!Finite(tint.b)||!Finite(tint.a)||tint.a<=0)return Color.white;
                tint=new Color(Mathf.Clamp01(tint.r),Mathf.Clamp01(tint.g),Mathf.Clamp01(tint.b),Mathf.Clamp01(tint.a));
                var panel=owner.window?.Panel;
                var background=panel!=null?KitPanel.Background(panel):null;
                var sprite=background?.sprite;var texture=sprite?.texture;
                // Unknown/translucent backdrops cannot establish safe contrast; retain the kit's own text color.
                if(background==null||sprite==null||texture==null||!texture.isReadable||
                    panel!.GetComponent<CanvasGroup>() is CanvasGroup group&&group.alpha<.99f)return Color.white;
                try
                {
                    var bounds=sprite.textureRect;
                    float[] fractions={.25f,.5f,.75f};
                    foreach(float horizontal in fractions)foreach(float vertical in fractions)
                    {
                        int x=Mathf.Clamp(Mathf.FloorToInt(bounds.x+horizontal*bounds.width),0,texture.width-1);
                        int y=Mathf.Clamp(Mathf.FloorToInt(bounds.y+vertical*bounds.height),0,texture.height-1);
                        Color back=texture.GetPixel(x,y)*background.color;
                        if(back.a<.99f)return Color.white;
                        Color shown=Color.Lerp(back,tint,tint.a);
                        double a=Luminance(shown),b=Luminance(back);
                        if((Math.Max(a,b)+.05)/(Math.Min(a,b)+.05)<4.5)return Color.white;
                    }
                    return tint;
                }
                catch{return Color.white;}
            }
            static double Luminance(Color color)
            {
                double Linear(float value)=>value<=.04045f?value/12.92:Math.Pow((value+.055)/1.055,2.4);
                return .2126*Linear(color.r)+.7152*Linear(color.g)+.0722*Linear(color.b);
            }
            string DraftError(NativeEntry native)
            {
                if(field!=null&&!field.Valid)return native.Parse(InputText(field.Text)).Error??"";
                if(number!=null&&!number.Field.Valid)return native.Parse(number.Field.Text).Error??"";
                if(vectors.Any(vector=>!vector.Valid))return owner.text("Enter a number in every field.");
                return "";
            }
            void FocusEditors(int row)
            {
                if(number!=null){owner.window!.Focusable(number.Field.Input,1,row);owner.window.Focusable(number.Less,2,row);owner.window.Focusable(number.More,3,row);if(number.Slider!=null)owner.window.Focusable(number.Slider,6,row);}
                for(int i=0;i<vectors.Count;i++)owner.window!.Focusable(vectors[i].Input,i+1,row);
                if(editor!=null)owner.window!.Focusable(editor,1,row);if(undo!=null)owner.window!.Focusable(undo,4,row);if(choice!=null)owner.window!.Focusable(choice,5,row);if(ownership!=null)owner.window!.Focusable(ownership,7,row);
            }
            // Logout can destroy the canvas before managed row leases are released.
            void UnregisterFocus(){if(owner.window==null||rect==null)return;foreach(var control in rect.GetComponentsInChildren<Selectable>(true))owner.window.Unfocusable(control);}
            void ReleaseEditors(){number?.Dispose();number=null;foreach(var vector in vectors)vector.Destroy();vectors.Clear();vectorLabels.Clear();drawer?.Dispose();drawer=null;ugui=null;drawerRect=null;editor=undo=choice=ownership=null;unit=null;}
            internal bool Contains(Transform selected)=>selected.IsChildOf(rect);
            internal string Key=>model?.Key??"";
            internal void Reset(){if(model?.Native!=null)owner.Reset(model.Native);}
            internal void Nudge(int amount){number?.Step(amount);}
            internal bool Activate()
            {
                var selected=EventSystem.current?.currentSelectedGameObject;
                if(field!=null&&selected==field.Input.gameObject){field.Focus();return true;}
                if(number!=null&&selected==number.Field.Input.gameObject){number.Field.Focus();return true;}
                foreach(var vector in vectors)if(selected==vector.Input.gameObject){vector.Focus();return true;}
                return false;
            }
        }
    }
}
