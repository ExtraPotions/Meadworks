// OpenKeep TabPicks/TabClicks/TabBox. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/Blueprints/Tab/.
// Changed by expDARE, 2026: per-owner opaque ids, current actor/session lifecycle, kit-owned native marks and viewport.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace ExtraPotions.Meadworks.Crucible.UI {
    public sealed partial class KitBuildFolders {
        readonly KitBuildTabPicks picks = new KitBuildTabPicks();
        readonly List<KitBuildPickMark> marks = new List<KitBuildPickMark>();
        KitBuildSelectionBox selectionBox;
        Player pickActor;
        long pickSession = -1;
        internal int PickGeneration { get; private set; }
        public IReadOnlyList<string> PickedIds => picks.Snapshot();
        internal static bool Ctrl => MeadworksInput.CtrlHeld;
        static bool Shift => MeadworksInput.ShiftHeld;
        internal bool BeforeClick(string id) {
            if (!Showing || TextInput.IsVisible()) return false;
            if (!tab.Draggable(id)) { ClearPicks(); return false; }
            picks.SetOrder(tab.PickOrder());
            if (Ctrl || Shift) { picks.Click(id, Ctrl, Shift); PaintPicks(); Player.m_localPlayer?.PlayButtonSound(); return true; }
            ClearPicks(); return false;
        }
        internal IReadOnlyList<string> DragSnapshot(string id) {
            picks.SetOrder(tab.PickOrder());
            return picks.Contains(id) ? picks.Snapshot() : Array.AsReadOnly(new[]{id});
        }
        internal bool DragValid(int generation) => generation == PickGeneration && Showing && !TextInput.IsVisible()
            && ReferenceEquals(pickActor, Player.m_localPlayer) && pickSession == ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation;
        internal void SetBox(IReadOnlyList<string> kept, IReadOnlyList<string> touched) {
            if (!Showing || TextInput.IsVisible()) return;
            picks.SetOrder(tab.PickOrder()); picks.Box(kept,touched); PaintPicks();
        }
        internal void ClearPicks() { picks.Clear(); PickGeneration++; PaintPicks(); }
        void TickPicks() {
            var actor = Player.m_localPlayer; long session = ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation;
            if (!Showing || TextInput.IsVisible() || !ReferenceEquals(pickActor,actor) || pickSession != session) ClearPicks();
            pickActor=actor; pickSession=session;
            picks.SetOrder(tab.PickOrder()); PaintPicks();
            if (selectionBox != null) selectionBox.gameObject.SetActive(Showing && !TextInput.IsVisible());
        }
        internal bool IsPicked(string id) => picks.Contains(id);
        internal void TrackMark(KitBuildPickMark mark) { if (!marks.Contains(mark)) marks.Add(mark); }
        void PaintPicks() {
            for (int i=marks.Count-1;i>=0;i--) { var mark=marks[i];
                if (mark == null || mark.Panel != this) { marks.RemoveAt(i); continue; }
                mark.Show(Showing && !TextInput.IsVisible() && picks.Contains(tab.IdOf(mark.Button != null ? mark.Button.Piece : null)));
            }
        }
        void InstallBox(ScrollRect list) {
            if (list == null || list.viewport == null || list.content == null) return;
            var area=new GameObject("Crucible_"+tab.Owner+"_pickarea",typeof(RectTransform),typeof(Image));
            var rect=(RectTransform)area.transform; rect.SetParent(list.viewport,false); rect.SetAsFirstSibling();
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var catcher=area.GetComponent<Image>();catcher.color=Color.clear;catcher.canvasRenderer.cullTransparentMesh=false;
            selectionBox=area.AddComponent<KitBuildSelectionBox>();selectionBox.Initialize(this,list.viewport,list.content);
            area.SetActive(false);
        }
        void ClearBox() {
            if (selectionBox != null) { selectionBox.Retire(); KitUnity.Retire(selectionBox.gameObject); } selectionBox=null;
            foreach (var mark in marks) if (mark != null && mark.Panel == this) mark.Show(false); marks.Clear();
        }
        internal IReadOnlyList<string> Touched(RectTransform viewport,Rect rect) {
            var result=new List<string>(); if (menu==null) return result;
            foreach (var button in menu.GetComponentsInChildren<BuildUiPieceButton>(false)) {
                string id=tab.IdOf(button.Piece); if (!tab.Draggable(id)) continue;
                var transform=button.transform as RectTransform;
                var min=(Vector2)viewport.InverseTransformPoint(transform.TransformPoint(transform.rect.min));
                var max=(Vector2)viewport.InverseTransformPoint(transform.TransformPoint(transform.rect.max));
                if (Rect.MinMaxRect(Math.Min(min.x,max.x),Math.Min(min.y,max.y),Math.Max(min.x,max.x),Math.Max(min.y,max.y)).Overlaps(rect)) result.Add(id);
            }
            return result;
        }
    }
    public sealed class KitBuildPickMark : MonoBehaviour {
        internal BuildUiPieceButton Button;
        internal KitBuildFolders Panel;
        Image mark;
        internal void Show(bool show) {
            if (show && mark==null) {
                var go=new GameObject("Crucible_group_pick",typeof(RectTransform),typeof(Image),typeof(Outline));
                var rect=(RectTransform)go.transform;rect.SetParent(transform,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                mark=go.GetComponent<Image>();mark.raycastTarget=false;mark.color=new Color(1,.65f,.2f,.22f);
                var edge=go.GetComponent<Outline>();edge.effectColor=new Color(1,.65f,.2f,.9f);edge.effectDistance=new Vector2(1,-1);
            }
            if(mark!=null)mark.gameObject.SetActive(show);
        }
        void OnDisable()=>Show(false);
    }
    public sealed class KitBuildSelectionBox : MonoBehaviour,IInitializePotentialDragHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,IPointerClickHandler {
        KitBuildFolders owner;
        RectTransform viewport,content,box;
        Vector2 start;
        IReadOnlyList<string> kept;
        int generation;
        internal void Initialize(KitBuildFolders owner,RectTransform viewport,RectTransform content) {
            this.owner=owner;this.viewport=viewport;this.content=content;
            var go=new GameObject("Crucible_pick_box",typeof(RectTransform),typeof(Image),typeof(Outline));box=(RectTransform)go.transform;box.SetParent(viewport,false);
            box.anchorMin=box.anchorMax=box.pivot=Vector2.zero;
            var image=go.GetComponent<Image>();image.color=new Color(1,.65f,.2f,.12f);image.raycastTarget=false;
            var outline=go.GetComponent<Outline>();outline.effectColor=new Color(1,.65f,.2f,.9f);outline.effectDistance=new Vector2(1,-1);go.SetActive(false);
        }
        public void OnInitializePotentialDrag(PointerEventData data)=>data.useDragThreshold=true;
        public void OnBeginDrag(PointerEventData data) {
            if(data.button!=PointerEventData.InputButton.Left || owner==null || !owner.Showing || TextInput.IsVisible())return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(content,data.pressPosition,data.pressEventCamera,out start))return;
            data.eligibleForClick=false;kept=KitBuildFolders.Ctrl?owner.PickedIds:Array.Empty<string>();generation=owner.PickGeneration;
            box.SetAsLastSibling();box.gameObject.SetActive(true);Draw(data);
        }
        public void OnDrag(PointerEventData data) { if(kept!=null)Draw(data); }
        public void OnEndDrag(PointerEventData data) { if(kept!=null)Draw(data);Cancel(); }
        public void OnPointerClick(PointerEventData data) { if(data.button==PointerEventData.InputButton.Left && !KitBuildFolders.Ctrl && owner!=null && owner.Showing && !TextInput.IsVisible())owner.ClearPicks(); }
        void Draw(PointerEventData data) {
            if(!owner.DragValid(generation)){Cancel();return;}
            Vector2 from=viewport.InverseTransformPoint(content.TransformPoint(start));
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,data.position,data.pressEventCamera,out var to))return;
            Rect rect=Rect.MinMaxRect(Math.Min(from.x,to.x),Math.Min(from.y,to.y),Math.Max(from.x,to.x),Math.Max(from.y,to.y));
            box.localPosition=rect.min;box.sizeDelta=rect.size;owner.SetBox(kept,owner.Touched(viewport,rect));
        }
        void Update(){if(kept!=null && (!owner.DragValid(generation)||!ZInput.GetMouseButton(0)))Cancel();}
        void OnDisable()=>Cancel();
        void Cancel(){kept=null;if(box!=null)box.gameObject.SetActive(false);}
        internal void Retire(){Cancel();if(box!=null)KitUnity.Retire(box.gameObject);box=null;}
        void OnDestroy()=>Retire();
    }
}
