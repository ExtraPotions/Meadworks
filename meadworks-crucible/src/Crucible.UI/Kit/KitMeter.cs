// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>Read-only kit meter for cooldowns, fishing tension and item upgrade chance. It never owns gameplay input.</summary>
    public sealed class KitMeter : IDisposable
    {
        public RectTransform Root { get; }
        public TMP_Text Label { get; }
        public float Fraction { get; private set; }
        readonly Image fill;
        bool disposed;

        KitMeter(RectTransform root,Image fill,TMP_Text label){Root=root;this.fill=fill;Label=label;}

        /// <summary>Top-left positioned meter, null before the graphical kit is active or on a dedicated server.</summary>
        public static KitMeter Create(RectTransform parent,string name,Vector2 at,Vector2 size)
        {
            if(!CrucibleUiPlugin.Active||parent==null)return null;
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));
            go.layer=parent.gameObject.layer;
            var root=(RectTransform)go.transform;
            root.SetParent(parent,false);
            root.anchorMin=root.anchorMax=root.pivot=new Vector2(0,1);
            root.anchoredPosition=at;
            root.sizeDelta=new Vector2(Mathf.Max(8,size.x),Mathf.Max(8,size.y));
            var track=go.GetComponent<Image>();
            track.color=KitUnity.Colour(KitPalette.SheetInset);
            track.raycastTarget=false;
            var inset=new GameObject("FillArea",typeof(RectTransform));
            inset.layer=go.layer;
            var insetRect=(RectTransform)inset.transform;
            insetRect.SetParent(root,false);
            insetRect.anchorMin=Vector2.zero;
            insetRect.anchorMax=Vector2.one;
            insetRect.offsetMin=new Vector2(2,2);
            insetRect.offsetMax=new Vector2(-2,-2);
            var bar=new GameObject("Fill",typeof(RectTransform),typeof(Image));
            bar.layer=go.layer;
            var rect=(RectTransform)bar.transform;
            rect.SetParent(insetRect,false);
            rect.anchorMin=Vector2.zero;
            rect.anchorMax=new Vector2(0,1);
            rect.offsetMin=rect.offsetMax=Vector2.zero;
            var fill=bar.GetComponent<Image>();
            fill.color=KitUnity.Colour(KitPalette.SheetValue);
            fill.raycastTarget=false;
            var label=KitCanvas.Text(root,"Label","",14,KitUnity.Colour(KitPalette.SheetLabel),TextAlignmentOptions.Center,false);
            var textRect=(RectTransform)label.transform;
            textRect.anchorMin=Vector2.zero;
            textRect.anchorMax=Vector2.one;
            textRect.offsetMin=new Vector2(4,0);
            textRect.offsetMax=new Vector2(-4,0);
            fill.enabled=false;
            return new KitMeter(root,fill,label);
        }

        /// <summary>Updates a clamped finite fraction; invalid or non-positive maxima display an empty meter.</summary>
        public void Set(float value,float maximum,string text="",Color? colour=null)
        {
            if(disposed||Root==null)return;
            Fraction=float.IsNaN(value)||float.IsInfinity(value)||float.IsNaN(maximum)||float.IsInfinity(maximum)||maximum<=0?0:Mathf.Clamp01(value/maximum);
            fill.enabled=Fraction>0;
            var rect=(RectTransform)fill.transform;
            rect.anchorMax=new Vector2(Fraction,1);
            fill.color=colour??KitUnity.Colour(KitPalette.SheetValue);
            Label.text=text??"";
        }

        public void Dispose(){if(disposed)return;disposed=true;if(Root!=null)UnityEngine.Object.Destroy(Root.gameObject);}
    }
}
