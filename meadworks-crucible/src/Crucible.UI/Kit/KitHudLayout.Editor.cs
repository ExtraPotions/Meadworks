// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitHudLayout
    {
        sealed class Undo
        {
            internal string Key;
            internal HudLayoutState State;
        }
        static readonly List<Undo> undo = new List<Undo>();
        static KitWindow editor;
        static TMP_Text detail, feedback;
        static KitToggle visible;
        static string selected = "";
        static Record dragRecord;
        static HudLayoutState dragBefore;
        static Vector2 dragStart, panelStart;
        static float startScale;
        static bool resizing;
        static Record shownRecord;
        static HudLayoutState shownState;
        static bool shownProductVisible;
        static string shownMessage;
        static Record Selected => records.TryGetValue(selected, out var record) ? record : null;
        static string PanelName(Record record)
        {
            string name = record.Panel.Root.name.Replace('_', ' ');
            string owner = record.Key.Substring(0, record.Key.IndexOf('/'));
            return ExtraPotions.Meadworks.Crucible.Plugin.IsReady
                && ExtraPotions.Meadworks.Crucible.Plugin.Services.TryGetMetadata(owner, out var metadata)
                ? metadata.Name + " · " + name : name;
        }
        static bool IsEditing(Record record) => editor != null && editor.IsOpen && record.Key == selected && KitAlmanacDialog.Active == null;

        public static void OpenEditor()
        {
            if (config == null || records.Count == 0) return;
            if (editor != null && editor.Panel == null) DestroyEditor();
            if (editor == null) BuildEditor();
            if (editor == null) return;
            if (!records.ContainsKey(selected)) selected = records.Keys.OrderBy(k => k, StringComparer.Ordinal).First();
            editor.Open();
        }

        static void BuildEditor()
        {
            editor = KitWindow.Create("hud-layout", new Vector2(640, 570));
            if (editor == null) return;
            editor.HideHud = false;
            editor.Closed += () => { CancelDrag(); foreach (var record in records.Values) Apply(record.Panel); };
            var title = KitAlmanacDialog.Label(editor.Panel, "Title", "HUD layout", new Vector2(20, -14), new Vector2(600, 32), 23);
            if (KitCanvas.Heading != null) title.font = KitCanvas.Heading;
            detail = KitAlmanacDialog.Label(editor.Panel, "Details", "", new Vector2(20, -94), new Vector2(600, 142), 15);
            feedback = KitAlmanacDialog.Label(editor.Panel, "Feedback", "", new Vector2(20, -458), new Vector2(600, 80), 14);
            Button("Previous", "Previous panel", () => Select(-1), 20, 54, 190, 0, 0);
            Button("Next", "Next panel", () => Select(1), 220, 54, 190, 1, 0);
            Button("Close", "Close", () => editor.Close(), 420, 54, 200, 2, 0);
            visible = KitToggle.Create(editor.Panel, "Visible", true, on => Change(s => s.Hidden = !on), new Vector2(20, -242), new Vector2(190, 32), "Show panel", "Hide panel");
            editor.Focusable(visible.Button, 0, 1);
            Button("ScaleLess", "Smaller", () => Scale(-.1f), 220, 242, 190, 1, 1);
            Button("ScaleMore", "Larger", () => Scale(.1f), 420, 242, 200, 2, 1);
            Button("OpacityLess", "Less background", () => Opacity(-.1f), 20, 284, 190, 0, 2);
            Button("OpacityMore", "More background", () => Opacity(.1f), 220, 284, 190, 1, 2);
            Button("OpacityAuto", "Product background", () => Change(s => s.Opacity = -1), 420, 284, 200, 2, 2);
            Button("Left", "Move left", () => Nudge(-8, 0), 20, 326, 140, 0, 3);
            Button("Right", "Move right", () => Nudge(8, 0), 170, 326, 140, 1, 3);
            Button("Up", "Move up", () => Nudge(0, -8), 320, 326, 140, 2, 3);
            Button("Down", "Move down", () => Nudge(0, 8), 470, 326, 150, 3, 3);
            Button("Target", "Attach to next", CycleTarget, 20, 368, 190, 0, 4);
            Button("Side", "Change edge", () => Change(s => s.Side = (HudAttachSide)(((int)s.Side + 1) % 4)), 220, 368, 190, 1, 4);
            Button("Detach", "Independent position", () => Change(s => { Place(s); s.Target = ""; }), 420, 368, 200, 2, 4);
            Button("GapLess", "Gap −", () => Change(s => s.Gap = Mathf.Max(0, s.Gap - 4)), 20, 410, 140, 0, 5);
            Button("GapMore", "Gap +", () => Change(s => s.Gap = Mathf.Min(200, s.Gap + 4)), 170, 410, 140, 1, 5);
            Button("Undo", "Undo", UndoLast, 320, 410, 140, 2, 5);
            Button("Automatic", "Automatic layout", () => { var record = Selected; if (record != null) Commit(record, new HudLayoutState()); }, 470, 410, 150, 3, 5);
        }

        static void Button(string name, string text, Action click, float x, float y, float width, int column, int row)
        {
            var button = KitButton.Create(editor.Panel, name, text, click, new Vector2(x, -y), new Vector2(width, 32));
            editor.Focusable(button, column, row);
        }

        static void Select(int direction)
        {
            CancelDrag();
            var keys = records.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            if (keys.Length == 0) return;
            int index = Array.IndexOf(keys, selected);
            selected = keys[(Math.Max(0, index) + direction + keys.Length) % keys.Length];
        }

        static void Change(Action<HudLayoutState> change)
        {
            CancelDrag();
            var record = Selected;
            if (record == null || !record.Readable) { message = "Reset unsupported saved layout explicitly before editing."; return; }
            var next = record.State.Copy();
            change(next);
            Commit(record, next);
        }

        static void Place(HudLayoutState state)
        {
            var record = Selected;
            if (record == null) return;
            var canvas = CanvasOf(record.Panel);
            if (canvas == null || canvas.rect.width <= 0 || canvas.rect.height <= 0) return;
            var at = Point(canvas, record.Panel.Root.position);
            state.Placed = true;
            state.X = Mathf.Clamp01(at.x / canvas.rect.width);
            state.Y = Mathf.Clamp01(at.y / canvas.rect.height);
        }

        static void Nudge(float x, float y) => Change(state =>
        {
            Place(state); state.Target = "";
            var canvas = CanvasOf(Selected.Panel);
            if (canvas == null) return;
            state.X = Mathf.Clamp01(state.X + x / Mathf.Max(1, canvas.rect.width));
            state.Y = Mathf.Clamp01(state.Y + y / Mathf.Max(1, canvas.rect.height));
        });

        static void Scale(float amount) => Change(state => state.Scale = Mathf.Clamp((state.Scale > 0 ? state.Scale : Selected.Panel.ProductScale) + amount, .5f, 3));
        static void Opacity(float amount) => Change(state => state.Opacity = Mathf.Clamp01((state.Opacity >= 0 ? state.Opacity : Selected.Panel.ProductOpacity) + amount));

        static void CycleTarget()
        {
            var record = Selected;
            if (record == null) return;
            var canvas = CanvasOf(record.Panel);
            if (canvas == null) return;
            var nodes = Nodes(canvas);
            var choices = new[] { "" }.Concat(nodes.Keys.Where(k => k != record.Key && HudLayoutRules.CanAttach(record.Key, k, nodes)).OrderBy(k => k, StringComparer.Ordinal)).ToArray();
            var target = choices[(Math.Max(0, Array.IndexOf(choices, record.State.Target)) + 1) % choices.Length];
            Change(state => { Place(state); state.Target = target; });
        }

        static void Remember(Record record, HudLayoutState state)
        {
            undo.Add(new Undo { Key = record.Key, State = state.Copy() });
            if (undo.Count > 40) undo.RemoveAt(0);
        }

        static void UndoLast()
        {
            CancelDrag();
            if (undo.Count == 0) { message = "Nothing to undo."; return; }
            var action = undo[undo.Count - 1];
            if (!records.TryGetValue(action.Key, out var record)) { message = "That panel is not currently available."; return; }
            if (Commit(record, action.State, false)) { undo.RemoveAt(undo.Count - 1); selected = action.Key; }
        }

        static void TickEditor()
        {
            if (editor == null || !editor.IsOpen) return;
            if (!records.ContainsKey(selected)) selected = records.Keys.OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault() ?? "";
            var record = Selected;
            if (record == null) { editor.Close(); return; }
            if (editor.Panel.parent is RectTransform host)
            {
                float scale = Mathf.Min(1, Mathf.Min(Mathf.Max(1, host.rect.width - 24) / 640, Mathf.Max(1, host.rect.height - 24) / 570));
                editor.Panel.localScale = Vector3.one * scale;
                editor.Panel.anchoredPosition = new Vector2(-Mathf.Max(0, host.rect.width * .5f - 330 * scale), 0);
            }
            var state = record.State;
            if (!ReferenceEquals(shownRecord, record) || !ReferenceEquals(shownState, state) || shownProductVisible != record.Panel.ProductVisible || shownMessage != message)
            {
                shownRecord = record; shownState = state; shownProductVisible = record.Panel.ProductVisible; shownMessage = message;
                visible.SetValue(!state.Hidden);
                detail.text = PanelName(record) + "\n" + (record.Panel.ProductVisible ? "Panel is visible" : "Hidden by its mod; layout cannot enable it")
                + "\n" + (state.Placed ? "Placed" : "Automatic position") + " · size " + (state.Scale > 0 ? state.Scale.ToString("0.0") : "product")
                + " · background " + (state.Opacity >= 0 ? state.Opacity.ToString("0.0") : "product")
                + "\n" + (state.Target.Length > 0 ? "Attached " + state.Side + " of " + (records.TryGetValue(state.Target,out var target) ? PanelName(target) : "an unavailable panel") + " · gap " + state.Gap.ToString("0") : "No attachment");
                feedback.text = "Drag the selected panel background to move; drag its lower-right corner to resize. Alt + arrows nudges. Escape closes this editor and cancels a drag.\n" + message;
            }
            if (dragRecord == null && KitAlmanacDialog.Active == null && !MeadworksInput.Typing && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow)) Nudge(-1, 0);
                if (Input.GetKeyDown(KeyCode.RightArrow)) Nudge(1, 0);
                if (Input.GetKeyDown(KeyCode.UpArrow)) Nudge(0, -1);
                if (Input.GetKeyDown(KeyCode.DownArrow)) Nudge(0, 1);
            }
        }

        static void DestroyEditor()
        {
            if (editor == null) return;
            editor.Close(); visible?.Dispose(); visible = null;
            editor.Destroy(); editor = null; detail = feedback = null; selected = "";
            shownRecord = null; shownState = null; shownMessage = null;
        }

        internal static void BeginDrag(KitHudPanel panel, PointerEventData data)
        {
            var record = Selected;
            if (record == null || !ReferenceEquals(panel, record.Panel) || !IsEditing(record) || !record.Readable || !panel.ProductVisible || data.button != PointerEventData.InputButton.Left) return;
            CancelDrag();
            var canvas = CanvasOf(panel);
            if (canvas == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, data.position, data.pressEventCamera, out var point)) return;
            dragRecord = record; dragBefore = record.State.Copy();
            dragStart = new Vector2(point.x - canvas.rect.xMin, canvas.rect.yMax - point.y);
            panelStart = Point(canvas, panel.Root.position);
            startScale = record.State.Scale > 0 ? record.State.Scale : panel.ProductScale;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(panel.Root, data.position, data.pressEventCamera, out var local);
            resizing = local.x > panel.Root.rect.xMax - 20 && local.y < panel.Root.rect.yMin + 20;
        }

        internal static void MoveDrag(KitHudPanel panel, PointerEventData data)
        {
            if (dragRecord == null || !ReferenceEquals(dragRecord.Panel, panel)) return;
            var canvas = CanvasOf(panel);
            if (canvas == null || !IsEditing(dragRecord)) { CancelDrag(); return; }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, data.position, data.pressEventCamera, out var point)) return;
            var delta = new Vector2(point.x - canvas.rect.xMin, canvas.rect.yMax - point.y) - dragStart;
            var state = dragBefore.Copy();
            state.Placed = true; state.Target = "";
            state.X = Mathf.Clamp01((panelStart.x + (resizing ? 0 : delta.x)) / Mathf.Max(1, canvas.rect.width));
            state.Y = Mathf.Clamp01((panelStart.y + (resizing ? 0 : delta.y)) / Mathf.Max(1, canvas.rect.height));
            if (resizing)
            {
                panel.Root.GetWorldCorners(corners);
                var size = Point(canvas, corners[3]) - Point(canvas, corners[1]);
                float extent = Mathf.Max(1, Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y)) / Mathf.Max(.5f, panel.Root.localScale.x));
                state.Scale = Mathf.Clamp(startScale + (delta.x + delta.y) * .5f / extent, .5f, 3);
            }
            dragRecord.State = state;
            Apply(panel);
        }

        internal static void EndDrag(KitHudPanel panel, bool cancel)
        {
            if (dragRecord == null || !ReferenceEquals(dragRecord.Panel, panel)) return;
            var record = dragRecord; var draft = record.State.Copy(); var before = dragBefore;
            dragRecord = null; dragBefore = null;
            record.State = before;
            if (!cancel && record.Panel.ProductVisible && record.Panel.Root != null) Commit(record, draft);
            else Apply(record.Panel);
        }

        static void CancelDrag()
        {
            if (dragRecord != null) EndDrag(dragRecord.Panel, true);
        }
    }

    public sealed class KitHudLayoutDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        internal KitHudPanel Panel;
        public void OnBeginDrag(PointerEventData data) => KitHudLayout.BeginDrag(Panel, data);
        public void OnDrag(PointerEventData data) => KitHudLayout.MoveDrag(Panel, data);
        public void OnEndDrag(PointerEventData data) => KitHudLayout.EndDrag(Panel, false);
        void OnDisable() { if (Panel != null) KitHudLayout.EndDrag(Panel, true); }
    }
}
