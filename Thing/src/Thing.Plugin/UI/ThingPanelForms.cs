// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Thing forms and controller hold confirmation compose existing kit editors/windows.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Thing.Config;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingPanelForms
    {
        internal static void Open(string title, string target, string[] captions, string[] defaults, Func<string[], string[]> command, bool destructive)
        {
            if (ThingPanel.Window == null || !ThingPanel.CanAdmin || ThingPanelJobs.Busy) return;
            var dialog = KitAlmanacDialog.Create(ThingPanel.Window, title, new Vector2(680, 470)); if (dialog == null) return;
            dialog.Dim();
            var values = new List<Func<string>>();
            var focused = new List<Func<bool>>();
            KitAlmanacDialog.Label(dialog.Panel, "Target", target, new Vector2(20, -48), new Vector2(640, 44)).richText = false;
            for (var i = 0; i < captions.Length; i++)
            {
                var help = FieldHelp(captions[i], title, target);
                KitCanvas.Fit(KitAlmanacDialog.Label(dialog.Panel, "Caption" + i, captions[i], new Vector2(20, -100 - i * 95), new Vector2(640, 25)));
                var initial = i < defaults.Length ? defaults[i] : "";
                if (captions[i] == ThingPanel.Ui("freeze") || captions[i] == ThingPanel.Ui("top_left") || captions[i] == ThingPanel.Ui("delve_dryrun"))
                {
                    var dryRun = captions[i] == ThingPanel.Ui("delve_dryrun");
                    var mode = captions[i] == ThingPanel.Ui("top_left") ? "top-left" : dryRun ? "on" : "freeze";
                    var toggle = KitToggle.Create(dialog.Panel, "Toggle", dryRun && initial == "on", _ => { }, new Vector2(20, -130 - i * 95), new Vector2(180, 40), ThingPanel.Ui("on"), ThingPanel.Ui("off"));
                    toggle.Tooltip(captions[i], help);
                    values.Add(() => toggle.Value ? mode : dryRun ? "off" : ""); focused.Add(() => false); dialog.Window.Focusable(toggle.Button, 0, i); dialog.Own(toggle.Dispose);
                }
                else if (captions[i] == ThingPanel.Ui("reason") || captions[i] == ThingPanel.Ui("text"))
                {
                    var editor = KitAlmanacMultiline.Create(dialog.Panel, "Field" + i, new Vector2(20, -130 - i * 95), new Vector2(640, i == 0 ? 200 : 115), initial);
                    editor.Input.VirtualKeyboardTitle = captions[i]; values.Add(() => editor.Text); focused.Add(() => editor.Focused);
                    KitTooltip.Set(editor.Input.gameObject, captions[i], help);
                    dialog.Window.Focusable(editor.Input, 0, i); dialog.Own(editor.Dispose);
                }
                else
                {
                    var field = KitTextField.Create(dialog.Panel, "Field" + i, KitFieldMode.Text, new Vector2(20, -130 - i * 95), new Vector2(640, 40), keyboardTitle: captions[i]);
                    field.Text = initial; values.Add(() => field.Text); focused.Add(() => field.Focused); dialog.Window.Focusable(field.Input, 0, i); dialog.Own(field.Destroy);
                    KitTooltip.Set(field.Input.gameObject, captions[i], help);
                }
            }
            dialog.TypingField = () => focused.Any(f => f());
            var apply = KitButton.Create(dialog.Panel, "Apply", ThingPanel.Ui("apply"), () =>
            {
                if (!ThingPanel.CanAdmin || ThingPanelJobs.Busy) return;
                string[] args;
                try { args = command(values.Select(value => value()).ToArray()); }
                catch { ThingPanel.Message(ThingPanel.Ui("invalid")); return; }
                if (args.Length == 0) { dialog.Dispose(); return; }
                if (destructive && (MeadworksInput.GamepadActive || Plugin.Settings?.Get(ThingSettings.General.ConfirmDestructive) == true))
                    Confirm(dialog.Window, title + "\n" + target, () => { dialog.Dispose(); ThingPanel.Send(args); });
                else { dialog.Dispose(); ThingPanel.Send(args); }
            }, new Vector2(20, -414), new Vector2(300, 36));
            var close = KitButton.Create(dialog.Panel, "Cancel", ThingPanel.Ui("cancel"), dialog.Dispose, new Vector2(360, -414), new Vector2(300, 36));
            KitTooltip.Set(apply.gameObject, title, ThingPanel.Ui("apply_hint") + (target.Length > 0 ? "\n" + target : ""));
            KitTooltip.Set(close.gameObject, ThingPanel.Ui("cancel"), ThingPanel.Ui("cancel_hint"));
            dialog.Window.Focusable(apply, 0, 5); dialog.Window.Focusable(close, 1, 5);
        }

        static string FieldHelp(string caption, string title, string target)
        {
            foreach (var key in new[] { "freeze", "top_left", "delve_dryrun", "clock", "reason", "minutes", "platform_id" })
                if (caption == ThingPanel.Ui(key)) return ThingPanel.Ui(key + "_hint");
            if (caption == ThingPanel.Ui("duration") || caption == ThingPanel.Ui("mute_duration")) return ThingPanel.Ui("duration_hint");
            if (caption == ThingPanel.Ui("repeat_minutes")) return ThingPanel.Ui("announcement_hint");
            if (caption == ThingPanel.Ui("radius") && title == ThingPanel.Ui("terrain")) return ThingPanel.Ui("terrain_radius_hint");
            if (caption == ThingPanel.Ui("text") && target.Length == 0) return ThingPanel.Ui("message_hint");
            return title + "\n" + caption + (target.Length > 0 ? "\n" + target : "");
        }

        internal static void Confirm(KitWindow parent, string words, Action apply, Action? cancelled = null, bool settingsOnly = false)
        {
            bool Allowed() => settingsOnly ? ThingPanel.CanConfigure : ThingPanel.CanOpen || ThingPanel.CanPeople;
            if (!Allowed()) return;
            var dialog = KitAlmanacDialog.Create(parent, ThingPanel.Ui("confirm"), new Vector2(620, 300)); if (dialog == null) return;
            KitAlmanacDialog.Label(dialog.Panel, "Detail", words, new Vector2(20, -54), new Vector2(580, 110)).richText = false;
            var hint = KitCanvas.Fit(KitAlmanacDialog.Label(dialog.Panel, "Hold", ThingPanel.Ui("hold"), new Vector2(20, -180), new Vector2(580, 30)));
            var done = false; var held = 0f;
            dialog.Own(() => { if (!done) cancelled?.Invoke(); });
            void Commit() { if (done || !Allowed() || ThingPanelJobs.Busy) return; done = true; dialog.Dispose(); apply(); }
            var yes = KitButton.Create(dialog.Panel, "Confirm", ThingPanel.Ui("confirm"), () => { if (!MeadworksInput.GamepadActive) Commit(); }, new Vector2(20, -240), new Vector2(270, 36));
            var no = KitButton.Create(dialog.Panel, "Cancel", ThingPanel.Ui("cancel"), dialog.Dispose, new Vector2(330, -240), new Vector2(270, 36));
            KitTooltip.Set(yes.gameObject, ThingPanel.Ui("confirm"), words + "\n" + ThingPanel.Ui("hold"));
            KitTooltip.Set(no.gameObject, ThingPanel.Ui("cancel"), ThingPanel.Ui("cancel_hint"));
            dialog.Window.Focusable(yes, 0, 0); dialog.Window.Focusable(no, 1, 0);
            dialog.Tick += () =>
            {
                if (!Allowed()) { dialog.Dispose(); return; }
                var selected = EventSystem.current?.currentSelectedGameObject == yes.gameObject;
                held = MeadworksInput.GamepadActive && selected && MeadworksInput.PadHeld(PadButton.A) ? held + Time.unscaledDeltaTime : 0;
                hint.text = held > 0 ? ThingText.Format("ui_hold_progress", Math.Min(100, (int)(held / 1.2f * 100))) : ThingPanel.Ui("hold");
                if (held >= 1.2f) { MeadworksInput.PadConsume(PadButton.A); Commit(); }
            };
        }
    }
}
