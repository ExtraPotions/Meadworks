// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room from Cairn's agreed marker behavior. Uses the existing kit window, virtual rows,
// switches and CA07a map. No TimeWeatherHud code or art used. Cairn's settings are edited in Almanac; this window
// keeps only the compass markers tool (the type name and the "tab" field stay for the test bridge).
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Hud;
using ExtraPotions.Cairn.Integration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using HarmonyLib;

namespace ExtraPotions.Cairn.Runtime
{
    public static class CairnSettingsWindow
    {
        static KitWindow? window;
        static KitVirtualList? list;
        static CairnMarkerMap? map;
        static TMP_Text? hint;
#pragma warning disable CS0414 // always 0 (Markers); read by the test bridge
        static int tab;
#pragma warning restore CS0414
        static long revision;
        static bool dirty;
        static float nextRefresh;
        static string language = "";
        static string markerFingerprint = "";
        static bool mapFocus;
        static readonly List<string> rowKeys = new List<string>();
        static readonly Dictionary<RectTransform, List<Action>> cleanup = new Dictionary<RectTransform, List<Action>>();
        static readonly Dictionary<RectTransform, Action> updates = new Dictionary<RectTransform, Action>();

        public static bool IsOpen => window?.IsOpen == true;

        public static void Tick()
        {
            var settings = Plugin.Settings;
            if (!Plugin.Ready || !Plugin.AllowsUi || settings == null || Player.m_localPlayer == null)
            {
                window?.Close();
                return;
            }
            var context = IsOpen ? InputContext.Window : InputContext.Gameplay;
            if (!CairnModules.StepAside.SteppedAside(CairnModule.Hud) && MeadworksInput.Allows(context) &&
                ((Enum.TryParse(settings.Get(CairnSettings.Hud.SettingsKey), true, out KeyCode key) && MeadworksInput.Pressed(new KeyboardShortcut(key), context))
                || CairnHud.PadChordPressed(settings.Get(CairnSettings.Hud.GamepadSettings))))
            {
                if (IsOpen) window!.Close(); else Open();
            }
            if (!IsOpen) return;
            if (language != MeadworksBlockedOptions.Language) { language = MeadworksBlockedOptions.Language; dirty = true; }
            if (dirty) Draw();
            list?.Tick();
            Pad();
            foreach (var update in updates.Values.ToArray()) update();
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 1f;
                // Pin edits from the game are reflected while this window is open too.
                RefreshMarkers();
            }
        }

        public static void Open()
        {
            if (!Plugin.Ready || Plugin.Settings == null || !Plugin.AllowsUi || Player.m_localPlayer == null || !CrucibleUiPlugin.Active || KitCanvas.Font == null) return;
            if (window?.Panel == null)
            {
                Reset();
                window = KitWindow.Create("Cairn", new Vector2(1000, 700));
                if (window == null) return;
                var heading = KitAlmanacDialog.Label(window.Panel, "Title", "Cairn", new Vector2(20, -12), new Vector2(800, 32), 22);
                if (KitCanvas.Heading != null) heading.font = KitCanvas.Heading;
                var close = KitButton.Create(window.Panel, "Close", Ui("close"), () => window?.Close(), new Vector2(850, -12), new Vector2(130, 32));
                KitTooltip.Set(close.gameObject, Ui("close"), Ui("help_close"));
                window.Focusable(close, 5, 0);
                hint = KitAlmanacDialog.Label(window.Panel, "Hint", "", new Vector2(20, -650), new Vector2(950, 36), 14);
                window.Opened += () => dirty = true;
                window.Closed += () => { if (KitAlmanacDialog.Active?.Parent == window) KitAlmanacDialog.Active.Dispose(); };
            }
            window.Open();
            dirty = true;
        }

        static string Ui(string key) => CairnText.Text("ui_" + key);

