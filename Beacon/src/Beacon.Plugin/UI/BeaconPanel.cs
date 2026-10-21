// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the BC05 server panel composes the existing Crucible window, virtual list and dialogs.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Beacon.Config;
using ExtraPotions.Beacon.Secrets;
using ExtraPotions.Beacon.Servers;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Beacon.Runtime
{
    internal static class BeaconPanel
    {
        static KitWindow? window;
        static KitVirtualList? list;
        static TMP_Text? status, storage;
        static string? selected;
        static string fingerprint = "";
        static long revision;
        static IDisposable? refresh;
        static float nextRefresh;
        static string title = "";
        static GameObject? hiddenMenu;
        static readonly List<Button> selectedActions = new List<Button>();
        static readonly List<Button> busyActions = new List<Button>();
        // The add/edit editor uses the same size so it covers the list completely.
        internal static readonly Vector2 Size = new Vector2(780, 610);
        internal static string Ui(string key) => BeaconText.Text("ui_" + key);
        internal static KitWindow? Window => window;
        internal static ServerEntry? Selected => BeaconServers.List.Entries.FirstOrDefault(e => e.SecretRef == selected);

        public static void Tick()
        {
            if (!Plugin.Ready || Game.instance != null || FejdStartup.instance == null) { Reset(); return; }
            BeaconPanelMenu.Tick();
            var key = Plugin.Settings?.Shortcut(BeaconSettings.General.PanelKey);
            if (key != null && MeadworksInput.Pressed(key, InputContext.Global))
            { if (window?.IsOpen == true) Reset(); else Open(); }
            if (window?.IsOpen != true) return;
            list?.Tick();
            if (title != Ui("servers") && KitAlmanacDialog.Active == null) { Open(); return; }
            if (!MeadworksInput.Typing && KitAlmanacDialog.Active == null)
            {
                if (MeadworksInput.PadDown(PadButton.LeftBumper)) { MeadworksInput.PadConsume(PadButton.LeftBumper); SelectOffset(-1); }
                if (MeadworksInput.PadDown(PadButton.RightBumper)) { MeadworksInput.PadConsume(PadButton.RightBumper); SelectOffset(1); }
            }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .25f;
            var current = ServerListCodec.Write(BeaconServers.List) + "|" + BeaconSecrets.Store.Backend + "|" + BeaconText.Text("ui_servers")
                + string.Join("", BeaconServers.List.Entries.Select(e => BeaconSecrets.Store.Contains(e.SecretRef) ? "1" : "0"));
            if (fingerprint != current) { fingerprint = current; Refresh(); }
            RefreshControls();
            if (BeaconImportSecrets.Busy) Message(BeaconText.Text("import_password_pending"));
        }

        public static void Open()
        {
            if (!Plugin.Ready || FejdStartup.instance == null || Game.instance != null || !CrucibleUiPlugin.Active) return;
            BeaconAutoJoin.Cancel(Plugin.Log);
            ResetWindow();
            var canvas = FejdStartup.instance.m_mainMenu?.GetComponentInParent<Canvas>(true)?.rootCanvas;
            if (canvas == null) return;
            window = KitWindow.Create("Beacon_Servers", Size, canvas.transform);
            if (window == null) return;
            title = Ui("servers");
            var heading = KitAlmanacDialog.Label(window.Panel, "Title", title, new Vector2(20, -14), new Vector2(740, 30), 22);
            if (KitCanvas.Heading != null) heading.font = KitCanvas.Heading;
            storage = KitAlmanacDialog.Label(window.Panel, "Storage", "", new Vector2(20, -50), new Vector2(740, 42));
            list = KitVirtualList.Create(window.Panel, "Servers", new Vector2(740, 340), parent =>
            {
                var row = new GameObject("Server", typeof(RectTransform)); row.transform.SetParent(parent, false);
                return (RectTransform)row.transform;
            });
            if (list != null) KitAlmanacDialog.Place(list.Root, new Vector2(20, -96), new Vector2(740, 340));
            Add("previous", () => SelectOffset(-1), 0, 0);
            Add("next", () => SelectOffset(1), 1, 0);
            Add("add", () => BeaconEditor.Open(null), 2, 0);
            Add("edit", () => { if (Selected != null) BeaconEditor.Open(Selected); }, 3, 0, true);
            Add("join", Join, 4, 0);
            Add("remove", Remove, 0, 1, true);
            Add("up", () => Move(-1), 1, 1, true);
            Add("down", () => Move(1), 2, 1, true);
            Add("default", SetDefault, 3, 1, true);
            Add("close", Reset, 4, 1);
            Add("forget", () => Forget(false), 0, 2, true);
            Add("forget_all", () => Forget(true), 1, 2);
            status = KitAlmanacDialog.Label(window.Panel, "Status", "", new Vector2(470, -546), new Vector2(290, 48), 14);
            window.Closed += Reset;
            window.Open(); fingerprint = ""; Refresh();
            HideMenu();
            refresh = BeaconSecretActions.Run(BeaconSecretActions.Refresh, _ => fingerprint = "", () => Message(BeaconText.Text("secret_store_failed")));
        }

        static void Add(string key, Action action, int x, int y, bool needsSelection = false)
        {
            var button = KitButton.Create(window!.Panel, key, Ui(key), action, new Vector2(20 + x * 150, -450 - y * 48), new Vector2(140, 38));
            window.Focusable(button, x, y + 1000);
            Tip(button.gameObject,key);
            if (needsSelection) selectedActions.Add(button);
            if (key != "previous" && key != "next" && key != "close") busyActions.Add(button);
        }

        static void Refresh()
        {
            if (window == null || list == null) return;
            if (Selected == null) selected = BeaconServers.List.Entries.FirstOrDefault()?.SecretRef;
            storage!.text = BeaconText.Text(SecretStores.StorageLineKey(BeaconSecrets.Store.Backend));
            var rows = new List<KitVirtualRow>();
            for (var index = 0; index < BeaconServers.List.Count; index++)
            {
                var entry = BeaconServers.List.Entries[index]; var at = index;
                rows.Add(new KitVirtualRow(entry.SecretRef, 66, rect =>
                {
                    foreach (Transform child in rect) KitUnity.Retire(child.gameObject);
                    var caption = (selected == entry.SecretRef ? "> " : "") + entry.Name + "\n" + entry.Address + "  " + BeaconText.Format("ui_alias_count", entry.Aliases.Count);
                    var button = KitButton.Create(rect, "Select", caption, () => { selected = entry.SecretRef; fingerprint = ""; Refresh(); }, new Vector2(0, -2), new Vector2(600, 60));
                    Tip(button.gameObject,"select",BeaconText.Text("ui_help_select")+"\n"+entry.Name+"\n"+entry.Address);
                    var words = button.GetComponentInChildren<TMP_Text>(true);
                    if (words != null) { words.richText = false; words.textWrappingMode = TextWrappingModes.Normal; }
                    window.Focusable(button, 0, at + 1);
                    if (entry.IsDefault)
                    {
                        var star = KitIconButton.Create(rect, "Default", KitGlyph.Star, new Vector2(610, -14), 30);
                        star.SetUsable(false); star.Tooltip(Ui("default"), Ui("default"));
                    }
                    if (BeaconSecrets.Store.Contains(entry.SecretRef))
                    {
                        if (Enum.TryParse<KitGlyph>("Lock", out var glyph))
                        {
                            var locked = KitIconButton.Create(rect, "Stored", glyph, new Vector2(660, -14), 30);
                            locked.SetUsable(false); locked.Tooltip(Ui("password"), Ui("stored"));
                        }
                        else KitAlmanacDialog.Label(rect, "Stored", Ui("stored"), new Vector2(648, -13), new Vector2(80, 44), 13);
                    }
                }, rect => { foreach (var control in rect.GetComponentsInChildren<Selectable>(true)) window?.Unfocusable(control); }));
            }
            list.SetRows(rows, ++revision);
            RefreshControls();
            if (BeaconServers.List.Count == 0) Message(Ui("empty"));
            else if (!BeaconServers.Writable) Message(Ui("read_only"));
            else if (!BeaconJoin.Active) Message(Ui("step_aside"));
        }

        static void RefreshControls()
        {
            foreach (var action in busyActions)
            {
                if (action == null) continue;
                var needsEntry = selectedActions.Contains(action) || action.name == "join";
                var writesList = action.name != "join" && action.name != "forget" && action.name != "forget_all";
                action.interactable = !BeaconImportSecrets.Busy && (!needsEntry || Selected != null) && (!writesList || BeaconServers.Writable)
                    && (action.name != "join" || BeaconJoin.Active);
            }
        }

        static void SelectOffset(int offset)
        {
            var entries = BeaconServers.List.Entries; if (entries.Count == 0) return;
            var index = entries.ToList().FindIndex(e => e.SecretRef == selected);
            selected = entries[(Math.Max(index, 0) + offset + entries.Count) % entries.Count].SecretRef;
            Refresh(); list?.Focus(selected);
        }
        static void Join()
        {
            if (BeaconImportSecrets.Busy) { Message(BeaconText.Text("import_password_pending")); return; }
            var entry = Selected; if (entry == null || !BeaconJoin.Active) { Message(Ui("step_aside")); return; }
            // The game's own join starts from its visible main menu, as an invited join does.
            ShowMenu();
            if (BeaconMenu.Join(entry.Address, null)) Reset(); else { HideMenu(); Message(BeaconText.Text("cmd_join_not_ready")); }
        }
        static void Move(int offset)
        {
            var entry = Selected; if (entry == null) return;
            var index = BeaconServers.List.Entries.ToList().IndexOf(entry);
            Amend(candidate => candidate.Move(entry.Name, index + offset));
        }
        static void SetDefault()
        {
            var entry = Selected; if (entry != null) Amend(candidate => candidate.SetDefault(entry.IsDefault ? null : entry.Name));
        }
        static void Remove()
        {
            var entry = Selected; if (entry == null) return;
            Confirm(BeaconText.Format("ui_remove_confirm", entry.Name), () => Amend(candidate => candidate.Remove(entry.Name)));
        }
        static void Forget(bool all)
        {
            if (BeaconImportSecrets.Busy) { Message(BeaconText.Text("import_password_pending")); return; }
            var entry = Selected; if (!all && entry == null) return;
            Confirm(all ? Ui("forget_all_confirm") : BeaconText.Format("ui_forget_confirm", entry!.Name), () =>
            {
                if (BeaconImportSecrets.Busy) { Message(BeaconText.Text("import_password_pending")); return; }
                if (all) BeaconSecretActions.Run(BeaconSecretActions.ForgetAll, count => { Message(BeaconText.Format("passwords_forgotten_all", count)); fingerprint = ""; }, () => Message(BeaconText.Text("secret_store_failed")));
                else BeaconSecretActions.Run(cancel => BeaconSecretActions.Forget(entry!.SecretRef, cancel), forgotten => { Message(forgotten ? BeaconText.Format("password_forgotten", entry!.Name) : BeaconText.Text("secret_store_failed")); fingerprint = ""; }, () => Message(BeaconText.Text("secret_store_failed")));
            });
        }
        internal static bool Amend(Action<ServerList> change)
        {
            if (BeaconImportSecrets.Busy) { Message(BeaconText.Text("import_password_pending")); return false; }
            if (Plugin.Log == null) return false;
            try { if (!BeaconServers.Amend(change, Plugin.Log)) { Message(Ui("write_failed")); return false; } }
            catch (Exception error) { Plugin.Log.Warning("Beacon server edit rejected: " + error.GetType().Name); Message(Ui("invalid")); return false; }
            fingerprint = ""; Refresh(); return true;
        }
        internal static void Message(string words) { if (status != null) status.text = words; }
        internal static void Confirm(string prompt, Action commit, KitWindow? parent = null)
        {
            if (window == null) return;
            var dialog = KitAlmanacDialog.Create(parent ?? window, prompt, new Vector2(560, 240)); if (dialog == null) return;
            var title = dialog.Panel.Find("Title")?.GetComponent<TMP_Text>();
            if (title != null) title.rectTransform.sizeDelta = new Vector2(528, 135);
            var yes = KitButton.Create(dialog.Panel, "Confirm", Ui("confirm"), () => { dialog.Dispose(); commit(); }, new Vector2(20, -180), new Vector2(240, 38));
            var no = KitButton.Create(dialog.Panel, "Cancel", Ui("cancel"), dialog.Dispose, new Vector2(300, -180), new Vector2(240, 38));
            Tip(yes.gameObject,"confirm",prompt);Tip(no.gameObject,"cancel");
            dialog.Window.Focusable(yes, 0, 0); dialog.Window.Focusable(no, 1, 0);
        }
        internal static void Tip(GameObject control,string key,string? explanation=null)
            =>KitTooltip.Set(control,Ui(key=="select"?"servers":key),explanation??BeaconText.Text("ui_help_"+key));
        // Like the game's Start Game, Settings and Credits screens, the panel replaces the main menu while it is open.
        static void HideMenu()
        {
            var menu = FejdStartup.instance?.m_mainMenu;
            if (menu != null && menu.activeSelf) { menu.SetActive(false); hiddenMenu = menu; }
        }
        static void ShowMenu()
        {
            if (hiddenMenu != null) hiddenMenu.SetActive(true);
            hiddenMenu = null;
        }
        static void ResetWindow()
        {
            ShowMenu();
            refresh?.Dispose(); refresh = null;
            BeaconEditor.Reset();
            var active = KitAlmanacDialog.Active;
            if (active != null && window != null && ReferenceEquals(active.Parent, window)) active.Dispose();
            list?.Dispose(); list = null; selectedActions.Clear(); busyActions.Clear();
            var old = window; window = null; status = storage = null;
            if (old != null) { old.Closed -= Reset; old.Close(); old.Destroy(); }
        }
        public static void Reset() { ResetWindow(); BeaconPanelMenu.Reset(); fingerprint = ""; }
    }
}
