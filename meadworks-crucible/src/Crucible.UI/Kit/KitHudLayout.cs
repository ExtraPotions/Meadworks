// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitHudLayout
    {
        sealed class Record
        {
            internal string Key;
            internal KitHudPanel Panel;
            internal HudLayoutState State = new HudLayoutState();
            internal ConfigEntry<string> Entry;
            internal EventHandler Changed;
            internal bool Readable = true;
            internal Transform Parent;
            internal RectTransform Canvas;
            internal HudLayoutNode Node;
        }
        sealed class CanvasPlan
        {
            internal readonly Dictionary<string, HudLayoutNode> Nodes = new Dictionary<string, HudLayoutNode>(StringComparer.Ordinal);
            internal readonly Dictionary<string, HudLayoutPosition> Positions = new Dictionary<string, HudLayoutPosition>(StringComparer.Ordinal);
            internal float Width, Height;
            internal bool Dirty = true;
        }
        static readonly Dictionary<string, Record> records = new Dictionary<string, Record>(StringComparer.Ordinal);
        static readonly Dictionary<KitHudPanel, Record> byPanel = new Dictionary<KitHudPanel, Record>();
        static readonly List<Record> live = new List<Record>();
        static readonly Dictionary<RectTransform, CanvasPlan> plans = new Dictionary<RectTransform, CanvasPlan>();
        internal static long ResolutionPasses { get; private set; }
        static ConfigFile config;
        static IDisposable menu;
        static string message = "";
        static readonly Vector3[] corners = new Vector3[4];
        static string Key(string owner, string id)
        {
            bool Valid(string value) => !string.IsNullOrEmpty(value) && value.Length <= 128 && value.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-');
            if (!Valid(owner) || !Valid(id)) throw new ArgumentException("HUD owner/id requires letters, numbers, dots, underscores or hyphens.");
            return owner + "/" + id;
        }
        internal static void ValidateIdentity(string owner, string id) => Key(owner, id);

        public static void Initialize(ConfigFile configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (config != null && !ReferenceEquals(config, configuration)) throw new InvalidOperationException("HUD layout configuration already belongs to another host.");
            config = configuration;
            foreach (var record in records.Values) TryBind(record);
            if (menu == null) menu = MeadworksMenu.Register("crucible.hud-layout", "HUD layout", OpenEditor, () => records.Values.Any(r => r.Panel.Root != null), 25);
        }

        internal static void Register(KitHudPanel panel, string owner, string id)
        {
            var key = Key(owner, id);
            if (records.TryGetValue(key, out var old))
            {
                CancelDrag();
                Unbind(old);
                byPanel.Remove(old.Panel); live.Remove(old);
                old.Panel.Present(new HudLayoutState(), false);
            }
            var record = new Record { Key = key, Panel = panel };
            records[key] = record;
            byPanel[panel] = record; live.Add(record); plans.Clear();
            if (config != null) TryBind(record);
            var drag = panel.Root.gameObject.AddComponent<KitHudLayoutDrag>();
            drag.Panel = panel;
            Apply(panel);
        }

        static void Bind(Record record)
        {
            Unbind(record);
            record.Entry = MeadworksConfig.BindHidden(config, "HUD Layout", record.Key, "", "Local HUD placement and appearance; use the HUD layout editor to change or return to automatic.");
            record.Changed = (sender, args) => Read(record);
            record.Entry.SettingChanged += record.Changed;
            Read(record);
        }

        static void TryBind(Record record)
        {
            try { Bind(record); }
            catch (Exception error) { Unbind(record); record.Entry = null; message = F(HudLayoutText.StorageFailed, error.Message); }
        }

        static void Read(Record record)
        {
            if (ReferenceEquals(dragRecord, record)) CancelDrag();
            record.Readable = HudLayoutRules.TryDecode(record.Entry.Value, out var state);
            record.State = state;
            if (!record.Readable) message = T(HudLayoutText.Unsupported);
            Apply(record.Panel);
        }

        static void Unbind(Record record)
        {
            if (record.Entry != null && record.Changed != null) record.Entry.SettingChanged -= record.Changed;
            record.Changed = null;
        }

        internal static void Unregister(KitHudPanel panel)
        {
            if (!byPanel.TryGetValue(panel, out var record)) return;
            if (ReferenceEquals(dragRecord, record)) CancelDrag();
            Unbind(record);
            records.Remove(record.Key);
            byPanel.Remove(panel); live.Remove(record); plans.Clear();
        }

        internal static void Apply(KitHudPanel panel)
        {
            byPanel.TryGetValue(panel, out var record);
            panel.Present(record?.State ?? automatic, record != null && IsEditing(record));
        }
        static readonly HudLayoutState automatic = new HudLayoutState();

        static RectTransform CanvasOf(KitHudPanel panel)
        {
            if (panel.Root == null) return null;
            if (byPanel.TryGetValue(panel, out var record))
            {
                if (record.Parent != panel.Root.parent || record.Canvas == null)
                {
                    var canvas = panel.Root.GetComponentInParent<Canvas>();
                    record.Parent = panel.Root.parent;
                    record.Canvas = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
                    plans.Clear();
                }
                return record.Canvas;
            }
            return null;
        }

        static Vector2 Point(RectTransform canvas, Vector3 world)
        {
            var local = canvas.InverseTransformPoint(world);
            return new Vector2(local.x - canvas.rect.xMin, canvas.rect.yMax - local.y);
        }

        static Dictionary<string, HudLayoutNode> Nodes(RectTransform canvas)
        {
            if (!plans.TryGetValue(canvas, out var plan)) { plan = new CanvasPlan(); plans.Add(canvas, plan); }
            if (plan.Width != canvas.rect.width || plan.Height != canvas.rect.height)
            { plan.Width = canvas.rect.width; plan.Height = canvas.rect.height; plan.Dirty = true; }
            for (int i = 0; i < live.Count; i++)
            {
                var record = live[i];
                var panel = record.Panel;
                if (panel.Root == null || record.Canvas != canvas) continue;
                var parent = panel.Root.parent as RectTransform;
                if (parent == null) continue;
                var auto = panel.AutomaticPosition;
                var automatic = Point(canvas, parent.TransformPoint(new Vector3(parent.rect.xMin + auto.x, parent.rect.yMax - auto.y, 0)));
                panel.Root.GetWorldCorners(corners);
                var top = Point(canvas, corners[1]); var bottom = Point(canvas, corners[3]);
                float width = Mathf.Abs(bottom.x - top.x), height = Mathf.Abs(bottom.y - top.y);
                bool available = panel.ProductVisible && panel.Root.parent.gameObject.activeInHierarchy;
                var old = record.Node;
                if (!plan.Nodes.ContainsKey(record.Key) || old == null || !ReferenceEquals(old.State, record.State)
                    || !Mathf.Approximately(old.AutomaticX, automatic.x) || !Mathf.Approximately(old.AutomaticY, automatic.y)
                    || !Mathf.Approximately(old.Width, width) || !Mathf.Approximately(old.Height, height) || old.Available != available)
                {
                    record.Node = new HudLayoutNode(record.Key, record.State, automatic.x, automatic.y, width, height, available);
                    plan.Nodes[record.Key] = record.Node; plan.Dirty = true;
                }
            }
            return plan.Nodes;
        }

        /// <summary>Call after product HUD updates, normally from the UI host's LateUpdate.</summary>
        public static void Tick()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var record = live[i];
                if (record.Panel.Root == null) { Unregister(record.Panel); continue; }
                Apply(record.Panel); CanvasOf(record.Panel);
            }
            for (int i = 0; i < live.Count; i++)
            {
                var canvas = live[i].Canvas;
                if (canvas != null && !plans.ContainsKey(canvas)) plans.Add(canvas, new CanvasPlan());
            }
            foreach (var pair in plans)
            {
                var canvas = pair.Key; var plan = pair.Value;
                var nodes = Nodes(canvas);
                if (plan.Dirty)
                {
                    plan.Positions.Clear();
                    foreach (var node in nodes.Values)
                        if (node.State.Placed || node.State.Scale > 0) plan.Positions[node.Key] = HudLayoutRules.Resolve(node.Key, nodes, plan.Width, plan.Height);
                    plan.Dirty = false; ResolutionPasses++;
                }
            }
            for (int i = 0; i < live.Count; i++)
            {
                var record = live[i]; var canvas = record.Canvas;
                if (canvas != null && plans.TryGetValue(canvas, out var plan) && plan.Positions.TryGetValue(record.Key, out var at))
                {
                    var wanted = canvas.TransformPoint(new Vector3(canvas.rect.xMin + at.X, canvas.rect.yMax - at.Y, 0));
                    if ((record.Panel.Root.position - wanted).sqrMagnitude > .0001f) record.Panel.Root.position = wanted;
                }
            }
            TickEditor();
        }

        static bool Commit(Record record, HudLayoutState state, bool remember = true)
        {
            if (record.Entry == null || !HudLayoutRules.Valid(state)) { message = T(HudLayoutText.StorageUnavailable); return false; }
            var before = record.State.Copy();
            try
            {
                var result = MeadworksConfig.Amend(record.Entry, CrucibleUiPlugin.Guid, ConfigScope.Local, HudLayoutRules.Encode(state));
                if (!result.Applied) { message = F(HudLayoutText.NotSaved, result.Decision.ToString()); record.State = before; return false; }
                record.State = state.Copy();
                if (remember) Remember(record, before);
                message = T(HudLayoutText.Saved);
                return true;
            }
            catch (Exception error) { record.State = before; message = F(HudLayoutText.SaveFailed, error.Message); return false; }
            finally { Apply(record.Panel); }
        }

        public static void Shutdown()
        {
            CancelDrag();
            DestroyEditor();
            menu?.Dispose(); menu = null;
            foreach (var record in records.Values)
            {
                Unbind(record);
                if (record.Panel.Root != null) record.Panel.Present(new HudLayoutState(), false);
            }
            records.Clear(); byPanel.Clear(); live.Clear(); plans.Clear(); undo.Clear(); config = null;
        }
    }
}
