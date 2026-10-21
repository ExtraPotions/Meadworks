// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace ExtraPotions.Meadworks.Crucible.UI
{
    internal static class KitStructuredTipPanel
    {
        const float Padding=12,Spacing=6,TextSize=18;
        internal static GameObject Show(RectTransform anchor,KitTipDocument document,Action changed)
        {
            var canvas=anchor.GetComponentInParent<Canvas>()?.rootCanvas;
            var parent=canvas!=null?canvas.transform as RectTransform:null;
            if(parent==null || KitAppearance.Font(KitFontRole.Body)==null)return null;
            RectTransform panel=null;
            try
            {
                float availableWidth=parent.rect.width-2*KitStructuredTipLayout.Margin;
                float availableHeight=parent.rect.height-2*KitStructuredTipLayout.Margin;
                if(availableWidth<=64||availableHeight<=64)return null;
                float width=KitStructuredTipLayout.ColumnWidth(document.PreferredWidth,availableWidth);
                panel=KitPanel.Create(parent,"Crucible_structured_tip",new Vector2(width,availableHeight));
                if(panel==null)return null;
                KitForged.SidePlate(panel);
                var group=panel.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
                panel.anchorMin=panel.anchorMax=new Vector2(.5f,.5f);panel.pivot=new Vector2(0,1);
                float header=Padding;
                var content=SheetScroll.Make(panel,Padding,header);
                var scroll=content.GetComponentInParent<ScrollRect>();
                float inner=width-2*Padding;
                float height=Body(content,document,0,0,inner,0);
                int split=0;
                if(KitStructuredTipLayout.TwoColumns(width,availableWidth,height,availableHeight-header-Padding,document.Sections.Count))
                {
                    split=document.SplitAfter>0?document.SplitAfter:Math.Max(1,document.Sections.Count/2);
                    UnityEngine.Object.DestroyImmediate(content.gameObject);
                    UnityEngine.Object.DestroyImmediate(scroll.gameObject);
                    // Reuse the same clipped-sheet primitive at the wider final size.
                    width=width*2+KitStructuredTipLayout.Gap;
                    panel.sizeDelta=new Vector2(width,availableHeight);
                    var oldBar=panel.Find("scrollbar");if(oldBar!=null)UnityEngine.Object.DestroyImmediate(oldBar.gameObject);
                    content=SheetScroll.Make(panel,Padding,header);scroll=content.GetComponentInParent<ScrollRect>();
                    height=Body(content,document,0,0,inner,split);
                }
                float visible=KitStructuredTipLayout.ViewHeight(height,availableHeight-header-Padding);
                float total=header+visible+Padding;
                panel.sizeDelta=new Vector2(width,total);content.sizeDelta=new Vector2(0,height);
                var corners=new Vector3[4];anchor.GetWorldCorners(corners);
                Vector3 local=parent.InverseTransformPoint(corners[2]);
                var bounds=new KitRect(parent.rect.xMin,parent.rect.yMin,parent.rect.width,parent.rect.height);
                var at=KitCardLayout.FloatTopLeft(new KitVector(local.x,local.y),new KitVector(width,total),bounds,KitStructuredTipLayout.Margin);
                panel.anchoredPosition=new Vector2(at.X-parent.rect.center.x,at.Y-parent.rect.center.y);
                var surface=panel.gameObject.AddComponent<KitStructuredTipSurface>();surface.Bind(scroll,height,visible,changed);
                panel.SetAsLastSibling();return panel.gameObject;
            }
            catch(Exception error)
            {
                if(panel!=null)UnityEngine.Object.Destroy(panel.gameObject);
                CrucibleUiPlugin.Log?.Debug("Structured tooltip used native fallback: "+error.GetType().Name);
                return null;
            }
        }
        static float Body(RectTransform content,KitTipDocument document,float x,float y,float width,int split)
        {
            float top=y;
            if(document.Title.Length>0){var title=Text(content,"Topic",document.Title,TextSize+2,KitTipTone.Accent);float h=Measure(title,width);Place(title.rectTransform,x,top,width,h);top+=h+Spacing;}
            if(document.Body.Length>0){var body=Text(content,"Body",document.Body,TextSize,KitTipTone.Normal);float h=Measure(body,width);Place(body.rectTransform,x,top,width,h);top+=h+Spacing;}
            float left=top,right=top;
            for(int i=0;i<document.Sections.Count;i++)
            {
                bool second=split>0&&i>=split;
                float sx=second?x+width+2*Padding+KitStructuredTipLayout.Gap:x;
                if(second)right=Section(content,document.Sections[i],sx,right,width)+Spacing;
                else left=Section(content,document.Sections[i],sx,left,width)+Spacing;
            }
            return Mathf.Max(top,left,right);
        }
        static float Section(RectTransform parent,KitTipSection section,float x,float y,float width)
        {
            var root=section.Card?KitCard.Create(parent,"Card"):Node(parent,"Section");
            float pad=section.Card?KitCardLayout.Padding:0,inner=width-pad*2,at=pad;
            if(section.Title.Length>0){var title=Text(root,"Heading",section.Title,16,KitTipTone.Accent);float h=Measure(title,inner);Place(title.rectTransform,pad,at,inner,h);at+=h+Spacing;}
            foreach(var row in section.Rows)at+=Row(root,row,pad,at,inner)+Spacing;
            at+=pad;Place(root,x,y,width,at);return y+at;
        }
        static float Row(RectTransform parent,KitTipRow row,float x,float y,float width)
        {
            var root=Node(parent,"Row");float height=0;
            if(row.Kind==KitTipKind.Comparison)
            {
                float labelWidth=width*.4f,col=(width-labelWidth)/3;
                height=Mathf.Max(height,Cell(root,"Label",row.Label,0,labelWidth-Spacing,KitTipTone.Normal));
                height=Mathf.Max(height,Cell(root,"Before",row.Before,labelWidth,col-Spacing,KitTipTone.Muted));
                height=Mathf.Max(height,Cell(root,"After",row.Value,labelWidth+col,col-Spacing,row.Tone));
                height=Mathf.Max(height,Cell(root,"Delta",row.Delta,labelWidth+2*col,col,row.Tone));
            }
            else
            {
                float valueWidth=row.Value.Length>0?width*.3f:0;
                height=Cell(root,"Label",row.Label,0,width-(valueWidth>0?valueWidth+Spacing:0),row.Kind==KitTipKind.Status?row.Tone:KitTipTone.Normal);
                if(valueWidth>0)height=Mathf.Max(height,Cell(root,"Value",row.Value,width-valueWidth,valueWidth,row.Tone));
            }
            if(row.Note.Length>0){var note=Text(root,"Note",row.Note,16,KitTipTone.Muted);float h=Measure(note,width);Place(note.rectTransform,0,height+Spacing,width,h);height+=h+Spacing;}
            Place(root,x,y,width,height);return height;
        }
        static float Cell(RectTransform parent,string name,string value,float x,float width,KitTipTone tone)
        {var label=Text(parent,name,value,TextSize,tone);float h=Measure(label,width);Place(label.rectTransform,x,0,width,h);return h;}
        static TMP_Text Text(Transform parent,string name,string value,float size,KitTipTone tone)
        {
            var label=KitCanvas.Text(parent,name,KitLocalize.Text(value),size,Color(tone),TextAlignmentOptions.TopLeft,true);
            label.richText=true;label.raycastTarget=false;
            var font=KitAppearance.Font(KitFontRole.Body);if(font!=null)label.font=font;
            return label;
        }
        static Color Color(KitTipTone tone)=>KitAppearance.Color(tone==KitTipTone.Good?KitColorRole.Good:tone==KitTipTone.Bad?KitColorRole.Bad:
            tone==KitTipTone.Warning?KitColorRole.Warning:tone==KitTipTone.Muted?KitColorRole.Muted:tone==KitTipTone.Accent?KitColorRole.Accent:KitColorRole.Text);
        static float Measure(TMP_Text label,float width)
        {float height=Mathf.Max(22,label.GetPreferredValues(width,0).y);if(float.IsNaN(height)||float.IsInfinity(height))throw new InvalidOperationException("Nonfinite tooltip measurement.");return height;}
        static RectTransform Node(Transform parent,string name)
        {var go=new GameObject(name,typeof(RectTransform));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
        static void Place(RectTransform rect,float x,float y,float width,float height)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
    }
    internal sealed class KitStructuredTipSurface:MonoBehaviour
    {
        ScrollRect scroll;
        float contentHeight,viewHeight;
        Action changed;
        internal void Bind(ScrollRect value,float content,float view,Action invalidate)
        {scroll=value;contentHeight=content;viewHeight=view;changed=invalidate;KitAppearance.Changed+=Changed;}
        void Changed()=>changed?.Invoke();
        internal bool Scroll(float delta)
        {
            float range=contentHeight-viewHeight;if(scroll==null||range<=.5f)return false;
            scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+delta*scroll.scrollSensitivity/range);return true;
        }
        void Update(){if(ZInput.IsGamepadActive())Scroll(-ZInput.GetJoyRightStickY()*Time.unscaledDeltaTime*12);}
        void OnDestroy(){KitAppearance.Changed-=Changed;changed=null;}
    }
}