        static void Draw()
        {
            if (window == null || Plugin.Settings == null) return;
            dirty = false;
            ClearContent();
            var title = window.Panel.Find("Title")?.GetComponent<TMP_Text>();
            if (title != null) title.text = "Cairn: " + Ui("menu_markers");
            if (CairnModules.StepAside.SteppedAside(CairnModule.Hud))
            {
                // The compass belongs to the mod Cairn stepped aside for; only the notice is shown.
                hint!.text = CairnText.Format(StepAsideNotice.KeyOf(CairnModule.Hud), string.Join(", ", CairnModules.StepAside.SourcesOf(CairnModule.Hud).Select(s => s.Name)));
                return;
            }
            hint!.text = Ui("markers_hint");
            const int width = 360;
            list = KitVirtualList.Create(window.Panel, "Cairn_SettingsRows", new Vector2(width, 540), parent =>
            {
                var go = new GameObject("Cairn_SettingRow", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                return (RectTransform)go.transform;
            });
            if (list == null) return;
            KitAlmanacDialog.Place(list.Root, new Vector2(20, -100), new Vector2(width, 540));
            var rows = new List<KitVirtualRow>();
            var cursor = KitButton.Create(window.Panel, "MapCursor", Ui("map_cursor"), () => mapFocus = true, new Vector2(790, -650), new Vector2(190, 32));
            KitTooltip.Set(cursor.gameObject, Ui("map_cursor"), Ui("help_map_cursor"));
            window.Focusable(cursor, 1, 1);
            map = CairnMarkerMap.Create(window.Panel, new Vector2(395, -100), new Vector2(585, 540));
            if (map != null)
            {
                map.OnChanged = _ => RefreshMarkers();
                map.OnShownByType = type => hint!.text = CairnText.Format("ui_shown_by_type", type);
                var plan = map.Refresh();
                if (plan != null) BuildMarkers(rows, plan);
            }
            rowKeys.Clear(); rowKeys.AddRange(rows.Select(r => r.Key));
            list.SetRows(rows, ++revision);
        }

        static void RefreshMarkers()
        {
            if (map == null || list == null) return;
            var plan = map.Refresh();
            if (plan == null) return;
            var fingerprint = string.Join("\n", plan.Types.Select(t => t.Entry + ":" + t.Count + ":" + t.Chosen))
                + string.Join("\n", plan.Pins.Select(p => p.Entry + ":" + p.Pin.Name + ":" + p.Shown));
            if (fingerprint == markerFingerprint) return;
            markerFingerprint = fingerprint;
            var rows = new List<KitVirtualRow>();
            BuildMarkers(rows, plan);
            rowKeys.Clear(); rowKeys.AddRange(rows.Select(r => r.Key));
            list.SetRows(rows, ++revision);
        }

        static void Pad()
        {
            if (!MeadworksInput.GamepadActive || MeadworksInput.Typing || list == null || KitAlmanacDialog.Active != null) return;
            if (mapFocus && map != null)
            {
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if (selected != null && selected.transform.IsChildOf(list.Root)) mapFocus = false;
                else
                {
                    var select = MeadworksInput.PadDown(PadButton.A);
                    map.Pad(ZInput.GetJoyLeftStick(), ZInput.GetJoyRTrigger() - ZInput.GetJoyLTrigger(), select, Time.unscaledDeltaTime);
                    if (select) MeadworksInput.PadConsume(PadButton.A);
                    return;
                }
            }
        }

        static void BuildMarkers(List<KitVirtualRow> rows, MarkerMapPlan plan)
        {
            var index = 0;
            foreach (var group in plan.Types)
            {
                var rowIndex = index++;
                rows.Add(new KitVirtualRow("type:" + group.Type, 48, rect =>
                {
                    var choices = CairnMarkers.Load();
                    var toggle = KitToggle.Create(rect, "Value", choices.Contains(group.Entry), value =>
                    {
                        var current = CairnMarkers.Load();
                        CairnMarkers.Save(value ? current.With(group.Entry) : current.Without(group.Entry));
                        RefreshMarkers();
                    }, new Vector2(0, -4), new Vector2(310, 32), CairnText.PinTypeName(group.Type) + " (" + group.Count + ")", CairnText.PinTypeName(group.Type) + " (" + group.Count + ")");
                    toggle.Tooltip(CairnText.PinTypeName(group.Type), Ui("help_marker_type"));
                    cleanup[rect] = new List<Action> { toggle.Dispose };
                    window!.Focusable(toggle.Button, 0, rowIndex + 1);
                }, Unbind));
                foreach (var pin in plan.Pins.Where(p => p.Pin.Type == group.Type))
                {
                    var pinIndex = index++;
                    rows.Add(new KitVirtualRow("pin:" + pinIndex + ":" + pin.Entry, 42, rect =>
                    {
                        var button = KitButton.Create(rect, "Value", (pin.Shown == MarkerShown.Hidden ? "" : "• ") + (string.IsNullOrEmpty(pin.Pin.Name) ? CairnText.PinTypeName(pin.Pin.Type) : global::Localization.instance?.Localize(pin.Pin.Name) ?? pin.Pin.Name),
                            () =>
                            {
                                var result = plan.Toggle(plan.Pins.ToList().IndexOf(pin), CairnMarkers.Load());
                                if (result.ShownByType) hint!.text = CairnText.Format("ui_shown_by_type", CairnText.PinTypeName(pin.Pin.Type));
                                else if (result.Changed) { CairnMarkers.Save(result.Choices); RefreshMarkers(); }
                            }, new Vector2(15, -2), new Vector2(295, 30));
                        KitTooltip.Set(button.gameObject, string.IsNullOrEmpty(pin.Pin.Name) ? CairnText.PinTypeName(pin.Pin.Type) : global::Localization.instance?.Localize(pin.Pin.Name) ?? pin.Pin.Name,
                            Ui("help_marker_pin") + (pin.Shown == MarkerShown.ByType ? "\n" + CairnText.Format("ui_shown_by_type", CairnText.PinTypeName(pin.Pin.Type)) : ""));
                        window!.Focusable(button, 0, pinIndex + 1);
                    }, Unbind));
                }
            }
        }

        static void Unbind(RectTransform rect)
        {
            foreach (var control in rect.GetComponentsInChildren<Selectable>(true)) window?.Unfocusable(control);
            if (cleanup.TryGetValue(rect, out var releases)) foreach (var release in releases) release();
            cleanup.Remove(rect); updates.Remove(rect);
            foreach (Transform child in rect) { child.gameObject.SetActive(false); child.name = "retired"; UnityEngine.Object.Destroy(child.gameObject); }
        }

        static void ClearContent()
        {
            list?.Dispose(); list = null; map?.Dispose(); map = null; cleanup.Clear(); updates.Clear(); markerFingerprint = ""; rowKeys.Clear(); mapFocus = false;
            var cursor = window?.Panel != null ? window.Panel.Find("MapCursor") : null;
            if (cursor != null)
            {
                var control = cursor.GetComponent<Selectable>(); if (control != null) window!.Unfocusable(control);
                cursor.gameObject.SetActive(false); cursor.name = "retired"; UnityEngine.Object.Destroy(cursor.gameObject);
            }
        }
        public static void Reset() { ClearContent(); window?.Close(); window?.Destroy(); window = null; hint = null; tab = 0; dirty = false; language = ""; }

        [HarmonyPatch(typeof(KitWindow), "Pad")]
        public static class Patches
        {
            // Let the existing kit manage every other window and horizontal/control navigation. Only this list needs
            // an offscreen row realized before native grid focus can move to it.
            [HarmonyPrefix]
            static bool BeforePad(KitWindow __instance, PadButton button)
            {
                if (__instance != window || !IsOpen || MeadworksInput.Typing || KitAlmanacDialog.Active != null) return true;
                if (mapFocus && map != null && button == PadButton.A) return false;
                if (button != PadButton.DPadUp && button != PadButton.DPadDown || list == null) return true;
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if (selected == null || !selected.transform.IsChildOf(list.Root)) return true;
                list.Tick();
                var at = list.FocusedKey != null ? rowKeys.IndexOf(list.FocusedKey) : -1;
                var next = at + (button == PadButton.DPadDown ? 1 : -1);
                if (at < 0 || next < 0 || next >= rowKeys.Count) return true;
                MeadworksInput.PadConsume(button);
                list.Focus(rowKeys[next]);
                return false;
            }
        }
    }
}
