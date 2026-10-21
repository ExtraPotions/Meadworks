// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static class KitStructuredTip
    {
        /// <summary>Typed content on the existing tooltip owner; exact native topic/body remain the fallback.</summary>
        public static void Set(GameObject control,KitTipDocument document,string fallbackTopic,string fallbackText)
        {
            KitTooltip.Set(control,fallbackTopic,fallbackText);
            var presenter=control!=null?control.GetComponent<KitControlTip>():null;
            if(presenter==null)return;
            presenter.Structured=document;
            var scroll=control.GetComponent<KitStructuredTipScroll>()??control.AddComponent<KitStructuredTipScroll>();
            scroll.Owner=presenter;scroll.enabled=document!=null;
        }
        public static void Show(GameObject control,KitTipDocument document,string fallbackTopic,string fallbackText)
        {
            Set(control,document,fallbackTopic,fallbackText);
            var presenter=control!=null?control.GetComponent<KitControlTip>():null;
            if(presenter!=null){presenter.AllowTopicOnly=true;presenter.Begin();}
        }
    }
    internal sealed partial class KitControlTip
    {
        internal KitTipDocument Structured;
        string shownStructured;
        bool HasStructuredContent => Structured?.HasContent==true;
        string StructuredKey => Structured==null?"":Structured.Key+"|"+KitLook.On;
        internal void ClearStructured()
        {
            Structured=null;
            var scroll=GetComponent<KitStructuredTipScroll>();if(scroll!=null)scroll.enabled=false;
        }
        internal void InvalidateStructured()=>shownStructured=null;
        GameObject StructuredBox()
        {
            shownStructured=StructuredKey;
            if(Structured==null||!KitAppearance.Active)return null;
            return KitStructuredTipPanel.Show((RectTransform)transform,Structured,InvalidateStructured);
        }
        internal bool ScrollStructured(float delta)
        {
            var surface=box!=null?box.GetComponent<KitStructuredTipSurface>():null;
            return surface!=null && surface.Scroll(delta);
        }
    }
    internal sealed class KitStructuredTipScroll:MonoBehaviour,IScrollHandler
    {
        internal KitControlTip Owner;
        public void OnScroll(PointerEventData data)
        {
            if(Owner!=null && Owner.ScrollStructured(data.scrollDelta.y))return;
            // A short or not-yet-visible tooltip must not swallow its parent list's wheel event.
            if(transform.parent!=null)ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject,data,ExecuteEvents.scrollHandler);
        }
    }
}
