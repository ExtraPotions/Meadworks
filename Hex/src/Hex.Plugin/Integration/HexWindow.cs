// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original Hex presentation over the existing Crucible.UI kit and qualified Cairn virtual-row ownership pattern.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Hex.Runtime.Native;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Hex.Runtime;

internal static partial class HexWindow
{
    static KitWindow? window;
    static KitVirtualList? list;
    static KitToggle? master;
    static Button? close, panic;
    static float nextSkills;
    static string skillShape = "";
    static TMP_Text? hint;
    static string tab = "cheats", language = "";
    static long revision;
    static bool dirty, focusTabAfterDraw;
    static Player? actor;
    static long session = -1;
    static readonly List<Button> tabs = new();
    static readonly List<string> keys = new();
    static readonly Dictionary<RectTransform, List<Action>> cleanup = new();
    static readonly Dictionary<RectTransform, Action> updates = new();
    /// <summary>The game's button art insets the caption to about half the button; translated captions such as "Restablecer" and "Réinitialiser" were cut to an ellipsis, so the caption fills the whole button.</summary>
    /// <summary>The caption's natural width at its design size (TMP measures auto-sized text at its largest size).</summary>
    static float CaptionWidth(Button button)
    {
        var text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        return text == null ? 0f : text.GetPreferredValues(text.text).x;
    }
    static void Roomy(Button button)
    {
        var text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null) return;
        var rect = text.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    static string Ui(string key) => HexText.Text("ui_" + key);
    static string Label(string key) => HexText.Text("label_" + key);
    internal static void Tick()
    {
        if (!Plugin.Ready || !Plugin.Active || !MeadworksHeadless.AllowsUi || Player.m_localPlayer == null)
        { Shutdown(); return; }
        if (window != null && (actor != Player.m_localPlayer || session != CruciblePlugin.Session.Generation)) Shutdown();
        if (MeadworksInput.Pressed(Plugin.Settings!.MenuKey, window?.IsOpen == true ? InputContext.Window : InputContext.Gameplay))
        { if (window?.IsOpen == true) window.Close(); else Open(); }
        if (window?.IsOpen != true) return;
        master?.SetValue(Plugin.Settings!.Enabled.Value);
        if (language != MeadworksBlockedOptions.Language) { language = MeadworksBlockedOptions.Language; dirty = true; }
        if (tab == "skills" && Time.unscaledTime >= nextSkills)
        {
            nextSkills = Time.unscaledTime + 1;
            var shape = MeadworksSkills.Registry.Revision + ":" + string.Join(",", Player.m_localPlayer.GetSkills().m_skills.Where(def => def != null).Select(def => (int)def.m_skill));
            if (shape != skillShape) { skillShape = shape; dirty = true; }
        }
        PadTabs();
        if (dirty) Draw();
        TickCatalog();
        list?.Tick();
        foreach (var update in updates.Values.ToArray()) update();
    }
    internal static void Open()
    {
        if (!Plugin.Ready || !Plugin.Active || !MeadworksHeadless.AllowsUi || Player.m_localPlayer == null || KitCanvas.Font == null) return;
        if (window?.Panel == null)
        {
            Shutdown();
            actor = Player.m_localPlayer; session = CruciblePlugin.Session.Generation;
            window = KitWindow.Create("Hex", new Vector2(980, 680));
            if (window == null) return;
            var heading = KitAlmanacDialog.Label(window.Panel, "Title", "Hex", new Vector2(20, -12), new Vector2(220, 32), 24);
            if (KitCanvas.Heading != null) heading.font = KitCanvas.Heading;
            master = KitToggle.Create(window.Panel, "Master", Plugin.Settings!.Enabled.Value, value => MeadworksConfig.Amend(Plugin.Settings!.Enabled, value),
                new Vector2(270, -12), new Vector2(160, 32), Ui("on"), Ui("off"));
            master.Tooltip("Hex", HexText.Text("enabled"));
            window.Focusable(master.Button, 1, 0);
            panic = KitButton.Create(window.Panel, "Panic", Ui("panic"), () => Plugin.Settings!.PanicReset(), new Vector2(620, -12), new Vector2(150, 32));
            close = KitButton.Create(window.Panel, "Close", Ui("close"), () => window?.Close(), new Vector2(800, -12), new Vector2(150, 32));
            Help(panic.gameObject, Ui("panic"), HexText.Text("panic")); Help(close.gameObject, Ui("close"), HexText.Text("help_close"));
            window.Focusable(panic, 2, 0); window.Focusable(close, 3, 0);
            hint = KitAlmanacDialog.Label(window.Panel, "Hint", "", new Vector2(200, -620), new Vector2(750, 45), 14);
            window.Opened += () => dirty = true;
            window.Closed += CloseEditor;
        }
        window.Open(); dirty = true;
        if (master?.Button != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(master.Button.gameObject);
    }
    static void CloseEditor() { if (window != null && KitAlmanacDialog.Active?.Parent == window) KitAlmanacDialog.Active.Dispose(); }
    static void Result(string value) { if (hint != null) hint.text = value; }
    static void Select(string value) { CloseEditor(); tab = value; dirty = focusTabAfterDraw = true; Result(""); }
    static void Draw()
    {
        if (window == null || Plugin.Settings == null) return;
        dirty = false; ClearRows();
        foreach (var button in tabs) { window.Unfocusable(button); button.gameObject.SetActive(false); button.name = "retired"; UnityEngine.Object.Destroy(button.gameObject); }
        tabs.Clear();
        var strip = KitTabs.Place(window.Panel, HexMenu.Tabs.Select(id => new KitTab { Id = id, Caption = Ui(id), Click = () => Select(id) }).ToArray(), tab, 20, 60, 160);
        for (var i = 0; i < strip.Count; i++)
        {
            var button = strip[i]; KitAlmanacDialog.Place((RectTransform)button.transform, new Vector2(20, -60 - i * 48), new Vector2(160, 40));
            Help(button.gameObject, Ui(VisibleTabs()[i]), HexText.Format("help_tab", Ui(VisibleTabs()[i])));
            tabs.Add(button); window.Focusable(button, 0, i + 1);
        }
        list = KitVirtualList.Create(window.Panel, "HexRows", new Vector2(750, 550), parent =>
        { var node = new GameObject("HexRow", typeof(RectTransform)); node.transform.SetParent(parent, false); return (RectTransform)node.transform; });
        if (list == null) return;
        KitAlmanacDialog.Place(list.Root, new Vector2(200, -60), new Vector2(750, 550));
        var rows = new List<KitVirtualRow>();
        foreach (var key in HexMenu.Preferences[tab])
        {
            var name = key; var entry = Plugin.Settings.PreferenceEntries()[name];
            rows.Add(new KitVirtualRow(name, name == "debug_mode" ? 144 : name == "local_time_minute" ? 170 : entry.SettingType == typeof(int) ? 130 : 96, rect => BindPreference(rect, name, entry), Unbind));
        }
        if (tab == "cheats")
        {
            ActionRow(rows, "heal", HexText.Text("actions_heal"), () => HexActions.Execute(new[] { "heal" }));
            ActionRow(rows, "rested", HexText.Text("actions_rested"), () => HexActions.Execute(new[] { "rested" }));
            ActionRow(rows, "power", HexText.Text("actions_power"), () =>
            {
                KitAlmanacChoice.Open(window, HexText.Text("actions_power"), new[] { "GP_Eikthyr", "GP_TheElder", "GP_Bonemass", "GP_Moder", "GP_Yagluth", "GP_Queen", "GP_Ashlands", "GP_DeepNorth" },
                    Array.Empty<string>(), false, values => { if (values.Count == 1) Result(HexActions.Execute(new[] { "power", values[0] })); });
                return "";
            });
        }
        if (tab == "movement") ActionRow(rows, "reset_speeds", HexText.Text("speeds_reset_all"), () => Plugin.Settings.ResetSpeeds());
        if (tab == "map")
        {
            ActionRow(rows, "poi", HexText.Text("poi_actual"), HexPoi.Request);
            ActionRow(rows, "estimatepoi", HexText.Text("poi_estimate"), HexPoi.RequestEstimate);
            ActionRow(rows, "clearpoi", HexText.Text("poi_clear"), HexPoi.Clear);
            ActionRow(rows, "reveal", HexText.Text("map_reveal"), () => HexMap.Execute(new[] { "map", "reveal" }));
            ActionRow(rows, "reset", HexText.Text("map_reset"), ConfirmMapReset);
            ActionRow(rows, "seed", HexText.Text("seed_label"), HexInformation.Seed);
        }
        if (tab == "debug")
        {
            SpawnProtectionRow(rows);
            foreach (var action in new[] { "fly", "build", "kill", "drops" })
            {
                var name = action;
                ActionRow(rows, "debug_" + name, HexText.Text("debug_" + name), () => DebugAction(name));
            }
        }
        if (tab == "skills")
        {
            if (HexSkills.Catalog(out var skills, out var reason))
                foreach (var skill in skills) { var row = skill; rows.Add(new KitVirtualRow("skill:" + (int)row.Type, 100, rect => BindSkill(rect, row), Unbind)); }
            else Result(reason);
        }
        if (tab == "presets") PresetRows(rows);
        keys.Clear(); keys.AddRange(rows.Select(row => row.Key));
        list.SetRows(rows, ++revision);
        if (CatalogTab) CatalogHeader();
        master?.SetLabels(Ui("on"), Ui("off")); master?.Tooltip("Hex", HexText.Text("enabled"));
        if (close != null) Help(close.gameObject, Ui("close"), HexText.Text("help_close"));
        if (panic != null) Help(panic.gameObject, Ui("panic"), HexText.Text("panic"));
        if (close != null) GameButtons.Caption(close.gameObject, Ui("close"));
        if (panic != null) GameButtons.Caption(panic.gameObject, Ui("panic"));
        if (focusTabAfterDraw && EventSystem.current != null)
        {
            focusTabAfterDraw = false;
            var index = Array.IndexOf(VisibleTabs(), tab);
            if (KitAlmanacDialog.Active == null && index >= 0 && index < tabs.Count) EventSystem.current.SetSelectedGameObject(tabs[index].gameObject);
        }
    }
    static string[] VisibleTabs() => HexMenu.Tabs.ToArray();
    static void PadTabs()
    {
        if (window?.IsOpen != true || !MeadworksInput.Allows(InputContext.Window) || MeadworksInput.Typing
            || KitAlmanacDialog.Active != null) return;
        var selected = EventSystem.current?.currentSelectedGameObject;
        if (selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(window.Panel)) return;
        var parent = window.Panel.parent;
        for (var i = window.Panel.GetSiblingIndex() + 1; parent != null && i < parent.childCount; i++)
            if (parent.GetChild(i).gameObject.activeInHierarchy) return;
        var visible = VisibleTabs(); var index = Array.IndexOf(visible, tab);
        if (index < 0 || visible.Length == 0) return;
        if (MeadworksInput.PadDown(PadButton.LeftBumper))
        { MeadworksInput.PadConsume(PadButton.LeftBumper); Select(visible[(index + visible.Length - 1) % visible.Length]); }
        else if (MeadworksInput.PadDown(PadButton.RightBumper))
        { MeadworksInput.PadConsume(PadButton.RightBumper); Select(visible[(index + 1) % visible.Length]); }
    }
    static void BindPreference(RectTransform rect, string key, ConfigEntryBase entry)
    {
        cleanup[rect] = new List<Action>();
        var description = KitAlmanacDialog.Label(rect, "Description", HexText.Text(key), new Vector2(0, -46), new Vector2(700, 44), 12);
        var label = KitAlmanacDialog.Label(rect, "Label", Label(key), Vector2.zero, new Vector2(350, 36), 16);
        string Reason() { var category = Plugin.Settings!.Category(key); return (key.StartsWith("achievements_", StringComparison.Ordinal) || key.StartsWith("clear_", StringComparison.Ordinal)) && !HexAchievements.Available ? Ui("achievements_sync") : category.HasValue && !Plugin.Settings!.MayUse(key) ? MeadworksBlockedOptions.Reason(MeadworksMischief.Decide(Plugin.Guid, category.Value).Reason) : ""; }
        Func<bool> allowed = () => Plugin.Settings!.Enabled.Value && Plugin.Settings.MayUse(key)
            && (!key.StartsWith("achievements_", StringComparison.Ordinal) && !key.StartsWith("clear_", StringComparison.Ordinal) || HexAchievements.Available);
        void Explain()
        {
            var category = Plugin.Settings!.Category(key);
            description.text = (key.StartsWith("achievements_", StringComparison.Ordinal) || key.StartsWith("clear_", StringComparison.Ordinal)) && !HexAchievements.Available ? Ui("achievements_sync") : category.HasValue && !Plugin.Settings!.MayUse(key)
                ? MeadworksBlockedOptions.Reason(MeadworksMischief.Decide(Plugin.Guid, category.Value).Reason) : HexText.Text(key);
        }
        var reset = KitButton.Create(rect, "Reset", Ui("reset"), () => Plugin.Settings!.SetPreference(key, Plugin.Settings.SafeValue(key)), new Vector2(640, 0), new Vector2(68, 32));
        Roomy(reset); Help(reset.gameObject, Ui("reset"), HexText.Format("help_reset", Label(key)) + "\n\n" + HexText.Text(key));
        var resetSpan = HexMenu.ResetSpan(CaptionWidth(reset));
        KitAlmanacDialog.Place((RectTransform)reset.transform, new Vector2(resetSpan.X, 0), new Vector2(resetSpan.Width, 32));
        Action refresh;
        if (entry.SettingType == typeof(bool))
        {
            var toggle = KitToggle.Create(rect, "Value", (bool)entry.BoxedValue, value => { Plugin.Settings!.SetPreference(key, value); if (key == "skill_editor") dirty = true; }, new Vector2(HexMenu.ValueX, 0), new Vector2(HexMenu.ValueWidth(200, resetSpan.X), 32), Ui("on"), Ui("off"));
            cleanup[rect].Add(toggle.Dispose); toggle.Tooltip(Label(key), HexText.Text(key));
            refresh = () => { toggle.SetValue((bool)entry.BoxedValue); toggle.SetUsable(allowed(), Reason()); Explain(); };
        }
        else
        {
            var range = (AcceptableValueRange<int>)entry.Description.AcceptableValues;
            string Current() => Convert.ToString(entry.BoxedValue, CultureInfo.InvariantCulture)!;
            bool Valid(string text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && range.IsValid(value);
            var number = KitAlmanacNumber.Create(rect, "Value", new Vector2(HexMenu.ValueX, 0), new Vector2(HexMenu.ValueWidth(250, resetSpan.X), 75), Current(), Valid,
                text => { if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) Plugin.Settings!.SetPreference(key, value); },
                direction => ((int)entry.BoxedValue + direction).ToString(CultureInfo.InvariantCulture), range.MinValue, range.MaxValue, 1,
                serialize: value => Math.Round(value).ToString(CultureInfo.InvariantCulture));
            description.rectTransform.anchoredPosition = new Vector2(0, -80);
            cleanup[rect].Add(number.Dispose);
            refresh = () => { number.SetUsable(allowed()); number.Set(Current()); NumberHelp(number, Label(key), HexText.Text(key), Reason()); Explain(); };
        }
        description.rectTransform.sizeDelta = new Vector2(700, key == "debug_mode" ? 88 : 44);
        if (key == "local_time_minute") refresh += TimePresetButtons(rect);
        var authorityLock = LockBadge(rect, new Vector2(352, -8));
        var update = refresh;
        updates[rect] = () => { update(); Help(label.gameObject, Label(key), HexText.Text(key), Reason()); var category = Plugin.Settings!.Category(key); authorityLock.gameObject.SetActive(category.HasValue && !Plugin.Settings!.MayUse(key) || (key.StartsWith("achievements_", StringComparison.Ordinal) || key.StartsWith("clear_", StringComparison.Ordinal)) && !HexAchievements.Available); };
        updates[rect](); Focus(rect, key);
    }
    /// <summary>The Midnight/Sunrise/Noon/Sunset buttons inside the Local Time Minute row; returns their refresh.</summary>
    static Action TimePresetButtons(RectTransform rect)
    {
        var buttons = new List<(Button Button, string Id, string Title)>();
        for (var i = 0; i < HexMenu.TimePresets.Count; i++)
        {
            var preset = HexMenu.TimePresets[i]; var title = HexText.Text("time_" + preset.Id);
            var button = KitButton.Create(rect, "Time" + i, title,
                () => Result(Plugin.Settings!.SetPreference("local_time_minute", preset.Minute) ? HexText.Text("time_applied") : HexText.Text("actions_denied")),
                new Vector2(i * 177f, -128), new Vector2(169, 34));
            Roomy(button); buttons.Add((button, "time_" + preset.Id, title));
        }
        return () =>
        {
            foreach (var item in buttons)
            {
                var decision = KitButton.Block(item.Button, MeadworksMischief.Query(Plugin.Guid, item.Id, MischiefCategory.Personal),
                    Plugin.Settings!.Enabled.Value && HexMovement.Owned(Player.m_localPlayer));
                Help(item.Button.gameObject, item.Title, HexText.Text("local_time_minute"), decision.Tooltip);
            }
        };
    }
    static void BindSkill(RectTransform rect, HexSkills.Row row)
    {
        cleanup[rect] = new List<Action>();
        KitAlmanacDialog.Label(rect, "Label", row.Name, Vector2.zero, new Vector2(350, 40), 16);
        float Current() => Player.m_localPlayer != null && Player.m_localPlayer.GetSkills().m_skillData.TryGetValue(row.Type, out var progress) ? progress.m_level : 0f;
        var number = KitAlmanacNumber.Create(rect, "Level", new Vector2(380, 0), new Vector2(280, 75), Current().ToString("0.###", CultureInfo.InvariantCulture),
            text => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && !float.IsNaN(value) && value >= 0 && value <= 100,
            text => { if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) Result(HexSkills.Set(((int)row.Type).ToString(CultureInfo.InvariantCulture), value)); },
            direction => (Current() + direction).ToString("0.###", CultureInfo.InvariantCulture), 0, 100, 1, serialize: value => value.ToString("0.###", CultureInfo.InvariantCulture));
        cleanup[rect].Add(number.Dispose); NumberHelp(number, row.Name, HexText.Text("skill_editor"));
        updates[rect] = () => { number.Set(Current().ToString("0.###", CultureInfo.InvariantCulture)); number.SetUsable(Plugin.Settings!.Enabled.Value && Plugin.Settings.SkillEditor.Value && HexMovement.Owned(Player.m_localPlayer) && MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.Progress).Allowed); NumberHelp(number, row.Name, HexText.Text("skill_editor"), MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.Progress).Allowed ? "" : MeadworksBlockedOptions.Reason(MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.Progress).Reason)); };
        updates[rect](); Focus(rect, "skill:" + (int)row.Type);
    }
    internal static string DebugAction(string action)
    {
        var native = ZNet.instance != null && ZNet.instance.IsServer();
        if (action != "kill" && action != "drops") return HexDebug.Execute(action);
        if (!HexDebug.Tools) return HexText.Text("debug_unavailable");
        if (KitAlmanacDialog.Active != null) return HexText.Text("actions_unavailable");
        Open();
        var capturedActor = actor; var capturedSession = session; var capturedNet = ZNet.instance;
        KitAlmanacTextEditor.Confirm(window, HexText.Text("debug_" + action), HexText.Text(native ? "debug_confirm_" + action + "_body" : "debug_confirm_remote_" + action + "_body"), () =>
        {
            if (actor == capturedActor && capturedActor == Player.m_localPlayer && session == capturedSession
                && capturedSession == CruciblePlugin.Session.Generation && capturedNet == ZNet.instance)
                Result(HexDebug.Execute(action, true));
        });
        return "";
    }
    internal static string ConfirmMapReset()
    {
        if (!HexMap.PrepareReset(out var reason)) return reason;
        if (KitAlmanacDialog.Active != null) return HexText.Text("actions_unavailable");
        Open();
        if (window == null) return HexText.Text("actions_unavailable");
        var currentActor = Player.m_localPlayer; var currentNet = ZNet.instance;
        var generation = CruciblePlugin.Session.Generation; var currentMap = Minimap.instance;
        KitAlmanacTextEditor.Confirm(window, HexText.Text("map_reset"), HexText.Text("map_confirm_reset"), () =>
        {
            if (currentActor != Player.m_localPlayer || currentNet != ZNet.instance || generation != CruciblePlugin.Session.Generation || currentMap != Minimap.instance)
            { Result(HexText.Text("map_unavailable")); return; }
            Result(HexMap.Execute(new[] { "map", "reset", "confirm" }));
        });
        return "";
    }
    static void ActionRow(List<KitVirtualRow> rows, string id, string title, Func<string> action)
    {
        rows.Add(new KitVirtualRow("action:" + id, 44, rect =>
        {
            cleanup[rect] = new List<Action>();
            var button = KitButton.Create(rect, "Action", title, () => Result(action()), Vector2.zero, new Vector2(700, 34));
            Help(button.gameObject, title, ActionHelp(id));
            MischiefCategory? category = id == "debug_build" ? MischiefCategory.Economy : id == "debug_kill" || id == "debug_drops" ? MischiefCategory.World
                : id == "debug_fly" || id.StartsWith("time_", StringComparison.Ordinal) ? MischiefCategory.Personal : id == "power" ? MischiefCategory.Progress : id == "heal" || id == "rested" ? MischiefCategory.Personal
                : id == "reveal" || id == "reset" || id == "seed" || id == "poi" || id == "estimatepoi" ? MischiefCategory.Information : (MischiefCategory?)null;
            if (category.HasValue) updates[rect] = () =>
            {
                var page = id.StartsWith("debug_", StringComparison.Ordinal) ? HexDebug.Tools : id == "seed" ? HexInformation.SeedPage : id == "reveal" || id == "reset" || id == "poi" || id == "estimatepoi" ? Plugin.Settings!.MapTools.Value : Plugin.Settings!.QuickActions.Value;
                var option = MeadworksMischief.Query(Plugin.Guid, id, category.Value);
                var ownerBlocked = id == "debug_build" && HexDebug.TimberInstalled;
                var decision = KitButton.Block(button, option, Plugin.Settings!.Enabled.Value && page && HexMovement.Owned(Player.m_localPlayer) && !ownerBlocked);
                Help(button.gameObject, title, ActionHelp(id), ownerBlocked && option.Allowed ? HexText.Text("debug_timbers_owner") : decision.Tooltip);
            };
            if (updates.TryGetValue(rect, out var update)) update();
            Focus(rect, "action:" + id);
        }, Unbind));
    }
    static void PresetRows(List<KitVirtualRow> rows)
    {
        ActionRow(rows, "savepreset", Ui("save_preset"), () =>
        {
            var dialog = KitAlmanacDialog.Create(window, Ui("save_preset"), new Vector2(500, 180));
            if (dialog == null) return "";
            var field = KitTextField.Create(dialog.Panel, "Name", KitFieldMode.Text, new Vector2(20, -60), new Vector2(460, 32));
            Help(field.Input.gameObject, Ui("save_preset"), HexText.Text("preset_name_invalid"));
            field.Text = HexPresets.Selected; field.Validator = HexPresetJson.NameValid; dialog.Own(field.Destroy);
            var save = KitButton.Create(dialog.Panel, "Save", Ui("save_preset"), () =>
            { if (!HexPresetJson.NameValid(field.Text)) return; var name = field.Text; dialog.Dispose(); Result(HexPresets.Execute(new[] { "preset", "save", name })); dirty = true; }, new Vector2(280, -120), new Vector2(200, 32));
            Help(save.gameObject, Ui("save_preset"), HexText.Text("help_savepreset"));
            dialog.Window.Focusable(field.Input, 0, 0); dialog.Window.Focusable(save, 0, 1);
            return "";
        });
        ActionRow(rows, "reloadpresets", Ui("reload"), () => { var result = HexPresets.Execute(new[] { "preset", "reload" }); dirty = true; return result; });
        foreach (var name in HexPresets.Names)
        {
            var selected = name;
            rows.Add(new KitVirtualRow("preset:" + selected, 48, rect =>
            {
                cleanup[rect] = new List<Action>();
                var load = KitButton.Create(rect, "Load", selected + (selected.Equals(HexPresets.Selected, StringComparison.OrdinalIgnoreCase) ? " *" : ""), () => { Result(HexPresets.Execute(new[] { "preset", "load", selected })); dirty = true; }, Vector2.zero, new Vector2(520, 34));
                var delete = KitButton.Create(rect, "Delete", Ui("delete"), () => KitAlmanacTextEditor.Confirm(window, Ui("delete"), selected,
                    () => { Result(HexPresets.Execute(new[] { "preset", "delete", selected })); dirty = true; }), new Vector2(540, 0), new Vector2(160, 34));
                Help(load.gameObject, selected, HexText.Text("help_loadpreset")); Help(delete.gameObject, Ui("delete"), HexText.Text("help_deletepreset"));
                foreach (var text in rect.GetComponentsInChildren<TMP_Text>(true)) text.richText = false;
                Focus(rect, "preset:" + selected);
            }, Unbind));
        }
    }
    static void Focus(RectTransform rect, string key)
    {
        var index = keys.IndexOf(key);
        if (index < 0) return;
        var column = 1;
        foreach (var control in rect.GetComponentsInChildren<Selectable>(true)) window!.Focusable(control, column++, index + 1);
    }
    static void Unbind(RectTransform rect)
    {
        foreach (var control in rect.GetComponentsInChildren<Selectable>(true)) window?.Unfocusable(control);
        if (cleanup.TryGetValue(rect, out var owned)) foreach (var release in owned) release();
        cleanup.Remove(rect); updates.Remove(rect);
        foreach (Transform child in rect) { child.gameObject.SetActive(false); child.name = "retired"; UnityEngine.Object.Destroy(child.gameObject); }
    }
    static void ClearRows() { ClearCatalog(); list?.Dispose(); list = null; foreach (var owned in cleanup.Values.ToArray()) foreach (var release in owned) release(); updates.Clear(); cleanup.Clear(); keys.Clear(); }
    internal static void Shutdown()
    {
        CloseEditor(); ClearRows(); master?.Dispose(); master = null;
        window?.Close(); window?.Destroy(); window = null; hint = null; tabs.Clear(); dirty = focusTabAfterDraw = false; language = ""; actor = null; session = -1; close = panic = null; nextSkills = 0; skillShape = "";
    }
    [HarmonyPatch(typeof(KitWindow), "Pad")]
    static class PadRows
    {
        static bool Prefix(KitWindow __instance, PadButton button)
        {
            if (__instance != window || window?.IsOpen != true || MeadworksInput.Typing || KitAlmanacDialog.Active != null
                || list == null || button != PadButton.DPadUp && button != PadButton.DPadDown) return true;
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == null || !selected.transform.IsChildOf(list.Root)) return true;
            list.Tick(); var at = list.FocusedKey != null ? keys.IndexOf(list.FocusedKey) : -1;
            var next = at + (button == PadButton.DPadDown ? 1 : -1);
            if (at < 0 || next < 0 || next >= keys.Count) return true;
            MeadworksInput.PadConsume(button); if (CatalogTab) PreserveCatalogColumn(selected, keys[next]); else list.Focus(keys[next]); return false;
        }
    }
}
