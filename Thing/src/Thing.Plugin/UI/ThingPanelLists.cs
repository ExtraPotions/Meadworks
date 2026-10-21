// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-owned reuse: Almanac 64a81614deb039db71d88ff70803ee648d3317dd SettingsWindow.Editors.ListEditor kit rows; data-only remote element metadata replaces native client reflection.
using System;
using System.Linq;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.ConfigEditing.Editors;
using ExtraPotions.Thing.ConfigEditing.Values;
using ExtraPotions.Thing.RemoteConfig;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingPanelLists
    {
        internal static void Open(ConfigEntryDocument entry, KitAlmanacDialog owner, Action<string> message, Action rawFallback)
        {
            var generation = CruciblePlugin.Session.Generation;
            bool Current() => owner.Panel != null && owner.Window.IsOpen && ThingPanel.CanConfigure && generation == CruciblePlugin.Session.Generation;
            ThingPanelJobs.Start(new[] { "config", "list-get", entry.File, entry.Entry }, lines =>
            {
                if (!Current()) return;
                if (!ConfigListDocument.TryDecode(lines, out var list) || list.Element.File != entry.File || list.Element.Entry != entry.Entry)
                { message(string.Join("\n", lines)); rawFallback(); return; }
                if (list.Element.Version != entry.Version || !list.Element.Editable) { message(ThingText.Text("config_stale")); return; }
                var shape = list.Element.Shape;
                string Error(string value)
                {
                    try
                    {
                        if (YamlTransfer.Bytes(value).Length > ConfigListDocument.MaxValueBytes) return ThingPanel.Ui("invalid");
                        var parsed = ValueCodec.Parse(shape, value);
                        return parsed.Ok || parsed.NeedsModConverter || shape.Kind == SettingKind.Enum && shape.IsFlags && value == "0" ? "" : parsed.Error ?? ThingPanel.Ui("invalid");
                    }
                    catch { return ThingPanel.Ui("invalid"); }
                }
                KitAlmanacList.Open(owner.Window, entry.Section + "." + entry.Key, list.Values, Error,
                    values => { if (Current()) ThingPanelConfig.SaveList(entry, values); },
                    (parent, rect, value, changed) =>
                    {
                        void Change(string text) { value = text; changed(text); }
                        if (shape.Kind == SettingKind.Bool)
                        {
                            if (value.Length == 0) Change("false");
                            var toggle = KitToggle.Create(rect, "Value", bool.TryParse(value, out var on) && on, state => Change(state ? "true" : "false"), Vector2.zero, new Vector2(410, 32), ThingPanel.Ui("on"), ThingPanel.Ui("off"));
                            toggle.Tooltip(entry.Section + "." + entry.Key, entry.Description); return toggle.Dispose;
                        }
                        if (shape.Kind == SettingKind.Enum)
                        {
                            Button? button = null;
                            button = KitButton.Create(rect, "Value", value, () => KitAlmanacChoice.Open(parent, shape.ShortTypeName, shape.EnumNames,
                                shape.IsFlags ? value.Split(',').Select(v => v.Trim()).ToArray() : new[] { value }, shape.IsFlags,
                                chosen => { if (chosen.Count == 0 && !shape.IsFlags) return; var text = shape.IsFlags ? chosen.Count == 0 ? "0" : string.Join(", ", chosen) : chosen[0]; Change(text); var label = button!.GetComponentInChildren<TMP_Text>(); if (label != null) { label.richText = false; label.text = text; } }),
                                Vector2.zero, new Vector2(410, 32));
                            var caption = button.GetComponentInChildren<TMP_Text>(); if (caption != null) caption.richText = false;
                            KitTooltip.Set(button.gameObject, entry.Section + "." + entry.Key, entry.Description);
                            return () => KitUnity.Retire(button.gameObject);
                        }
                        if (value.Length > KitInputLayout.TextLength || value.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0)
                        {
                            var button = KitButton.Create(rect, "Value", ThingPanel.Ui("edit"), () => KitAlmanacTextEditor.Open(parent, shape.ShortTypeName, value, Error, Change), Vector2.zero, new Vector2(410, 32));
                            KitTooltip.Set(button.gameObject, entry.Section + "." + entry.Key, entry.Description);
                            return () => KitUnity.Retire(button.gameObject);
                        }
                        var field = KitTextField.Create(rect, "Value", KitFieldMode.Text, Vector2.zero, new Vector2(410, 32)); field.Text = value;
                        KitTooltip.Set(field.Input.gameObject, entry.Section + "." + entry.Key, entry.Description);
                        field.Validator = text => Error(text).Length == 0; field.Ended += edit => { if (edit.Commit) Change(edit.Text); }; return field.Destroy;
                    });
            });
        }
    }
}
