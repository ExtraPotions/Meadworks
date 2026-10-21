// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-owned reuse: Almanac editor selection, native TOML text handling and existing kit editors over Thing's owned relay job.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.ConfigEditing.Editors;
using ExtraPotions.Thing.ConfigEditing.Values;
using ExtraPotions.Thing.RemoteConfig;
using ExtraPotions.Thing.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingPanelConfig
    {
        static KitAlmanacDialog? dialog;
        static KitVirtualList? list;
        static ThingUiPage? page;
        static TMP_Text? status;
        static string file = "", selected = "";
        static int offset;
        static long revision;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static void Open()
        {
            if (ThingPanel.Window == null || !ThingPanel.CanConfigure || ThingPanelJobs.Busy) return;
            Reset(); file = selected = ""; offset = 0;
            var current = KitAlmanacDialog.Create(ThingPanel.Window, ThingPanel.Ui("tab_server_config"), new Vector2(840, 650)); if (current == null) return;
            current.Dim();
            dialog = current; current.Own(() => { if (ReferenceEquals(dialog, current)) Release(); });
            Button Control(string key, Action click, float x, float y, float width, int column, int row)
            {
                var control = KitButton.Create(current.Panel, key, ThingPanel.Ui(key), () => { if (ThingPanel.CanConfigure && !ThingPanelJobs.Busy) click(); }, new Vector2(x, -y), new Vector2(width, 36));
                current.Window.Focusable(control, column, row); return control;
            }
            Control("refresh", Refresh, 20, 60, 185, 0, 0);
            Control("previous", () => { offset = Math.Max(0, offset - ThingUiPage.PageSize); Refresh(); }, 225, 60, 185, 1, 0);
            Control("next", () => { if (page != null && offset + ThingUiPage.PageSize < page.Total) { offset += ThingUiPage.PageSize; Refresh(); } }, 430, 60, 185, 2, 0);
            Control("config_back", () => { file = selected = ""; offset = 0; Refresh(); }, 635, 60, 185, 3, 0);
            list = KitVirtualList.Create(current.Panel, "ServerConfig", new Vector2(800, 355), parent =>
            { var row = new GameObject("Row", typeof(RectTransform)); row.transform.SetParent(parent, false); return (RectTransform)row.transform; });
            if (list != null) KitAlmanacDialog.Place(list.Root, new Vector2(20, -115), new Vector2(800, 355));
            status = KitAlmanacDialog.Label(current.Panel, "Status", ThingPanel.Ui("config_hint"), new Vector2(20, -480), new Vector2(800, 75), 14); status.richText = false;
            Control("config_open", Inspect, 20, 565, 390, 0, 20);
            var close = KitButton.Create(current.Panel, "Close", ThingPanel.Ui("close"), current.Dispose, new Vector2(430, -565), new Vector2(390, 36)); current.Window.Focusable(close, 1, 20);
            current.Tick += () => { if (!ThingPanel.CanConfigure) { current.Dispose(); return; } list?.Tick(); };
            Refresh();
        }
        static void Refresh()
        {
            if (dialog == null) return;
            var owner = dialog; var expectedFile = file; var expectedOffset = offset;
            var args = file.Length == 0 ? new[] { "config", "list", offset.ToString(Inv) } : new[] { "config", "entries", file, offset.ToString(Inv) };
            ThingPanelJobs.Start(args, lines =>
            {
                if (!ReferenceEquals(dialog, owner) || file != expectedFile || offset != expectedOffset) return;
                if (!ThingUiPage.TryDecode(lines, out var loaded) || loaded.Page != (file.Length == 0 ? "config_files" : "config_entries") || loaded.Offset != offset)
                { Message(string.Join("\n", lines)); return; }
                page = loaded; selected = ""; Render(); Message(ThingText.Format("ui_page", offset + (loaded.Rows.Count == 0 ? 0 : 1), offset + loaded.Rows.Count, loaded.Total));
            });
        }
        static void Render()
        {
            if (list == null || dialog == null || page == null) return;
            var owner = dialog;
            list.SetRows(page.Rows.Select((row, at) => new KitVirtualRow(row.Id, 72, rect =>
            {
                foreach (Transform child in rect) KitUnity.Retire(child.gameObject);
                var button = KitButton.Create(rect, "Select", (selected == row.Id ? "> " : "") + row.Caption + "\n" + row.Detail,
                    () => { selected = row.Id; Render(); }, new Vector2(0, -2), new Vector2(780, 68));
                var words = button.GetComponentInChildren<TMP_Text>(true); if (words != null) { words.richText = false; words.textWrappingMode = TextWrappingModes.Normal; }
                owner.Window.Focusable(button, 0, at + 1);
            }, rect => { foreach (var control in rect.GetComponentsInChildren<Selectable>(true)) owner.Window.Unfocusable(control); })).ToArray(), ++revision);
        }
        static void Inspect()
        {
            var row = page?.Rows.FirstOrDefault(r => r.Id == selected);
            if (dialog == null || row == null) { Message(ThingPanel.Ui("config_select")); return; }
            if (file.Length == 0 && row.Kind == "yaml") { ThingPanelYaml.Open(dialog, row.Id, row.Caption, Message); return; }
            if (file.Length == 0) { file = row.Id; offset = 0; selected = ""; Refresh(); return; }
            var owner = dialog; var expectedFile = file; var expectedEntry = row.Id;
            ThingPanelJobs.Start(new[] { "config", "get", file, row.Id }, lines =>
            {
                if (!ReferenceEquals(dialog, owner) || file != expectedFile) return;
                if (!ConfigEntryDocument.TryDecode(lines, out var document) || document.File != expectedFile || document.Entry != expectedEntry)
                { Message(string.Join("\n", lines)); return; }
                Edit(document, owner);
            });
        }
        static string? Validate(ConfigEntryDocument document, string value)
        {
            if (System.Text.Encoding.UTF8.GetByteCount(value) > 4096) return ThingPanel.Ui("invalid");
            var parsed = ValueCodec.Parse(document.Shape, value);
            return parsed.Ok || parsed.NeedsModConverter || document.Shape.Kind == SettingKind.Enum && document.Shape.IsFlags && value == "0" ? null : parsed.Error;
        }
        static void Save(ConfigEntryDocument document, string value)
        {
            if (dialog == null || !ThingPanel.CanConfigure || ThingPanelJobs.Busy || !document.Editable) return;
            var error = Validate(document, value); if (error != null) { Message(error); return; }
            Commit(document, new[] { "set", document.File, document.Entry, document.Version }.Concat(ConfigWire.PackValue(value)).ToArray());
        }
        internal static void SaveList(ConfigEntryDocument document, IReadOnlyList<string> values)
        {
            try { Commit(document, new[] { "config", "list-set", document.File, document.Entry, document.Version }.Concat(ConfigListDocument.PackValues(values)).ToArray()); }
            catch { Message(ThingPanel.Ui("invalid")); }
        }
        static void Commit(ConfigEntryDocument document, string[] args)
        {
            if (dialog == null || !ThingPanel.CanConfigure || ThingPanelJobs.Busy || !document.Editable) return;
            var owner = dialog;
            ThingPanelForms.Confirm(owner.Window, document.Section + "." + document.Key + "\n" + ThingPanel.Ui("config_write_warning"), () =>
            {
                if (!ReferenceEquals(dialog, owner)) return;
                ThingPanelJobs.Start(args, lines =>
                {
                    if (!ReferenceEquals(dialog, owner)) return;
                    var message = ConfigWire.TryWriteEcho(lines, out var version, out var value) ? ThingText.Text("config_saved") + "\n" + value : string.Join("\n", lines);
                    Message(message + (document.Attributes.RequiresRestart ? "\n" + ThingPanel.Ui("config_restart") : ""));
                });
            }, settingsOnly: true);
        }
        static void Edit(ConfigEntryDocument d, KitAlmanacDialog owner)
        {
            var title = d.Section + "." + d.Key;
            Message(d.Description + (d.Attributes.RequiresRestart ? "\n" + ThingPanel.Ui("config_restart") : ""));
            if (!d.Editable)
            { KitAlmanacTextEditor.Open(owner.Window, title, d.Current + "\n\n" + d.Effective + "\n\n" + d.Description + "\n\n" + d.Reason, _ => null!, _ => { }, true); return; }
            var editor = EditorSelector.Fallback(d.Shape, d.Attributes, d.Current).Kind;
            if (editor == EditorKind.Toggle)
            {
                var window = KitAlmanacDialog.Create(owner.Window, title, new Vector2(500, 220)); if (window == null) return;
                var toggle = KitToggle.Create(window.Panel, "Value", bool.TryParse(d.Current, out var initial) && initial, value => { window.Dispose(); Save(d, value ? "true" : "false"); }, new Vector2(20, -65), new Vector2(460, 40), ThingPanel.Ui("on"), ThingPanel.Ui("off"));
                toggle.Tooltip(title, d.Description + (d.Attributes.RequiresRestart ? "\n" + ThingPanel.Ui("config_restart") : ""));
                window.Window.Focusable(toggle.Button, 0, 0); window.Own(toggle.Dispose);
                Close(window, 150); return;
            }
            if (d.Shape.AcceptableList != null || d.Shape.Kind == SettingKind.Enum)
            {
                var choices = d.Shape.AcceptableList ?? d.Shape.EnumNames; var flags = d.Shape.IsFlags && d.Shape.AcceptableList == null;
                KitAlmanacChoice.Open(owner.Window, title, choices, flags ? d.Current.Split(',').Select(s => s.Trim()).ToArray() : new[] { d.Current }, flags,
                    values => { if (values.Count > 0) Save(d, flags ? string.Join(", ", values) : values[0]); else if (flags) Save(d, "0"); else Message(ThingPanel.Ui("config_choose_value")); }); return;
            }
            if (editor == EditorKind.KeyCapture || editor == EditorKind.ShortcutCapture)
            {
                KitAlmanacKeyCapture.Open(owner.Window, title, editor == EditorKind.ShortcutCapture, chord => Save(d, editor == EditorKind.ShortcutCapture
                    ? TomlTypeConverter.ConvertToString(chord, typeof(KeyboardShortcut)) : chord.MainKey.ToString())); return;
            }
            if (editor == EditorKind.ColorPicker && ColorValue.TryParse(d.Current, out var color))
            {
                KitAlmanacColor.Open(owner.Window, title, new Color32(color.R, color.G, color.B, color.A), value => Save(d, TomlTypeConverter.ConvertToString(value, typeof(Color)))); return;
            }
            if (editor == EditorKind.Slider || editor == EditorKind.Spinner)
            { Number(d, owner, title); return; }
            if (editor == EditorKind.VectorRow) { Vector(d, owner, title); return; }
            if (editor == EditorKind.ListEditor && d.Shape.Kind == SettingKind.String && d.Attributes.ListSeparator != null)
            {
                var separator = d.Attributes.ListSeparator;
                var initial = ((string)TomlTypeConverter.ConvertToValue(d.Current, typeof(string))).Split(new[] { separator }, StringSplitOptions.None);
                KitAlmanacList.Open(owner.Window, title, initial, _ => "", values => Save(d, TomlTypeConverter.ConvertToString(string.Join(separator, values), typeof(string)))); return;
            }
            if (editor == EditorKind.ListEditor && d.Shape.Kind == SettingKind.List)
            { ThingPanelLists.Open(d, owner, Message, () => Raw(d, owner)); return; }
            Raw(d, owner);
        }
        static void Raw(ConfigEntryDocument d, KitAlmanacDialog owner)
        {
            var title = d.Section + "." + d.Key;
            var decoded = d.Shape.Kind == SettingKind.String;
            var text = decoded ? (string)TomlTypeConverter.ConvertToValue(d.Current, typeof(string)) : d.Current;
            string Encode(string value) => decoded ? TomlTypeConverter.ConvertToString(value, typeof(string)) : value;
            KitAlmanacTextEditor.Open(owner.Window, title, text, value => Validate(d, Encode(value))!, value => Save(d, Encode(value)));
        }
        static void Number(ConfigEntryDocument d, KitAlmanacDialog owner, string title)
        {
            var window = KitAlmanacDialog.Create(owner.Window, title, new Vector2(600, 270)); if (window == null) return;
            var text = d.Current; var step = d.Attributes.Step ?? (d.Shape.HasRange ? (d.Shape.RangeMax - d.Shape.RangeMin) / 100 : 1);
            if (d.Shape.Kind == SettingKind.Integer) step = Math.Max(1, step);
            string Change(int direction)
            {
                if (d.Shape.Kind == SettingKind.Integer && decimal.TryParse(text, NumberStyles.Integer, Inv, out var exact))
                { try { return (exact + direction * (decimal)step).ToString(Inv); } catch { return text; } }
                if (!double.TryParse(text, NumberStyles.Float, Inv, out var current)) return text;
                var next = current + direction * step;
                return double.IsNaN(next) || double.IsInfinity(next) ? text : next.ToString("R", Inv);
            }
            var number = KitAlmanacNumber.Create(window.Panel, "Value", new Vector2(20, -70), new Vector2(560, 40), text, value => Validate(d, value) == null,
                value => { text = value; window.Dispose(); Save(d, value); }, Change, d.Shape.HasRange ? d.Shape.RangeMin : (double?)null, d.Shape.HasRange ? d.Shape.RangeMax : (double?)null,
                step, d.Attributes.ShowRangeAsPercent == true, value => d.Shape.Kind == SettingKind.Integer ? Math.Round(value).ToString(Inv) : value.ToString("R", Inv));
            window.Own(number.Dispose); window.TypingField = () => number.Field.Focused;
            foreach (var control in new Selectable[] { number.Field.Input, number.Less, number.More }) KitTooltip.Set(control.gameObject, title, d.Description);
            window.Window.Focusable(number.Field.Input, 1, 0); window.Window.Focusable(number.Less, 0, 0); window.Window.Focusable(number.More, 2, 0);
            Close(window, 200);
        }
        static void Vector(ConfigEntryDocument d, KitAlmanacDialog owner, string title)
        {
            var parsed = ValueCodec.Parse(d.Shape, d.Current);
            if (!parsed.Ok || !(parsed.Value is float[] initial)) { Message(ThingPanel.Ui("invalid")); return; }
            var window = KitAlmanacDialog.Create(owner.Window, title, new Vector2(620, 290)); if (window == null) return;
            var fields = new List<KitTextField>(); var axes = "xyzw";
            for (var i = 0; i < initial.Length; i++)
            {
                KitAlmanacDialog.Label(window.Panel, "Axis" + i, axes[i].ToString().ToUpperInvariant(), new Vector2(20 + i * 145, -65), new Vector2(130, 25));
                var field = KitTextField.Create(window.Panel, "Value" + i, KitFieldMode.Text, new Vector2(20 + i * 145, -100), new Vector2(130, 40));
                field.Text = initial[i].ToString("R", Inv); fields.Add(field); window.Own(field.Destroy); window.Window.Focusable(field.Input, i, 0);
                KitTooltip.Set(field.Input.gameObject, title + " / " + axes[i].ToString().ToUpperInvariant(), d.Description);
            }
            window.TypingField = () => fields.Any(f => f.Focused);
            var apply = KitButton.Create(window.Panel, "Apply", ThingPanel.Ui("apply"), () =>
            {
                var value = "{" + string.Join(",", fields.Select((f, i) => "\"" + axes[i] + "\":" + f.Text)) + "}";
                if (Validate(d, value) != null) return; window.Dispose(); Save(d, value);
            }, new Vector2(20, -190), new Vector2(580, 36)); window.Window.Focusable(apply, 0, 1);
            Close(window, 240);
        }
        static void Close(KitAlmanacDialog window, float y)
        { var close = KitButton.Create(window.Panel, "Close", ThingPanel.Ui("close"), window.Dispose, new Vector2(20, -y), new Vector2(window.Panel.rect.width - 40, 32)); window.Window.Focusable(close, 0, 5); }
        static void Message(string text) { if (status != null) status.text = text; }
        static void Release() { ThingPanelYaml.Reset(); list?.Dispose(); list = null; dialog = null; page = null; status = null; selected = file = ""; }
        internal static void Reset() { var old = dialog; dialog = null; old?.Dispose(); Release(); }
    }
}
