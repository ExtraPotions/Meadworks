// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// AX12/13: native GuiInputField composition; Save/Cancel/validation follows the existing YamlEditorWindow boundary.
// No IMGUI YAML implementation is copied. The owning product supplies validation and safe commit.
using System;
using GUIFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public sealed class KitAlmanacMultiline : IDisposable
    {
        static readonly System.Collections.Generic.HashSet<KitAlmanacMultiline> fields=new System.Collections.Generic.HashSet<KitAlmanacMultiline>();
        static bool registered;
        public GuiInputField Input {get;}
        public string Text {get=>Input!=null?Input.text:"";set{if(Input!=null)Input.SetTextWithoutNotify(value??"");}}
        public bool Focused=>Input!=null&&Input.gameObject.activeInHierarchy&&Input.isFocused;
        /// <summary>
        /// Optional coloring: returns TextMeshPro rich text whose visible characters equal the input (tags only, '&lt;' escaped).
        /// It is drawn over the field's own transparent text, so caret, selection and wrapping stay the native field's.
        /// </summary>
        public Func<string,string> Highlight {get;set;}
        TMP_Text overlay;string highlighted;Color baseColor;
        readonly TMP_Text numbers;
        readonly RectTransform gutter;
        string numbered;
        bool disposed;
        KitAlmanacMultiline(GuiInputField input,TMP_Text numbers,RectTransform gutter){Input=input;this.numbers=numbers;this.gutter=gutter;}
        public static KitAlmanacMultiline Create(RectTransform parent,string name,Vector2 at,Vector2 size,string value,Action<string> changed=null,bool lineNumbers=true)
        {
            var template=KitTextField.Create(parent,name+"Template",KitFieldMode.Text,at,size);
            if(template==null)return null;
            var clone=UnityEngine.Object.Instantiate(template.Input.gameObject,parent,false);clone.name=name;
            template.Destroy();var input=clone.GetComponent<GuiInputField>();
            var box=(RectTransform)clone.transform;KitAlmanacDialog.Place(box,at,size);input.textViewport.offsetMin=new Vector2(KitMultilineLayout.TextLeft(lineNumbers),input.textViewport.offsetMin.y);
            input.onValueChanged=new TMP_InputField.OnChangeEvent();input.onEndEdit=new TMP_InputField.SubmitEvent();
            input.onSelect=new TMP_InputField.SelectionEvent();input.OnInputSubmit=new OnInputSubmitEvent();
            input.onValidateInput=null;input.characterLimit=0;input.richText=false;
            input.lineType=TMP_InputField.LineType.MultiLineNewline;input.onFocusSelectAll=false;
            KitCanvas.HangulWordBreaks();
            input.textComponent.textWrappingMode=TextWrappingModes.Normal;
            input.textComponent.alignment=TextAlignmentOptions.TopLeft;input.textComponent.fontSize=16;
            input.SetTextWithoutNotify(value??"");input.onValueChanged.AddListener(current=>changed?.Invoke(current));
            RectTransform gutter=null;TMP_Text numbers=null;
            if(lineNumbers)
            {
                // Inside the field box, level with the text area, so the numbers never sit outside the box.
                var side=new GameObject(name+"Gutter",typeof(RectTransform),typeof(RectMask2D));side.layer=parent.gameObject.layer;side.transform.SetParent(box,false);
                gutter=(RectTransform)side.transform;gutter.anchorMin=new Vector2(0,0);gutter.anchorMax=new Vector2(0,1);gutter.pivot=new Vector2(0,1);
                gutter.offsetMin=new Vector2(KitMultilineLayout.GutterLeft,KitInputLayout.InsetY);gutter.offsetMax=new Vector2(KitMultilineLayout.GutterLeft+KitMultilineLayout.Gutter,-KitInputLayout.InsetY);
                numbers=KitAlmanacDialog.Label(gutter,name+"Lines","",Vector2.zero,new Vector2(KitMultilineLayout.Gutter,size.y),16);numbers.raycastTarget=false;
            }
            var made=new KitAlmanacMultiline(input,numbers,gutter);
            fields.Add(made);
            if(!registered){registered=true;MeadworksInput.AddTypingWindow(()=>{foreach(var field in fields)if(field.Focused)return true;return false;});}
            var life=clone.AddComponent<KitAlmanacMultilineLifetime>();life.Editor=made;made.Tick();return made;
        }
        internal void Tick()
        {
            if(Input==null)return;
            // GuiInputField.Start adds the game's single-line validator; this editor owns a multiline field.
            Input.onValidateInput=(current,index,added)=>added=='\0'?'\0':added;
            UpdateHighlight();
            if(numbers==null)return;
            // Match the native text's wrapped visual lines and its own caret/scroll offset.
            Input.textComponent.ForceMeshUpdate();var info=Input.textComponent.textInfo;
            if(numbered!=Text)
            {
                numbered=Text;var labels=new System.Text.StringBuilder();int logical=1;
                for(int i=0;i<Math.Min(info.lineCount,10000);i++)
                {
                    bool first=i==0||info.lineInfo[i].firstCharacterIndex>0&&info.characterInfo[info.lineInfo[i].firstCharacterIndex-1].character=='\n';
                    if(first)labels.Append(logical++);labels.Append('\n');
                }
                numbers.text=labels.ToString();
            }
            numbers.rectTransform.sizeDelta=new Vector2(KitMultilineLayout.Gutter,Mathf.Max(gutter.rect.height,Input.textComponent.preferredHeight));
            numbers.rectTransform.anchoredPosition=new Vector2(0,Input.textComponent.rectTransform.anchoredPosition.y+Input.textViewport.anchoredPosition.y);
        }
        void UpdateHighlight()
        {
            var text=Input.textComponent;
            if(Highlight==null){if(overlay!=null&&overlay.gameObject.activeSelf){overlay.gameObject.SetActive(false);text.color=baseColor;highlighted=null;}return;}
            if(overlay==null)
            {
                baseColor=text.color;
                var copy=UnityEngine.Object.Instantiate(text.gameObject,text.transform.parent,false);copy.name=text.gameObject.name+"Highlight";
                foreach(Transform child in copy.transform)UnityEngine.Object.Destroy(child.gameObject);
                copy.transform.SetSiblingIndex(text.transform.GetSiblingIndex()+1);
                overlay=copy.GetComponent<TMP_Text>();overlay.richText=true;overlay.raycastTarget=false;overlay.color=baseColor;
            }
            if(highlighted!=Text)
            {
                highlighted=Text;string rich=null;
                try{rich=Highlight(Text);}catch{rich=null;}
                bool on=rich!=null;overlay.gameObject.SetActive(on);
                if(on)overlay.text=rich;
                text.color=on?new Color(baseColor.r,baseColor.g,baseColor.b,0f):baseColor;
            }
            var from=text.rectTransform;var to=overlay.rectTransform;
            to.anchorMin=from.anchorMin;to.anchorMax=from.anchorMax;to.pivot=from.pivot;to.sizeDelta=from.sizeDelta;to.anchoredPosition=from.anchoredPosition;
            overlay.fontSize=text.fontSize;overlay.textWrappingMode=text.textWrappingMode;overlay.margin=text.margin;
        }
        public void SetUsable(bool value){if(Input!=null){Input.interactable=true;Input.readOnly=!value;}}
        public void Dispose(){if(disposed)return;disposed=true;fields.Remove(this);if(Input!=null){Input.gameObject.SetActive(false);UnityEngine.Object.Destroy(Input.gameObject);}}
    }
    public sealed class KitAlmanacMultilineLifetime : MonoBehaviour
    {
        internal KitAlmanacMultiline Editor;
        void LateUpdate(){Editor?.Tick();}
        void OnDestroy(){Editor?.Dispose();Editor=null;}
    }
    /// <summary>Optional text editor extras: coloring (see <see cref="KitAlmanacMultiline.Highlight"/>) and one owner action button.</summary>
    public sealed class KitTextEditorOptions
    {
        public Func<string,string> Highlight {get;set;}
        public string ExtraLabel {get;set;}
        /// <summary>Receives the current text and a setter that replaces it (ignored when read-only).</summary>
        public Action<string,Action<string>> Extra {get;set;}
    }
    public static class KitAlmanacTextEditor
    {
        public static KitAlmanacDialog Open(KitWindow parent,string title,string initial,Func<string,string> validate,Action<string> save,bool readOnly=false)
            =>Open(parent,title,initial,validate,save,readOnly,null);
        /// <summary>As <see cref="Open(KitWindow,string,string,Func{string,string},Action{string},bool)"/> with coloring and one optional extra button.</summary>
        public static KitAlmanacDialog Open(KitWindow parent,string title,string initial,Func<string,string> validate,Action<string> save,bool readOnly,KitTextEditorOptions options)
        {
            var dialog=KitAlmanacDialog.Create(parent,title,new Vector2(760,560));if(dialog==null)return null;
            var message=KitAlmanacDialog.Label(dialog.Panel,"Validation","",new Vector2(20,-470),new Vector2(720,32),14);
            KitAlmanacMultiline editor=null;
            void Validate(string value)
            {try{message.text=validate?.Invoke(value)??"";}catch(Exception error){message.text=error.GetBaseException().Message;}}
            editor=KitAlmanacMultiline.Create(dialog.Panel,"Text",new Vector2(20,-58),new Vector2(720,400),initial,Validate);
            editor.Highlight=options?.Highlight;
            editor.SetUsable(!readOnly);dialog.TypingField=()=>editor.Focused;dialog.Own(editor.Dispose);Validate(initial);
            Button extra=null;
            if(options?.Extra!=null&&!string.IsNullOrEmpty(options.ExtraLabel))
                extra=KitButton.Create(dialog.Panel,"Extra",options.ExtraLabel,()=>options.Extra(editor.Text,value=>{if(readOnly)return;editor.Input.text=value??"";}),new Vector2(20,-510),new Vector2(140,32));
            var apply=KitButton.Create(dialog.Panel,"Save","Save",()=>
            {
                Validate(editor.Text);if(message.text.Length!=0)return;
                try{save?.Invoke(editor.Text);dialog.Dispose();}
                catch(Exception error){message.text=error.GetBaseException().Message;}
            },new Vector2(540,-510),new Vector2(90,32));apply.interactable=!readOnly;
            var cancel=KitButton.Create(dialog.Panel,"Cancel","Cancel",dialog.Dispose,new Vector2(640,-510),new Vector2(100,32));
            dialog.Window.Focusable(editor.Input,0,0);dialog.Window.Focusable(apply,0,1);dialog.Window.Focusable(cancel,1,1);
            if(extra!=null)dialog.Window.Focusable(extra,2,1);
            return dialog;
        }
        public static void Confirm(KitWindow parent,string title,string detail,Action confirmed)
        {
            var dialog=KitAlmanacDialog.Create(parent,title,new Vector2(640,430));if(dialog==null)return;
            var preview=KitAlmanacMultiline.Create(dialog.Panel,"Detail",new Vector2(20,-55),new Vector2(600,300),detail);preview.SetUsable(false);dialog.Own(preview.Dispose);
            var yes=KitButton.Create(dialog.Panel,"Confirm","Confirm",()=>{dialog.Dispose();confirmed();},new Vector2(380,-380),new Vector2(100,32));
            var no=KitButton.Create(dialog.Panel,"Cancel","Cancel",dialog.Dispose,new Vector2(490,-380),new Vector2(130,32));dialog.Window.Focusable(yes,0,0);dialog.Window.Focusable(no,1,0);
        }
    }
}
