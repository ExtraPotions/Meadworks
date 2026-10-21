// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Native menu attachment: a copy of the game's own Settings entry, placed above START GAME.
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Almanac.Runtime.Input
{
    /// <summary>Owns only Almanac's cloned buttons and navigation references to those buttons.</summary>
    internal sealed class AlmanacMenus : IDisposable
    {
        const string Name="Meadworks_Almanac_ModSettings";
        readonly Action open;
        readonly Func<string> caption;
        readonly List<Link> links=new List<Link>();
        FejdStartup? mainOwner;
        Button? main;
        // The in-world pause menu has one MeadWorks entry for every product (Crucible MeadworksMenu); Almanac registers with it.
        IDisposable? entry;
        public AlmanacMenus(Action open,Func<string> caption){this.open=open;this.caption=caption;}
        static T? Read<T>(object owner,string field) where T:class=>AccessTools.Field(owner.GetType(),field)?.GetValue(owner) as T;
        public void Tick(bool enabled=true)
        {
            if(!enabled){ReleaseMain();ReleaseEntry();return;}
            if(entry==null)entry=MeadworksMenu.Register("almanac",caption,open,null,10);
            if(FejdStartup.instance!=mainOwner||main==null){ReleaseMain();AttachMain(FejdStartup.instance);}
            if(main==null)return;
            string words=caption();
            if(words!=shownCaption){shownCaption=words;foreach(var text in main.GetComponentsInChildren<TMPro.TMP_Text>(true))text.text=words;}
            Place();
        }
        string? shownCaption;
        Button? first;
        Vector3 placedFor=new Vector3(float.NaN,0,0);
        void AttachMain(FejdStartup owner)
        {
            if(owner==null)return;
            var list=Read<GameObject>(owner,"m_menuList");var buttons=Read<Button[]>(owner,"m_menuButtons");
            if(list==null||!(list.transform is RectTransform parent)||buttons==null||buttons.Length==0)return;
            Button? source=buttons.FirstOrDefault(b=>b!=null&&Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="OnButtonSettings"));
            source??=buttons.FirstOrDefault(b=>b!=null);if(source==null||parent.Find(Name)!=null)return;
            // START GAME: the native default entry, which the new button sits above.
            first=buttons.FirstOrDefault(b=>b!=null);
            main=Make(source,parent);mainOwner=owner;shownCaption=null;placedFor=new Vector3(float.NaN,0,0);
            // Native FejdStartup caches this array in Awake; adding a child alone leaves it unreachable by keys and pad.
            // Appended, not prepended: index 0 stays START GAME, the native default selection.
            AccessTools.Field(typeof(FejdStartup),"m_menuButtons").SetValue(owner,buttons.Concat(new[]{main}).ToArray());
            if(first!=null)SpliceAbove(first,main);
        }
        Button Make(Button source,RectTransform parent)
        {
            // A copy of the native Settings entry keeps its display font, size, colors and hover: only its gamepad key
            // and localizer come off. GameButtons.Make would apply the kit's small tab caption sizing.
            var go=UnityEngine.Object.Instantiate(source.gameObject,parent);go.name=Name;
            foreach(var pad in go.GetComponentsInChildren<UIGamePad>(true))
            {
                if(pad.m_hint!=null&&pad.m_hint.transform.IsChildOf(go.transform))UnityEngine.Object.DestroyImmediate(pad.m_hint);
                UnityEngine.Object.DestroyImmediate(pad);
            }
            foreach(var localize in go.GetComponentsInChildren<Localize>(true))UnityEngine.Object.DestroyImmediate(localize);
            var button=go.GetComponent<Button>();
            button.onClick=new Button.ButtonClickedEvent();button.onClick.AddListener(()=>{try{open();}catch(Exception error){Debug.LogError("Almanac: Mod settings failed: "+error);}});
            button.navigation=new Navigation{mode=Navigation.Mode.Explicit};
            // Outside the column's layout: it floats above the menu's top ornament instead of joining the list under QUIT.
            (go.GetComponent<LayoutElement>()??go.AddComponent<LayoutElement>()).ignoreLayout=true;
            go.SetActive(true);
            return button;
        }
        /// <summary>Above START GAME and the ornament line over it; follows the column when the layout or resolution moves it.</summary>
        void Place()
        {
            if(main==null||first==null||!(first.transform is RectTransform anchor))return;
            if(anchor.position==placedFor)return;
            placedFor=anchor.position;
            var rect=(RectTransform)main.transform;var parent=(RectTransform)rect.parent;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0.5f,0.5f);rect.sizeDelta=anchor.rect.size;rect.localScale=anchor.localScale;
            var corners=new Vector3[4];anchor.GetWorldCorners(corners);
            float top=parent.InverseTransformPoint(corners[1]).y,x=parent.InverseTransformPoint(anchor.position).x;
            float ornament=OrnamentTop(anchor,parent,top);
            rect.anchoredPosition=Vector2.zero;
            var center=new Vector2(x,Mathf.Max(top,ornament)+anchor.rect.height*0.5f+Gap);
            // Convert the parent-local center to anchoredPosition (anchors at the parent's center).
            rect.anchoredPosition=center-parent.rect.center;
        }
        const float Gap=6f;
        /// <summary>The top of the decoration line drawn just above START GAME, or <paramref name="top"/> when there is none.</summary>
        static float OrnamentTop(RectTransform anchor,RectTransform parent,float top)
        {
            var root=parent.parent!=null?parent.parent:parent;float best=top;var corners=new Vector3[4];
            float height=anchor.rect.height;
            foreach(var image in root.GetComponentsInChildren<Image>(false))
            {
                if(image.GetComponentInParent<Selectable>()!=null||!(image.transform is RectTransform rect))continue;
                rect.GetWorldCorners(corners);
                float bottom=parent.InverseTransformPoint(corners[0]).y,upper=parent.InverseTransformPoint(corners[1]).y;
                float left=parent.InverseTransformPoint(corners[0]).x,right=parent.InverseTransformPoint(corners[2]).x,middle=parent.InverseTransformPoint(anchor.position).x;
                // A thin rule centered over the column within one entry's height above START GAME; not a full-column backdrop.
                if(upper-bottom>height||bottom<top-height*0.5f||bottom>top+height||left>middle||right<middle)continue;
                best=Mathf.Max(best,upper);
            }
            return best;
        }
        void SpliceAbove(Button next,Button owned)
        {
            var before=next.navigation;
            // Automatic navigation would be lost by making it explicit; keep only our own links then.
            owned.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnDown=next,selectOnRight=before.selectOnRight};
            if(before.mode!=Navigation.Mode.Explicit||before.selectOnUp==owned)return;
            Selectable? above=before.selectOnUp;
            var own=owned.navigation;own.selectOnUp=above;owned.navigation=own;
            var modified=before;modified.selectOnUp=owned;
            Remember(next,owned,before,modified);next.navigation=modified;
            if(above!=null&&above.navigation.selectOnDown==next)
            {var original=above.navigation;var changed=original;changed.selectOnDown=owned;Remember(above,owned,original,changed);above.navigation=changed;}
        }
        void Remember(Selectable control,Button owned,Navigation before,Navigation after)
        {
            // Replace an old record when native navigation was rebuilt; never restore an obsolete snapshot.
            links.RemoveAll(l=>l.Control==control&&l.Owned==owned);links.Add(new Link(control,owned,before,after));
        }
        void Restore(Button? owned)
        {
            if(ReferenceEquals(owned,null))return;
            if(owned!=null&&owned.transform.parent!=null){
                var before=owned.navigation.selectOnUp;var after=owned.navigation.selectOnDown;
                foreach(var control in owned.transform.parent.GetComponentsInChildren<Selectable>(true)){
                    if(control==null||control==owned)continue;
                    var current=control.navigation;
                    if(current.selectOnUp==owned)current.selectOnUp=before==owned?null:before;
                    if(current.selectOnDown==owned)current.selectOnDown=after==owned?null:after;
                    control.navigation=current;
                }
            }
            foreach(var link in links.Where(l=>ReferenceEquals(l.Owned,owned)).ToArray())
            {
                if(link.Control==null)continue;var current=link.Control.navigation;
                if(current.Equals(link.After))link.Control.navigation=link.Before;
                else
                {
                    // Preserve later native/other-mod edits; remove only references to our disappearing button.
                    if(current.selectOnUp==owned)current.selectOnUp=link.Before.selectOnUp;
                    if(current.selectOnDown==owned)current.selectOnDown=link.Before.selectOnDown;
                    link.Control.navigation=current;
                }
            }
            links.RemoveAll(l=>ReferenceEquals(l.Owned,owned));
        }
        void ReleaseMain()
        {
            Restore(main);
            if(mainOwner!=null&&main!=null)
            {
                var field=AccessTools.Field(typeof(FejdStartup),"m_menuButtons");
                if(field.GetValue(mainOwner) is Button[] buttons)field.SetValue(mainOwner,buttons.Where(b=>b!=main).ToArray());
            }
            if(main!=null)UnityEngine.Object.Destroy(main.gameObject);main=null;mainOwner=null;first=null;
        }
        void ReleaseEntry(){entry?.Dispose();entry=null;}
        public void Dispose(){ReleaseMain();ReleaseEntry();links.Clear();}
        sealed class Link
        {
            internal Link(Selectable control,Button owned,Navigation before,Navigation after){Control=control;Owned=owned;Before=before;After=after;}
            internal readonly Selectable Control;internal readonly Button Owned;internal readonly Navigation Before,After;
        }
    }
}
