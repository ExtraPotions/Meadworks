// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Thing.Communication;
using ExtraPotions.Thing.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingPanelMessages
    {
        static KitAlmanacDialog? editor;
        static KitVirtualList? list;
        static ThingUiPage? page;
        static int offset, selected = -1;
        static long revision;
        static TMP_Text? status;
        internal static void Motd()
        {
            ThingPanelJobs.Start(new[] { "motd", "get" }, lines =>
            {
                if (!MessageDocument.TryDecode(lines, out var document) || document.Kind != "motd") { ThingPanel.Message(string.Join("\n", lines)); return; }
                ThingPanelForms.Open(ThingPanel.Ui("motd"), ThingPanel.Ui("motd_hint"), new[] { ThingPanel.Ui("text") }, new[] { document.Text }, values =>
                    values[0].Trim().Length == 0 ? new[] { "motd", "clear", document.Version }
                    : new[] { "motd", "set", document.Version }.Concat(MessageEditing.Pack(values[0])).ToArray(), true);
            });
        }
        internal static void Announcements()
        {
            if (ThingPanel.Window == null || !ThingPanel.CanAdmin || ThingPanelJobs.Busy) return;
            Reset(); offset = 0;
            var current = KitAlmanacDialog.Create(ThingPanel.Window, ThingPanel.Ui("announcements"), new Vector2(800, 600)); if (current == null) return;
            current.Dim();
            editor = current;
            current.Own(() => { if (ReferenceEquals(editor, current)) Release(); });
            Button Control(string key, Action click, float x, float y, float width, int gridX, int gridY)
            {
                var button = KitButton.Create(current.Panel, key, ThingPanel.Ui(key), () => { if (ThingPanel.CanAdmin && !ThingPanelJobs.Busy) click(); }, new Vector2(x, -y), new Vector2(width, 36));
                ThingPanel.Tip(button.gameObject, key);
                current.Window.Focusable(button, gridX, gridY); return button;
            }
            Control("refresh", Refresh, 20, 60, 230, 0, 0);
            Control("previous", () => { offset = Math.Max(0, offset - ThingUiPage.PageSize); Refresh(); }, 280, 60, 230, 1, 0);
            Control("next", () => { if (page != null && offset + ThingUiPage.PageSize < page.Total) { offset += ThingUiPage.PageSize; Refresh(); } }, 540, 60, 230, 2, 0);
            list = KitVirtualList.Create(current.Panel, "Announcements", new Vector2(760, 310), parent =>
            { var row = new GameObject("Row", typeof(RectTransform)); row.transform.SetParent(parent, false); return (RectTransform)row.transform; });
            if (list != null) KitAlmanacDialog.Place(list.Root, new Vector2(20, -112), new Vector2(760, 310));
            status = KitAlmanacDialog.Label(current.Panel, "Status", ThingPanel.Ui("announcement_hint"), new Vector2(20, -430), new Vector2(760, 50), 14);
            Control("add", Add, 20, 490, 170, 0, 20);
            Control("edit", Edit, 215, 490, 170, 1, 20);
            Control("remove", Remove, 410, 490, 170, 2, 20);
            Control("clear", Clear, 605, 490, 170, 3, 20);
            var close = KitButton.Create(current.Panel, "Close", ThingPanel.Ui("close"), current.Dispose, new Vector2(20, -548), new Vector2(760, 36));
            current.Window.Focusable(close, 0, 21);
            current.Tick += () => { if (!ThingPanel.CanAdmin) { current.Dispose(); return; } list?.Tick(); };
            Refresh();
        }
        static void Refresh()
        {
            var current = editor; var requestedOffset = offset;
            if (current == null) return;
            ThingPanelJobs.Start(new[] { "announce", "list", offset.ToString(CultureInfo.InvariantCulture) }, lines =>
            {
                if (!ReferenceEquals(editor, current) || offset != requestedOffset) return;
                if (!ThingUiPage.TryDecode(lines, out var loaded) || loaded.Page != "announcements" || loaded.Version.Length == 0 || loaded.Offset != requestedOffset)
                { if (status != null) status.text = string.Join("\n", lines); return; }
                page = loaded; selected = -1; Render();
                if (status != null) status.text = ThingText.Format("ui_page", offset + (page.Rows.Count == 0 ? 0 : 1), offset + page.Rows.Count, page.Total);
            });
        }
        static void Render()
        {
            if (list == null || editor == null || page == null) return;
            var current = editor;
            list.SetRows(page.Rows.Select((row, at) => new KitVirtualRow(row.Id, 72, rect =>
            {
                foreach (Transform child in rect) KitUnity.Retire(child.gameObject);
                var caption = (selected.ToString(CultureInfo.InvariantCulture) == row.Id ? "> " : "") + ThingText.Format("ui_announcement_every", row.Caption) + "\n" + row.Detail;
                var button = KitButton.Create(rect, "Select", caption, () => { if (int.TryParse(row.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var index)) { selected = index; Render(); } }, new Vector2(0, -2), new Vector2(740, 68));
                var words = button.GetComponentInChildren<TMP_Text>(true); if (words != null) { words.richText = false; words.textWrappingMode = TextWrappingModes.Normal; }
                current.Window.Focusable(button, 0, at + 1);
            }, rect => { foreach (var control in rect.GetComponentsInChildren<Selectable>(true)) current.Window.Unfocusable(control); })).ToArray(), ++revision);
        }
        static bool Ready => editor != null && page != null && MessageEditing.Version(page.Version) && ThingPanel.CanAdmin && !ThingPanelJobs.Busy;
        static void Add()
        {
            if (!Ready) return;
            var version = page!.Version;
            ThingPanelForms.Open(ThingPanel.Ui("add_announcement"), ThingPanel.Ui("announcement_hint"), new[] { ThingPanel.Ui("repeat_minutes"), ThingPanel.Ui("text") }, new[] { "30", "" },
                values => new[] { "announce", "add", version, values[0] }.Concat(MessageEditing.Pack(values[1])).ToArray(), true);
        }
        static void Edit()
        {
            if (!Ready || selected < 0) { ThingPanel.Message(ThingPanel.Ui("select_announcement")); return; }
            var current = editor; var index = selected;
            ThingPanelJobs.Start(new[] { "announce", "get", index.ToString(CultureInfo.InvariantCulture) }, lines =>
            {
                if (!ReferenceEquals(editor, current)) return;
                if (!MessageDocument.TryDecode(lines, out var document) || document.Kind != "announce" || document.Index != index)
                { if (status != null) status.text = string.Join("\n", lines); return; }
                ThingPanelForms.Open(ThingPanel.Ui("edit_announcement"), ThingPanel.Ui("announcement_hint"), new[] { ThingPanel.Ui("repeat_minutes"), ThingPanel.Ui("text") },
                    new[] { document.Minutes.ToString(CultureInfo.InvariantCulture), document.Text },
                    values => new[] { "announce", "set", document.Version, document.Index.ToString(CultureInfo.InvariantCulture), values[0] }.Concat(MessageEditing.Pack(values[1])).ToArray(), true);
            });
        }
        static void Remove()
        {
            if (!Ready || selected < 0) { ThingPanel.Message(ThingPanel.Ui("select_announcement")); return; }
            var current = editor!; var version = page!.Version; var index = selected;
            ThingPanelForms.Confirm(current.Window, ThingPanel.Ui("remove_announcement"), () => { current.Dispose(); ThingPanel.Send(new[] { "announce", "remove", version, index.ToString(CultureInfo.InvariantCulture) }); });
        }
        static void Clear()
        {
            if (!Ready) return;
            var current = editor!; var version = page!.Version;
            ThingPanelForms.Confirm(current.Window, ThingPanel.Ui("clear_announcements_warning"), () => { current.Dispose(); ThingPanel.Send(new[] { "announce", "clear", version }); });
        }
        static void Release() { list?.Dispose(); list = null; editor = null; page = null; status = null; selected = -1; }
        internal static void Reset() { var old = editor; editor = null; old?.Dispose(); Release(); }
    }
}
