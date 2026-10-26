// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: TG07 administration on the existing Crucible window, tabs, virtual rows and editors.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Config;
using ExtraPotions.Thing.Moderation;
using ExtraPotions.Thing.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    public static class ThingPanel
    {
        static readonly string[] tabs = { "people", "moderation", "messages", "world", "regeneration", "server_config" };
        static KitWindow? window;
        static RectTransform? content;
        static KitVirtualList? list;
        static ThingUiPage? page;
        static TMP_Text? status, title;
        static readonly List<Selectable> controls = new List<Selectable>();
        static readonly List<Button> actions = new List<Button>();
        static string query = "players", selected = "", language = "";
        static int tab, offset;
        static long generation, revision;
        internal static KitWindow? Window => window;
        internal static string Ui(string key) => ThingText.Text("ui_" + key);
        internal static bool CanAdmin
        {
            get
            {
                if (!Plugin.Ready || !Plugin.AllowsUi || !CruciblePlugin.IsReady || Player.m_localPlayer == null || !ThingModules.ProductActive(out _)) return false;
                var session = CruciblePlugin.Session;
                return session.AuthorityReady && session.IsAuthority;
            }
        }
        internal static bool CanConfigure
        {
            get
            {
                if (!Plugin.Ready || !Plugin.AllowsUi || !CruciblePlugin.IsReady || Player.m_localPlayer == null || Plugin.Settings == null || !ThingModules.ProductActive(out _)) return false;
                var session = CruciblePlugin.Session;
                return session.AuthorityReady && ExtraPotions.Thing.Admin.SettingsAccess.CanConfigure(session.IsAuthority,
                    session.Role == SessionRole.RemoteClient && session.IsAdmin, Plugin.Settings.AllowAdminSettingsEditing, Plugin.Settings.Get(ThingSettings.Remote.Editing));
            }
        }
        internal static bool CanOpen => CanAdmin || CanConfigure;
        internal static bool KeepsJob => CanOpen || CanPeople || AskingSelf;
        internal static bool AskingSelf { get; private set; }
        static bool SelfKnown => ThingSelf.Known && CruciblePlugin.IsReady && ThingSelf.Generation == CruciblePlugin.Session.Generation;
        internal static bool CanEditGrants => CanAdmin || SelfKnown && ThingSelf.FullAdmin;
        internal static bool CanPeople => CanAdmin || SelfKnown && (ThingSelf.FullAdmin || ThingSelf.Held != AdminRight.None);
        internal static bool CanViewInventory => CanAdmin || SelfKnown && (ThingSelf.FullAdmin || AdminPermissions.Covers(ThingSelf.Held, ThingAction.InventoryView));
        internal static bool CanMutateInventory => Holds(AdminRight.Inventory);
        static bool Holds(AdminRight bit) => CanAdmin || SelfKnown && (ThingSelf.FullAdmin || (ThingSelf.Held & bit) == bit
            || bit == AdminRight.Teleport && AdminPermissions.Covers(ThingSelf.Held, ThingAction.TeleportTo)
            || bit == AdminRight.Freeze && AdminPermissions.Covers(ThingSelf.Held, ThingAction.FreezePlayer));
        static string shownFor = "";
        static AdminRight shown;
        static bool shownReady;
        static string grantAsk = "";
        static float nextSelf;
        static string RowKey(ThingUiRow row, int at) => row.Kind + ":" + row.Id + ":" + (offset + at).ToString(CultureInfo.InvariantCulture);
        static ThingUiRow? Selected => page?.Rows.Where((row, at) => RowKey(row, at) == selected).FirstOrDefault();

        public static void Tick()
        {
            ThingPanelMenu.Tick();
            ThingInventoryWindow.Tick();
            if (Plugin.Settings == null || !Plugin.Ready || !Plugin.AllowsUi) { ResetWindow(); return; }
            var context = window?.IsOpen == true ? InputContext.Window : InputContext.Gameplay;
            if (MeadworksInput.Pressed(Plugin.Settings.ShortcutValue(ThingSettings.General.AdminWindowKey), context))
            { if (window?.IsOpen == true) ResetWindow(); else Open(); }
            ThingPanelJobs.Tick();
            if (window == null) return;
            if ((!CanOpen && !CanPeople) || generation != CruciblePlugin.Session.Generation) { ResetWindow(); return; }
            list?.Tick();
            if (tab == 0 && (query == "players" || query == "grants") && CanEditGrants && Selected != null)
                EnsureGrant(Selected.Id);
            if (!CanAdmin && !ThingPanelJobs.Busy && Time.unscaledTime >= nextSelf)
            {
                nextSelf = Time.unscaledTime + 10f;
                ThingPanelJobs.Start(new[] { "grant", "self" }, lines =>
                {
                    if (lines.Count > 0) ThingSelf.Apply(CruciblePlugin.Session.Generation, lines[0]);
                    if (window != null && !CanPeople && !CanOpen) ResetWindow();
                });
            }
            foreach (var action in actions) if (action != null) action.interactable = !ThingPanelJobs.Busy;
            if (language != MeadworksBlockedOptions.Language && KitAlmanacDialog.Active == null && !ThingPanelJobs.Busy) { language = MeadworksBlockedOptions.Language; Draw(); }
            if (!CanAdmin || MeadworksInput.Typing || KitAlmanacDialog.Active != null) return;
            if (MeadworksInput.PadDown(PadButton.LeftBumper)) { MeadworksInput.PadConsume(PadButton.LeftBumper); SelectTab((tab + tabs.Length - 1) % tabs.Length); }
            if (MeadworksInput.PadDown(PadButton.RightBumper)) { MeadworksInput.PadConsume(PadButton.RightBumper); SelectTab((tab + 1) % tabs.Length); }
        }
        public static void Open()
        {
            if (CanAdmin) { Show(); return; }
            if (!Plugin.AllowsUi || Player.m_localPlayer == null) return;
            if (CruciblePlugin.IsReady && CruciblePlugin.Session.AuthorityReady && !CruciblePlugin.Session.IsAuthority && !CanPeople)
            {
                AskingSelf = true;
                if (!ThingPanelJobs.Start(new[] { "grant", "self" }, lines =>
                {
                    AskingSelf = false;
                    if (lines.Count > 0) ThingSelf.Apply(CruciblePlugin.Session.Generation, lines[0]);
                    if (CanPeople || CanOpen) Show();
                    else if (Player.m_localPlayer != null) MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ThingText.Text("hint_not_admin"));
                }, failure: () => { AskingSelf = false; if (Player.m_localPlayer != null) MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ThingText.Text("hint_not_admin")); }))
                { AskingSelf = false; if (CanOpen) Show(); else if (Player.m_localPlayer != null) MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ThingText.Text("hint_not_admin")); }
                return;
            }
            if (CanPeople || CanOpen) { Show(); return; }
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ThingText.Text("hint_not_admin"));
        }
        static void Show()
        {
            if (!CrucibleUiPlugin.Active || (!CanOpen && !CanPeople)) return;
            ResetWindow(); generation = CruciblePlugin.Session.Generation; language = MeadworksBlockedOptions.Language;
            window = KitWindow.Create("Thing_Admin", new Vector2(1000, 700)); if (window == null) return;
            title = KitAlmanacDialog.Label(window.Panel, "Title", "Thing", new Vector2(20, -14), new Vector2(960, 32), 22);
            if (KitCanvas.Heading != null) title.font = KitCanvas.Heading;
            window.Closed += ResetWindow; window.Open(); Draw();
        }
        static void SelectTab(int at)
        {
            if (ThingPanelJobs.Busy) return;
            tab = at; offset = 0; selected = ""; page = null;
            query = tab == 0 ? "players" : tab == 4 ? "delve" : "audit"; Draw();
        }
        static void Draw(bool refreshRows = true)
        {
            if (window == null) return;
            ClearContent();
            var holder = new GameObject("Thing_Content", typeof(RectTransform)); holder.transform.SetParent(window.Panel, false); content = (RectTransform)holder.transform;
            KitAlmanacDialog.Place(content, Vector2.zero, new Vector2(1000, 700));
            status = KitAlmanacDialog.Label(content, "Status", "", new Vector2(220, -620), new Vector2(760, 60), 14);
            status.richText = false; status.raycastTarget = false;
            if (!CanAdmin && CanPeople)
            {
                Add("close", ResetWindow, 20, 620, 180, 0, 20, false);
                if (CanConfigure) Add("tab_server_config", ThingPanelConfig.Open, 20, 80, 180, 0, 1);
                tab = 0; if (query != "players" && !(query == "grants" && CanEditGrants)) query = "players";
                DrawRows(refreshRows); return;
            }
            if (!CanAdmin) { Add("close", ResetWindow, 20, 620, 180, 0, 20, false); DrawServerConfig(); return; }
            var buttons = KitTabs.Place(content, tabs.Select((name, at) => new KitTab { Id = name, Caption = Ui("tab_" + name), Click = () => SelectTab(at) }).ToList(), tabs[tab], 20, 65, 960);
            for (var i = 0; i < buttons.Count; i++) { KitAlmanacDialog.Place((RectTransform)buttons[i].transform, new Vector2(20, -65 - 52 * i), new Vector2(180, 42)); Register(buttons[i], 0, i + 1); Tip(buttons[i].gameObject, "tab_" + tabs[i]); }
            Add("close", ResetWindow, 20, 620, 180, 0, 20, false);
            if (tab < 2) DrawRows(refreshRows);
            else if (tab == 2) DrawMessages();
            else if (tab == 3) DrawWorld();
            else if (tab == 4) DrawRows(refreshRows);
            else DrawServerConfig();
        }
        static void DrawServerConfig()
        {
            // Same gate as the server's RemoteConfigRead/Write check: the dialog is offered only while the server allows remote editing.
            if (!CanConfigure)
            { KitAlmanacDialog.Label(content!, "ConfigOff", ThingText.Text("refused_disabled_by_server"), new Vector2(220, -80), new Vector2(740, 60)); return; }
            float configY = 80;
            if (CanAdmin)
            {
                KitAlmanacDialog.Label(content!, "DelegationLabel", Ui("settings_delegation"), new Vector2(220, -80), new Vector2(740, 35), 16);
                var toggle = KitToggle.Create(content!, "SettingsDelegation", Plugin.Settings!.AllowAdminSettingsEditing,
                    value => { if (CanAdmin) Plugin.Settings?.SetAdminSettingsEditing(value, generation); }, new Vector2(220, -120), new Vector2(350, 40), Ui("on"), Ui("off"));
                toggle.Tooltip(Ui("settings_delegation"), Ui("settings_delegation_hint"));
                Register(toggle.Button, 1, 1); configY = 185;
            }
            Add("tab_server_config", ThingPanelConfig.Open, 220, configY, 350, 1, 2);
            KitAlmanacDialog.Label(content!, "ConfigHint", Ui("config_hint"), new Vector2(220, -configY-65), new Vector2(740, 80), 14);
        }
        static void DrawRows(bool refreshRows = true)
        {
            Add("refresh", Refresh, 220, 65, 180, 1, 1);
            Add("previous", () => { offset = Math.Max(0, offset - ThingUiPage.PageSize); Refresh(); }, 420, 65, 180, 2, 1);
            Add("next", () => { if (page != null && offset + ThingUiPage.PageSize < page.Total) { offset += ThingUiPage.PageSize; Refresh(); } }, 620, 65, 180, 3, 1);
            if (tab == 0 && CanEditGrants) Add(query, () => { query = query == "players" ? "permitted" : query == "permitted" ? "admins" : query == "admins" ? "grants" : "players"; offset = 0; selected = ""; shownFor = ""; shownReady = false; page = null; Draw(); }, 820, 65, 160, 4, 1);
            if (tab == 4) Add(query == "delve" ? "delve_actions" : query == "delve_actions" ? "delve_due" : query == "delve_due" ? "delve_nearest" : query == "delve_nearest" ? "delve_nature" : "delve_kinds", () => { query = query == "delve" ? "delve_actions" : query == "delve_actions" ? "delve_due" : query == "delve_due" ? "delve_nearest" : query == "delve_nearest" ? "delve_nature" : "delve"; offset = 0; selected = ""; page = null; Draw(); }, 820, 65, 160, 4, 1);
            if (tab == 1) Add(query == "audit" ? "ledger" : "audit", () => { query = query == "audit" ? "moderation" : "audit"; offset = 0; selected = ""; Refresh(); }, 820, 65, 160, 4, 1);
            var people = tab == 0 && query == "players" && CruciblePlugin.Session.Role != SessionRole.SinglePlayer;
            var grantPage = tab == 0 && query == "grants";
            var listHeight = people && CanEditGrants ? 200f : people || grantPage ? 250f : 350f;
            list = KitVirtualList.Create(content!, "Rows", new Vector2(760, listHeight), parent =>
            { var row = new GameObject("Row", typeof(RectTransform)); row.transform.SetParent(parent, false); return (RectTransform)row.transform; });
            if (list != null) KitAlmanacDialog.Place(list.Root, new Vector2(220, -120), new Vector2(760, listHeight));
            if (people) DrawPlayerActions();
            else if (grantPage) DrawGrantPage();
            else if (tab == 0 && query != "players") DrawNativeListActions();
            else if (tab == 4) DrawDelveActions();
            else if (tab == 1 && CruciblePlugin.Session.Role != SessionRole.SinglePlayer) Add("unban", () =>
            {
                var target = Selected; if (target == null || target.Kind != "ban") { Message(Ui("select_ban")); return; }
                ThingPanelForms.Confirm(window!, Ui("unban") + "\n" + target.Caption, () => Send(new[] { "unban", target.Id }));
            }, 220, 490, 230, 1, 20);
            if (query == "audit") Message(Ui("audit_bounded"));
            if (refreshRows) Refresh();
            else RenderRows();
        }
        static void Refresh()
        {
            if (window == null || tab > 1 && tab != 4) return;
            var requested = query; var requestedOffset = offset;
            var args = tab == 4 ? new[] { "regeneration", requested == "delve" ? "kinds" : requested.Substring("delve_".Length), requestedOffset.ToString(CultureInfo.InvariantCulture) }
                : new[] { "ui-read", requested, requestedOffset.ToString(CultureInfo.InvariantCulture) };
            ThingPanelJobs.Start(args, lines =>
            {
                if (query != requested || offset != requestedOffset) return;
                if (!ThingUiPage.TryDecode(lines, out var loaded) || loaded.Page != requested || loaded.Offset != requestedOffset)
                { Message(string.Join("\n", lines)); return; }
                page = loaded; if (Selected == null) selected = "";
                RenderRows(); Message(ThingText.Format("ui_page", page.Offset + (page.Rows.Count == 0 ? 0 : 1), page.Offset + page.Rows.Count, page.Total));
            });
        }
        static void RenderRows()
        {
            if (window == null || list == null || page == null) return;
            var rows = page.Rows.Select((row, at) => new KitVirtualRow(RowKey(row, at), 72, rect =>
            {
                foreach (Transform child in rect) KitUnity.Retire(child.gameObject);
                var detail = row.Detail;
                if (row.Kind == "player" && Plugin.Settings?.Get(ThingSettings.General.ShowPlatformIds) != true) detail = "";
                var name = query == "delve" ? ThingText.Text("delve_kind_" + row.Id.ToLowerInvariant()) : row.Caption;
                var caption = (selected == RowKey(row, at) ? "> " : "") + name + "\n" + (row.Kind == "player" || tab == 4 ? "" : row.Kind + " | ") + detail;
                var button = KitButton.Create(rect, "Select", caption, () =>
                {
                    selected = RowKey(row, at);
                    if (shownFor != row.Id) { shownFor = ""; shownReady = false; }
                    if ((query == "players" || query == "grants") && CanEditGrants)
                    {
                        Draw(false);
                    }
                    else RenderRows();
                }, new Vector2(0, -2), new Vector2(740, 68));
                KitTooltip.Set(button.gameObject, name, Ui("help_select"));
                var words = button.GetComponentInChildren<TMP_Text>(true); if (words != null) { words.richText = false; words.textWrappingMode = TextWrappingModes.Normal; }
                window.Focusable(button, 1, at + 2);
            }, rect => { foreach (var control in rect.GetComponentsInChildren<Selectable>(true)) window?.Unfocusable(control); })).ToArray();
            list.SetRows(rows, ++revision);
        }
        static void DrawDelveActions()
        {
            if (query == "delve_due" || query == "delve_nearest" || query == "delve_nature")
            {
                KitAlmanacDialog.Label(content!, "Scope", Ui(query == "delve_nature" ? "delve_nature_scope" : "delve_site_scope"), new Vector2(220, -490), new Vector2(760, 100), 14);
                return;
            }
            Add("delve_request", () =>
            {
                var target = Selected;
                if (query != "delve" || target == null) { Message(Ui("delve_select")); return; }
                ThingPanelForms.Confirm(window!, Ui("delve_request_warning") + "\n" + ThingText.Text("delve_kind_" + target.Id.ToLowerInvariant()),
                    () => Send(new[] { "regenerate", target.Id }));
            }, 220, 490, 350, 1, 20);
            Add("delve_dryrun", () =>
            {
                var target = Selected; var version = page?.Version;
                if (query != "delve" || target == null || string.IsNullOrEmpty(version)) { Message(Ui("delve_select")); return; }
                ThingPanelForms.Open(Ui("delve_dryrun"), Ui("delve_dryrun_hint"), new[] { Ui("delve_dryrun") }, new[] { target.Kind == "dry_on" ? "on" : "off" },
                    values => new[] { "regeneration", "dryrun", target.Id, values[0], version! }, true);
            }, 610, 490, 350, 2, 20);
            KitAlmanacDialog.Label(content!, "Scope", Ui("delve_scope"), new Vector2(220, -545), new Vector2(760, 65), 14);
        }
        static void DrawNativeListActions()
        {
            var admins = query == "admins";
            if (admins && Plugin.Settings?.Get(ThingSettings.General.AdminListEditable) != true)
            {
                KitAlmanacDialog.Label(content!, "ReadOnly", Ui("admin_readonly"), new Vector2(220, -490), new Vector2(760, 60), 14);
                return;
            }
            if (!admins && CruciblePlugin.Session.Role == SessionRole.SinglePlayer) return;
            Add(admins ? "admin_add" : "permit", () => ThingPanelForms.Open(Ui(admins ? "admin_add" : "permit"), "",
                new[] { Ui("platform_id") }, new[] { "" }, values => admins ? new[] { "admin", "add", values[0] } : new[] { "permit", values[0] }, true), 220, 490, 350, 1, 20);
            Add(admins ? "admin_remove" : "unpermit", () =>
            {
                var target = Selected;
                if (target == null || target.Kind != (admins ? "admin" : "permit")) { Message(Ui("select_identity")); return; }
                ThingPanelForms.Confirm(window!, Ui(admins ? "admin_remove" : "unpermit") + "\n" + target.Id,
                    () => Send(admins ? new[] { "admin", "remove", target.Id } : new[] { "unpermit", target.Id }));
            }, 610, 490, 350, 2, 20);
        }
        static void Teleport(string verb)
        {
            var target = Selected; if (target == null || target.Kind != "player") { Message(Ui("select_player")); return; }
            ThingPanelForms.Confirm(window!, Ui(verb) + "\n" + target.Caption, () => Send(new[] { verb, target.Id }));
        }
        static void Moderate(string verb)
        {
            var target = Selected; if (target == null || target.Kind != "player") { Message(Ui("select_player")); return; }
            var mute = verb == "mute";
            var temporary = verb == "tempban" || mute;
            ThingPanelForms.Open(Ui(verb), target.Caption, temporary ? new[] { Ui(mute ? "mute_duration" : "duration"), Ui("reason") } : new[] { Ui("reason") },
                temporary ? new[] { mute ? (Plugin.Settings?.Get(ThingSettings.Moderation.MuteDefaultMinutes) ?? 30) + "m" : (Plugin.Settings?.Get(ThingSettings.Moderation.TempBanDefaultHours) ?? 24) + "h", "" } : new[] { "" },
                values => temporary ? new[] { verb, target.Id, values[0], values[1] } : new[] { verb, target.Id, values[0] }, verb != "warn" && verb != "mute");
        }
        static void DrawMessages()
        {
            if (CruciblePlugin.Session.Role == SessionRole.SinglePlayer)
            {
                Add("save_world", () => Send(new[] { "save" }), 220, 80, 350, 1, 1);
                KitAlmanacDialog.Label(content!, "NoOthers", ThingText.Text("refused_no_other_players"), new Vector2(220, -160), new Vector2(740, 110));
                return;
            }
            Add("broadcast", () => ThingPanelForms.Open(Ui("broadcast"), "", new[] { Ui("top_left"), Ui("text") }, new[] { "", "" }, values => new[] { values[0] == "top-left" ? "saytop" : "say", values[1] }, false), 220, 80, 350, 1, 1);
            Add("shutdown", () => ThingPanelForms.Open(Ui("shutdown"), "", new[] { Ui("minutes") }, new[] { "5" }, values => new[] { "shutdown", values[0] }, true), 610, 80, 350, 2, 1);
            Add("cancel_shutdown", () => Send(new[] { "cancelshutdown" }), 220, 145, 350, 1, 2);
            Add("save_world", () => Send(new[] { "save" }), 610, 145, 350, 2, 2);
            Add("motd", ThingPanelMessages.Motd, 220, 210, 350, 1, 3);
            Add("announcements", ThingPanelMessages.Announcements, 610, 210, 350, 2, 3);
        }
        static void DrawWorld()
        {
            if (!ThingModules.Active(ExtraPotions.Thing.Integration.ThingModule.WorldTools))
            { KitAlmanacDialog.Label(content!, "StepAside", Ui("world_step_aside"), new Vector2(220, -80), new Vector2(740, 100)); return; }
            Add("seed", () => Send(new[] { "seed" }), 220, 80, 350, 1, 1);
            Add("time", () => ThingPanelForms.Open(Ui("time"), "", new[] { Ui("clock"), Ui("freeze") }, new[] { "12:00", "" }, values =>
                values[1].Trim().Length == 0 ? new[] { "time", values[0] } : new[] { "time", values[0], "freeze" }, false), 610, 80, 350, 2, 1);
            Add("resume_time", () => Send(new[] { "resumetime" }), 220, 145, 350, 1, 2);
            Add("ward_on", () => WorldCommand(new[] { "ward", "on" }), 610, 145, 350, 2, 2);
            Add("ward_off", () => WorldCommand(new[] { "ward", "off" }), 220, 210, 350, 1, 3);
            Add("terrain", () => ThingPanelWorld.Terrain(), 610, 210, 350, 2, 3);
            Add("drops", () => ThingPanelWorld.Drops(), 220, 275, 350, 1, 4);
            Add("delve_reset_nearest", () => ThingPanelForms.Confirm(window!, Ui("delve_reset_nearest_warning"),
                () => Send(new[] { "resetdungeons", "nearest" })), 610, 275, 350, 2, 4);
            Add("regenerate_nature", () => ThingPanelForms.Confirm(window!, Ui("regenerate_nature_warning"),
                () => Send(new[] { "regenerate", "nature" })), 220, 340, 350, 1, 5);
            Add("reset_resources", () => ThingPanelForms.Confirm(window!, Ui("reset_resources_warning"),
                () => Send(new[] { "resetresources" })), 610, 340, 350, 2, 5);
            Add("pickup_radius", ThingPanelWorld.Pickup, 220, 405, 350, 1, 6);
        }
        static void WorldCommand(string[] args)
        {
            if (!CanAdmin || ThingPanelJobs.Busy) return;
            if (ThingWard.Run(args)) Message(Ui("world_console_result"));
        }
        internal static void Send(string[] args)
        {
            if (!CanAdmin && !CanPeople) return;
            ThingPanelJobs.Start(args, lines =>
            {
                Message(string.Join("\n", lines));
                if (args.Length > 1 && args[0] == "grant") RememberGrant(args[1], lines);
                if (args.Length > 2 && args[0] == "grant" && (args[2] == "add" || args[2] == "remove"))
                { offset = 0; selected = ""; shownFor = ""; shownReady = false; page = null; Refresh(); }
            },
                args.Length > 0 && (args[0] == "goto" || args[0] == "bring") ? ThingTeleportCommands.OperationId : ExtraPotions.Thing.Commands.RemoteAdmin.OperationId);
        }
        static void Add(string key, Action click, float x, float y, float width, int gridX, int gridY, bool mutation = true)
        {
            var button = KitButton.Create(content!, key, Ui(key), () => { if ((CanAdmin || CanConfigure && (key == "tab_server_config" || key == "close") || CanPeople && (key == "close" || tab == 0)) && (!mutation || !ThingPanelJobs.Busy)) click(); }, new Vector2(x, -y), new Vector2(width, 40));
            Tip(button.gameObject, key);
            Register(button, gridX, gridY); if (mutation) actions.Add(button);
        }
        internal static void Tip(GameObject control, string key)
        {
            string hint;
            switch(key)
            {
                case "players": case "permitted": case "admins": case "grants": hint="help_identity_lists";break;
                case "ledger": case "audit": hint="help_moderation_views";break;
                case "delve_actions": case "delve_due": case "delve_nearest": case "delve_nature": case "delve_kinds": hint="help_delve_views";break;
                case "delve_request": hint="delve_request_warning";break;
                case "delve_dryrun": hint="delve_dryrun_hint";break;
                case "delve_reset_nearest": hint="delve_reset_nearest_warning";break;
                case "regenerate_nature": hint="regenerate_nature_warning";break;
                case "reset_resources": hint="reset_resources_warning";break;
                case "pickup_radius": hint="pickup_radius_hint";break;
                case "tab_server_config": hint="config_hint";break;
                case "clear": hint="clear_announcements_warning";break;
                case "grant_subadmin": hint="help_grant_subadmin";break;
                case "subadmin_revoke_self": hint="subadmin_revoke_self_hint";break;
                default: hint="help_"+key;break;
            }
            KitTooltip.Set(control, Ui(key), Ui(hint));
        }
        static void Register(Selectable control, int x, int y) { controls.Add(control); window!.Focusable(control, x, y); }
        internal static void Message(string text) { if (status != null) status.text = text; }
        static void ClearContent()
        {
            list?.Dispose(); list = null;
            foreach (var control in controls) window?.Unfocusable(control);
            controls.Clear(); actions.Clear(); status = null;
            if (content != null) KitUnity.Retire(content.gameObject); content = null;
        }
        static void ResetWindow()
        {
            var active = KitAlmanacDialog.Active;
            if (active != null && window != null && active.Panel != null && active.Panel.IsChildOf(window.Panel)) active.Dispose();
            AskingSelf = false;
            shownFor = ""; shownReady = false; grantAsk = "";
            ThingInventoryWindow.Close();
            ThingPanelJobs.Reset(); ThingPanelWorld.Reset(); ThingPanelMessages.Reset(); ThingPanelConfig.Reset();
            ClearContent(); var old = window; window = null; title = null; page = null; selected = "";
            if (old != null) { old.Closed -= ResetWindow; old.Close(); old.Destroy(); }
        }
        public static void Shutdown() { AskingSelf = false; ThingSelf.Clear(); ResetWindow(); ThingPanelMenu.Reset(); }

        static void DrawPlayerActions()
        {
            var y = CanEditGrants ? 330f : 390f;
            if (CanEditGrants)
            {
                Add("kick", () => Moderate("kick"), 220, y, 170, 1, 20);
                Add("ban", () => Moderate("ban"), 410, y, 170, 2, 20);
                Add("tempban", () => Moderate("tempban"), 600, y, 170, 3, 20);
                Add("warn", () => Moderate("warn"), 790, y, 190, 4, 20);
                Add("mute", () => Moderate("mute"), 220, y + 46, 170, 1, 21);
                Add("unmute", () => { var target = SelectedPlayer(); if (target == null) return; Send(new[] { "unmute", target }); }, 410, y + 46, 170, 2, 21);
            }
            if (Holds(AdminRight.Teleport) && ActionCatalog.Visible(ThingAction.TeleportTo, ThingModules.StepAside.AdminToolLoaded))
            {
                Add("goto", () => Teleport("goto"), 600, y + 46, 170, 3, 21);
                Add("bring", () => Teleport("bring"), 790, y + 46, 190, 4, 21);
            }
            if (Holds(AdminRight.Freeze))
            {
                Add("freeze_player", () => { var target = SelectedPlayer(); if (target == null) return; Send(new[] { "freeze", target }); }, 220, y + 92, 170, 1, 22);
                Add("unfreeze_player", () => { var target = SelectedPlayer(); if (target == null) return; Send(new[] { "unfreeze", target }); }, 410, y + 92, 170, 2, 22);
            }
            if (CanViewInventory)
                Add("inventory", () => { var target = Selected; if (target == null || target.Kind != "player") { Message(Ui("select_player")); return; } ThingInventoryWindow.Open(target.Id, target.Caption); }, 600, y + 92, 360, 3, 22);
            if (CanEditGrants) DrawGrantToggles(y + 146);
            else if ((ThingSelf.Held & AdminRight.SubAdmin) != 0)
                Add("subadmin_revoke_self", () => ThingPanelForms.Confirm(window!, Ui("subadmin_revoke_self_confirm"), () => Send(new[] { "grant", "self", "subadmin", "off" })), 220, y + 146, 350, 1, 23);
        }

        static void DrawGrantPage()
        {
            var field = KitTextField.Create(content!, "GrantId", KitFieldMode.Text, new Vector2(220, -390), new Vector2(360, 40), keyboardTitle: Ui("platform_id"));
            if (field != null)
            {
                window!.Focusable(field.Input, 1, 20);
                controls.Add(field.Input);
                KitTooltip.Set(field.Input.gameObject, Ui("grant_add"), Ui("help_grant_add"));
            }
            Add("grant_add", () =>
            {
                var id = field == null ? "" : field.Text.Trim();
                if (!GameListIdentity.Valid(id)) { Message(Ui("help_grant_add")); return; }
                Send(new[] { "grant", id, "add" });
            }, 600, 390, 170, 2, 20);
            Add("grant_remove", () =>
            {
                var target = Selected; if (target == null || target.Kind != "grant") { Message(Ui("permissions_empty")); return; }
                ThingPanelForms.Confirm(window!, Ui("help_grant_remove") + "\n" + target.Id, () => Send(new[] { "grant", target.Id, "remove" }));
            }, 790, 390, 170, 3, 20);
            DrawGrantToggles(450);
        }

        static void DrawGrantToggles(float y)
        {
            var target = Selected;
            if (!CanEditGrants) return;
            if (target == null || target.Kind != "player" && target.Kind != "grant")
            { KitAlmanacDialog.Label(content!, "GrantEmpty", Ui("permissions_empty"), new Vector2(220, -y), new Vector2(740, 30), 14); return; }
            if (!shownReady || shownFor != target.Id)
            {
                KitAlmanacDialog.Label(content!, "GrantWait", Ui(shownFor == target.Id && !shownReady && grantAsk.Length == 0 ? "permissions_failed" : "permissions_loading"), new Vector2(220, -y), new Vector2(740, 30), 14);
                EnsureGrant(target.Id);
                return;
            }

            var id = target.Id;
            var isSubAdmin = (shown & AdminRight.SubAdmin) != 0;
            var subadminToggle = KitToggle.Create(content!, "Grant_subadmin", isSubAdmin, value =>
            {
                if (!CanEditGrants || ThingPanelJobs.Busy || !shownReady || shownFor != id || !GrantReply.IsCurrent(id, Selected?.Id ?? "")) return;
                if (!value)
                {
                    ThingPanelJobs.Start(new[] { "grant", id, "subadmin", "off" }, lines =>
                    {
                        if (RememberGrant(id, lines)) Draw(false);
                        else if (GrantReply.IsCurrent(id, Selected?.Id ?? "")) GrantFailed(id);
                    }, failure: () => { if (GrantReply.IsCurrent(id, Selected?.Id ?? "")) GrantFailed(id); });
                }
                else
                {
                    var identity = target.Caption + "\n" + id;
                    var confirm1 = string.Format(Ui("subadmin_confirm_1"), identity);
                    var confirm2 = string.Format(Ui("subadmin_confirm_2"), identity);
                    ThingPanelForms.Confirm(window!, confirm1, () =>
                    {
                        ThingPanelForms.Confirm(window!, confirm2, () =>
                        {
                            ThingPanelJobs.Start(new[] { "grant", id, "subadmin", "on" }, lines =>
                            {
                                if (RememberGrant(id, lines)) Draw(false);
                                else if (GrantReply.IsCurrent(id, Selected?.Id ?? "")) GrantFailed(id);
                            }, failure: () => { if (GrantReply.IsCurrent(id, Selected?.Id ?? "")) GrantFailed(id); });
                        });
                    });
                }
            }, new Vector2(220, -y), new Vector2(350, 36), Ui("grant_subadmin"), Ui("grant_subadmin"));
            if (subadminToggle != null)
            {
                subadminToggle.Tooltip(Ui("grant_subadmin"), Ui("help_grant_subadmin"));
                Register(subadminToggle.Button, 1, 29);
                actions.Add(subadminToggle.Button);
            }
            y += 42f;

            var bits = new[] { AdminRight.World, AdminRight.Spawn, AdminRight.Teleport, AdminRight.Freeze, AdminRight.Inventory, AdminRight.Ward };
            var names = new[] { "world", "spawn", "teleport", "freeze", "inventory", "ward" };
            for (var i = 0; i < bits.Length; i++)
            {
                var bit = bits[i]; var name = names[i];
                var toggle = KitToggle.Create(content!, "Grant_" + name, (shown & bit) == bit, value =>
                {
                    if (!CanEditGrants || ThingPanelJobs.Busy || !shownReady || shownFor != id || !GrantReply.IsCurrent(id, Selected?.Id ?? "")) return;
                    ThingPanelJobs.Start(new[] { "grant", id, name, value ? "on" : "off" }, lines =>
                    {
                        if (RememberGrant(id, lines)) Draw(false);
                        else if (GrantReply.IsCurrent(id, Selected?.Id ?? "")) GrantFailed(id);
                    }, failure: () => { if (GrantReply.IsCurrent(id, Selected?.Id ?? "")) GrantFailed(id); });
                }, new Vector2(220 + (i % 3) * 250, -(y + (i / 3) * 46)), new Vector2(230, 36), Ui("grant_" + name), Ui("grant_" + name));
                if (toggle == null) continue;
                toggle.Tooltip(Ui("grant_" + name), Ui("help_grant_" + name));
                Register(toggle.Button, 1 + (i % 3), 30 + i / 3);
                actions.Add(toggle.Button);
            }
        }

        static void EnsureGrant(string id)
        {
            if (!CanEditGrants || id.Length == 0 || shownFor == id || grantAsk == id || ThingPanelJobs.Busy) return;
            grantAsk = id;
            ThingPanelJobs.Start(new[] { "grant", "get", id }, lines =>
            {
                grantAsk = "";
                if (!GrantReply.IsCurrent(id, Selected?.Id ?? "")) return;
                if (!RememberGrant(id, lines)) GrantFailed(id);
                else if (window != null && KitAlmanacDialog.Active == null) Draw(false);
            }, failure: () => { grantAsk = ""; if (GrantReply.IsCurrent(id, Selected?.Id ?? "")) GrantFailed(id); });
        }

        static void GrantFailed(string id)
        {
            shownFor = id; shownReady = false;
            if (window != null && KitAlmanacDialog.Active == null) Draw(false);
            Message(Ui("permissions_failed"));
        }

        static bool RememberGrant(string id, IReadOnlyList<string> lines)
        {
            if (!GrantReply.TryRead(id, Selected?.Id ?? "", lines, out var set)) return false;
            shownFor = id; shown = set; shownReady = true;
            return true;
        }

        static string SelectedPlayer()
        {
            var target = Selected;
            if (target == null || target.Kind != "player") { Message(Ui("select_player")); return null; }
            return target.Id;
        }
    }

    internal static class ThingSelf
    {
        internal static long Generation = -1;
        internal static bool Known, FullAdmin;
        internal static AdminRight Held;
        internal static void Apply(long generation, string line)
        {
            if (Generation != generation) { Generation = generation; Known = false; FullAdmin = false; Held = AdminRight.None; }
            if (!AdminPermissions.TryWire(line ?? "", out var full, out var set)) return;
            Known = true; FullAdmin = full; Held = full ? AdminRight.All : set;
        }
        internal static void Clear() { Generation = -1; Known = false; FullAdmin = false; Held = AdminRight.None; }
    }
}
