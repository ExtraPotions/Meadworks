// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Folder drop targeting and native piece-button drag lifecycle adapted from OpenKeep 3.0.0
// FolderTarget/TabDrag at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, GPL-3.0-or-later.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public sealed class KitFolderTarget : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        internal static KitFolderTarget Hovered;
        internal KitBuildFolders Panel;
        internal KitFolderLine Line;
        Image mark;
        Image icon;
        internal Image Icon()
        {
            if (icon != null) return icon;
            var go = new GameObject("Crucible_folder_icon", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform; rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, .5f); rect.sizeDelta = new Vector2(20, 20);
            icon = go.GetComponent<Image>(); icon.preserveAspect = true; icon.raycastTarget = false;
            return icon;
        }
        internal void Set(KitBuildFolders panel, KitFolderLine line) { Panel = panel; Line = line; Light(false); }
        internal void Activate() { if (Panel != null && Line != null) Panel.Open(Line); }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Right && Panel != null && Line != null) Panel.Rename(Line);
        }
        internal void Light(bool show)
        {
            if (show && mark == null)
            {
                var go = new GameObject("Crucible_drop_target", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform; rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                mark = go.GetComponent<Image>(); mark.raycastTarget = false; mark.color = new Color(1, .65f, .2f, .2f);
            }
            if (mark != null) mark.gameObject.SetActive(show);
        }
        public void OnPointerEnter(PointerEventData data) => Hovered = this;
        public void OnPointerExit(PointerEventData data) { if (Hovered == this) Hovered = null; }
        void OnDisable() { if (Hovered == this) Hovered = null; Light(false); }
    }

    public sealed class KitBuildFolderDrag : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        internal KitBuildFolders Panel;
        IReadOnlyList<string> entries;
        int generation;
        int releasedFrame = -1;
        KitFolderTarget target;
        RectTransform ghost;
        static Piece suppressed;
        static int suppressedFrame = -1;
        internal static bool Suppresses(Piece piece) => piece != null && piece == suppressed && Time.frameCount <= suppressedFrame + 1;
        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = true;
        public void OnBeginDrag(PointerEventData data)
        {
            Cancel();
            var button = GetComponent<BuildUiPieceButton>();
            if (data.button != PointerEventData.InputButton.Left || Panel == null || !Panel.DragEntry(button, out var id)) return;
            entries = Panel.DragSnapshot(id); generation = Panel.PickGeneration; releasedFrame = -1; data.eligibleForClick = false;
            suppressed = button.Piece; suppressedFrame = Time.frameCount;
            var go = new GameObject("Crucible_buildtab_drag", typeof(RectTransform), typeof(Image));
            ghost = (RectTransform)go.transform; ghost.SetParent(PanelParent(), false); ghost.sizeDelta = new Vector2(40, 40);
            var image = go.GetComponent<Image>(); image.sprite = button.Piece.m_icon; image.preserveAspect = true; image.raycastTarget = false;
            if (entries.Count > 1) { var count = KitCanvas.Text(ghost, "count", entries.Count.ToString(), 18, Color.white, TMPro.TextAlignmentOptions.BottomRight, false); count.raycastTarget = false; }
            OnDrag(data);
        }
        Transform PanelParent() => GetComponentInParent<BuildUi>().transform;
        public void OnDrag(PointerEventData data)
        {
            if (entries == null) return;
            if (Panel == null || !Panel.DragValid(generation)) { Cancel(); return; }
            suppressedFrame = Time.frameCount;
            if (ghost != null && ghost.parent is RectTransform parent && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position, data.pressEventCamera, out var local)) ghost.anchoredPosition = local;
            var over = data.pointerCurrentRaycast.gameObject;
            var next = over != null ? over.GetComponentInParent<KitFolderTarget>() : null;
            if (next != null && (next.Panel != Panel || next.Line == null || next.Line.Current || !next.gameObject.activeInHierarchy)) next = null;
            if (next == target) return;
            if (target != null) target.Light(false);
            target = next;
            if (target != null) target.Light(true);
        }
        public void OnEndDrag(PointerEventData data)
        {
            if (entries == null) return;
            OnDrag(data);
            var moving = entries; var into = target; var panel = Panel;
            suppressedFrame = Time.frameCount; Cancel();
            if (moving != null && into != null && panel != null) panel.Move(moving, into.Line);
        }
        void Update() {
            if (entries == null) return;
            if (Panel == null || !Panel.DragValid(generation)) { Cancel(); return; }
            if (ZInput.GetMouseButton(0)) releasedFrame = -1;
            else if (releasedFrame < 0) releasedFrame = Time.frameCount;
            else if (Time.frameCount > releasedFrame + 2) Cancel();
        }
        void OnDisable() => Cancel();
        void Cancel()
        {
            entries = null;
            if (target != null) target.Light(false);
            target = null;
            if (ghost != null) KitUnity.Retire(ghost.gameObject);
            ghost = null;
        }
    }
}
