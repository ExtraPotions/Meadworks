// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: BC05 add/edit dialog uses AX13 fields and the shared masked KX-B1 password control.
using System;
using System.Linq;
using ExtraPotions.Beacon.Servers;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;

namespace ExtraPotions.Beacon.Runtime
{
    internal static class BeaconEditor
    {
        static KitAlmanacDialog? dialog;
        static KitTextField? name, address, password;
        static KitAlmanacMultiline? aliases;
        static IDisposable? operation;
        static string? tracked;
        static bool saving;
        static TMP_Text? hint;
        static string Ui(string key) => BeaconPanel.Ui(key);

        public static void Open(ServerEntry? entry)
        {
            Reset();
            if (BeaconPanel.Window == null || !BeaconServers.Writable || BeaconImportSecrets.Busy) return;
            dialog = KitAlmanacDialog.Create(BeaconPanel.Window, Ui(entry == null ? "add" : "edit"), BeaconPanel.Size);
            if (dialog == null) return;
            dialog.Dim();
            name = Field("name", 65, entry?.Name ?? "");
            address = Field("address", 135, entry?.Address.ToString() ?? "");
            KitAlmanacDialog.Label(dialog.Panel, "AliasesCaption", Ui("aliases"), new Vector2(20, -182), new Vector2(740, 24));
            aliases = KitAlmanacMultiline.Create(dialog.Panel, "Aliases", new Vector2(20, -205), new Vector2(740, 70), entry == null ? "" : string.Join(", ", entry.Aliases), lineNumbers: false);
            aliases.Input.VirtualKeyboardTitle = Ui("aliases");
            dialog.Window.Focusable(aliases.Input, 0, 2);
            BeaconPanel.Tip(aliases.Input.gameObject,"aliases");
            password = Field("password", 315, "", true);
            hint = KitAlmanacDialog.Label(dialog.Panel, "Hint", Ui("password_hint"), new Vector2(20, -370), new Vector2(740, 62), 14);
            name.Validator = value => !string.IsNullOrWhiteSpace(value);
            address.Validator = value => ServerAddress.TryParse(value, out _);
            var save = KitButton.Create(dialog.Panel, "Save", Ui("save"), () => Save(entry), new Vector2(20, -550), new Vector2(350, 38));
            var cancel = KitButton.Create(dialog.Panel, "Cancel", Ui("cancel"), Reset, new Vector2(410, -550), new Vector2(350, 38));
            BeaconPanel.Tip(save.gameObject,"save");BeaconPanel.Tip(cancel.gameObject,"cancel");
            dialog.Window.Focusable(save, 0, 4); dialog.Window.Focusable(cancel, 1, 4);
            dialog.Tick += () =>
            {
                var usable = !saving && !BeaconImportSecrets.Busy;
                name?.SetUsable(usable); address?.SetUsable(usable); aliases?.SetUsable(usable); password?.SetUsable(usable);
                if (save != null) save.interactable = usable;
            };
            dialog.Closed += Reset;
            dialog.Own(() => { name?.Destroy(); address?.Destroy(); aliases?.Dispose(); password?.Destroy(); });
            dialog.TypingField = () => name?.Focused == true || address?.Focused == true || aliases?.Focused == true || password?.Focused == true;
        }

        static KitTextField Field(string key, float y, string value, bool secret = false)
        {
            KitAlmanacDialog.Label(dialog!.Panel, key + "Caption", Ui(key), new Vector2(20, -y + 23), new Vector2(740, 24));
            var field = KitTextField.Create(dialog.Panel, key, secret ? KitFieldMode.Password : KitFieldMode.Text, new Vector2(20, -y), new Vector2(740, 36), keyboardTitle: Ui(key));
            field.Text = value;
            BeaconPanel.Tip(field.Input.gameObject,key,key=="password"?Ui("password_hint"):null);
            dialog.Window.Focusable(field.Input, 0, (int)((y - 65) / 70));
            return field;
        }

        static void Save(ServerEntry? previous)
        {
            if (saving || BeaconImportSecrets.Busy || dialog == null || name == null || address == null || aliases == null || password == null) return;
            // Existing imported values must survive the shared short field's display limit when only another field changes.
            var nextName = previous != null && name.Text == KitFieldRules.Clip(KitFieldMode.Text, previous.Name) ? previous.Name : name.Text.Trim();
            ServerAddress parsed;
            ServerAddress[] others;
            try
            {
                parsed = previous != null && address.Text == KitFieldRules.Clip(KitFieldMode.Text, previous.Address.ToString()) ? previous.Address : ServerAddress.Parse(address.Text);
                others = aliases.Text.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(ServerAddress.Parse).ToArray();
            }
            catch { hint!.text = Ui("invalid"); return; }
            if (!BeaconPanel.Amend(candidate => { if (previous == null) candidate.Add(nextName, parsed, others); else candidate.Edit(previous.Name, nextName, parsed, others); })) { hint!.text = Ui("invalid"); return; }
            var entry = BeaconServers.List.FindByName(nextName)!;
            var secret = password.Text;
            if (secret.Length == 0) { Reset(); return; }
            tracked = secret; BeaconSecrets.Redactor.Track(secret); saving = true;
            name.SetUsable(false); address.SetUsable(false); aliases.SetUsable(false); password.SetUsable(false);
            operation = BeaconSecretActions.Run(cancel => BeaconSecretActions.Store(entry.SecretRef, secret, cancel), saved =>
            {
                BeaconPanel.Message(saved ? BeaconText.Format("password_saved", entry.Name) : BeaconText.Text("secret_store_failed"));
                Reset();
            }, () => { BeaconPanel.Message(BeaconText.Text("secret_store_failed")); Reset(); });
        }

        public static void Reset()
        {
            operation?.Dispose(); operation = null;
            BeaconSecrets.Redactor.Untrack(tracked); tracked = null; saving = false;
            var old = dialog; dialog = null;
            if (old != null) { old.Closed -= Reset; old.Dispose(); }
            name = address = password = null; aliases = null;
            hint = null;
        }
    }
}
