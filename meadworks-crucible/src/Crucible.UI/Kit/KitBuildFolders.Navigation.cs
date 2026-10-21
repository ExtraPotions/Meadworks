// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Existing C07 GridNavigator assigns native Unity neighbors; Valheim owns move/submit/repeat and TextInput.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace ExtraPotions.Meadworks.Crucible.UI {
    public sealed partial class KitBuildFolders {
        readonly Dictionary<Selectable,(Navigation Before,Navigation Written)> folderNavigation=new Dictionary<Selectable,(Navigation,Navigation)>();
        readonly Dictionary<GridCell,Selectable> folderControls=new Dictionary<GridCell,Selectable>();
        string rememberedFolder;
        bool rememberedCrumb,rememberedCreate;
        Player navigationActor;
        long navigationSession=-1;
        static readonly System.Reflection.MethodInfo FocusScroll=AccessTools.Method(typeof(BuildUi),"FocusScrollingAt");

        KitFolderTarget RenameTarget() {
            var selected=EventSystem.current?.currentSelectedGameObject?.GetComponentInParent<KitFolderTarget>();
            if(selected!=null&&selected.Panel==this&&(MeadworksInput.GamepadActive||KitFolderTarget.Hovered==null))return selected;
            return KitFolderTarget.Hovered;
        }
        void RememberNavigationFocus() {
            var selected=EventSystem.current?.currentSelectedGameObject;
            if(selected==null)return;
            if(create!=null&&selected==create.gameObject) {rememberedCreate=true;rememberedFolder=null;return;}
            var target=selected.GetComponentInParent<KitFolderTarget>();
            if(target==null||target.Panel!=this||target.Line==null) {rememberedFolder=null;rememberedCreate=false;return;}
            rememberedCreate=false;rememberedFolder=target.Line.Id;
            rememberedCrumb=strip!=null&&selected.transform.IsChildOf(strip);
        }
        void TickFolderNavigation() {
            var actor=Player.m_localPlayer;long session=ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation;
            if(!ReferenceEquals(actor,navigationActor)||session!=navigationSession) {ClearFolderNavigation();dirty=true;}
            navigationActor=actor;navigationSession=session;
        }
        void RestoreFolderNavigation() {
            foreach(var pair in folderNavigation) {
                if(pair.Key==null)continue;
                var now=pair.Key.navigation;var before=pair.Value.Before;var applied=pair.Value.Written;
                if(now.mode==applied.mode)now.mode=before.mode;
                if(now.selectOnUp==applied.selectOnUp)now.selectOnUp=before.selectOnUp;
                if(now.selectOnDown==applied.selectOnDown)now.selectOnDown=before.selectOnDown;
                if(now.selectOnLeft==applied.selectOnLeft)now.selectOnLeft=before.selectOnLeft;
                if(now.selectOnRight==applied.selectOnRight)now.selectOnRight=before.selectOnRight;
                pair.Key.navigation=now;
            }
            folderNavigation.Clear();folderControls.Clear();
        }
        void ClearFolderNavigation() {
            var selected=EventSystem.current?.currentSelectedGameObject;
            if(selected!=null&&((create!=null&&selected==create.gameObject)||selected.GetComponentInParent<KitFolderTarget>()?.Panel==this))
                EventSystem.current.SetSelectedGameObject(null);
            RestoreFolderNavigation();rememberedFolder=null;rememberedCreate=false;
        }
        void WriteNavigation(Selectable control,Navigation next) {
            if(control==null)return;
            folderNavigation[control]=(control.navigation,next);control.navigation=next;
        }
        static bool UsableFolderControl(Selectable control)=>control!=null&&control.gameObject.activeInHierarchy&&control.IsInteractable();
        void ConfigureFolderNavigation() {
            if(!Showing||strip==null||dirty)return;
            RestoreFolderNavigation();
            if(create!=null)folderControls[new GridCell(0,0)]=create;
            for(int i=0;i<rows.Count&&i<buttons.Count;i++)if(buttons[i]!=null)folderControls[new GridCell(0,i+1)]=buttons[i].GetComponent<Button>();
            for(int i=0;i<crumbs.Count;i++)if(crumbs[i]!=null)folderControls[new GridCell(i+1,0)]=crumbs[i];
            var grid=new GridNavigator(Math.Max(1,crumbs.Count+1),Math.Max(1,rows.Count+1),cell=>folderControls.TryGetValue(cell,out var control)&&UsableFolderControl(control));
            Selectable Neighbor(GridCell cell,GridCell direction) {
                var next=grid.Move(cell,direction);return next!=cell&&folderControls.TryGetValue(next,out var control)?control:null;
            }
            var pieces=Field<RectTransform>("m_pieceButtonsContainer");
            Selectable firstPiece=null;
            if(pieces!=null)foreach(var button in pieces.GetComponentsInChildren<Button>(false))if(UsableFolderControl(button)){firstPiece=button;break;}
            var search=Field<Selectable>("m_searchField");
            foreach(var pair in folderControls) {
                if(pair.Value==null)continue;
                var cell=pair.Key;var nav=pair.Value.navigation;nav.mode=Navigation.Mode.Explicit;
                nav.selectOnUp=Neighbor(cell,new GridCell(0,-1))??search;
                nav.selectOnDown=Neighbor(cell,new GridCell(0,1))??(cell.X>0?firstPiece:null);
                nav.selectOnLeft=Neighbor(cell,new GridCell(-1,0));
                nav.selectOnRight=Neighbor(cell,new GridCell(1,0))??firstPiece;
                WriteNavigation(pair.Value,nav);
                var focus=pair.Value.GetComponent<KitFolderNavigationFocus>()??pair.Value.gameObject.AddComponent<KitFolderNavigationFocus>();focus.Owner=this;
            }
            Selectable entry=create;
            for(int i=0;i<rows.Count&&i<buttons.Count;i++)if(rows[i].Current&&buttons[i]!=null){entry=buttons[i].GetComponent<Button>();break;}
            var all=Field<BuildUiTagButton>("m_showAllTagsButton");var hidden=all!=null?all.GetComponent<Selectable>():null;
            if(entry!=null&&hidden!=null)foreach(var control in menu.GetComponentsInChildren<Selectable>(false)) {
                if(folderNavigation.ContainsKey(control)||control==hidden)continue;
                var nav=control.navigation;bool changed=false;
                if(nav.selectOnLeft==hidden){nav.selectOnLeft=entry;changed=true;}
                if(nav.selectOnRight==hidden){nav.selectOnRight=entry;changed=true;}
                if(nav.selectOnUp==hidden){nav.selectOnUp=entry;changed=true;}
                if(nav.selectOnDown==hidden){nav.selectOnDown=entry;changed=true;}
                if(changed)WriteNavigation(control,nav);
            }
            if(rememberedCreate&&create!=null)SelectFolderControl(create);
            else if(rememberedFolder!=null) {
                Selectable restored=null;
                foreach(var pair in folderControls) {
                    var target=pair.Value!=null?pair.Value.GetComponent<KitFolderTarget>():null;
                    if(target!=null&&target.Line!=null&&target.Line.Id==rememberedFolder&&(pair.Key.X>0)==rememberedCrumb){restored=pair.Value;break;}
                }
                SelectFolderControl(restored??entry);
            }
            rememberedFolder=null;rememberedCreate=false;
        }
        static void SelectFolderControl(Selectable control) {
            if(UsableFolderControl(control)&&EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(control.gameObject);
        }
        internal void FolderControlFocused(Selectable control) {
            if(!Showing||control==null||TextInput.IsVisible())return;
            var scroll=Field<ScrollRect>("m_tagListScrollRect");
            if(scroll!=null&&control.transform.IsChildOf(scroll.content)&&FocusScroll!=null)
                Safe(()=> {Canvas.ForceUpdateCanvases();FocusScroll.Invoke(null,new object[]{control.transform as RectTransform,scroll});});
        }
        internal static void NativeFolderNavigation(BuildUi nativeMenu) {
            if(active==null||active.menu!=nativeMenu||!active.Showing)return;
            active.RememberNavigationFocus();active.ConfigureFolderNavigation();
        }
    }
    public sealed class KitFolderNavigationFocus:MonoBehaviour,ISelectHandler {
        internal KitBuildFolders Owner;
        public void OnSelect(BaseEventData data)=>Owner?.FolderControlFocused(GetComponent<Selectable>());
    }
}
