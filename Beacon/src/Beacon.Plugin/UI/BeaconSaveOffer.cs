// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the accepted password's explicit save offer (BC05/F3); no password is displayed or logged.
using System;
using ExtraPotions.Beacon.Servers;
using ExtraPotions.Beacon.Localization;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;

namespace ExtraPotions.Beacon.Runtime
{
    internal static class BeaconSaveOffer
    {
        static KitWindow? window;
        static ServerAddress? target;
        static string? secret;
        static IDisposable? operation;
        static bool waiting;
        public static bool Pending => target != null;
        public static bool Showing => window?.IsOpen == true;

        public static void Offer(ServerAddress address, string password)
        {
            Reset(); target = address; secret = password; BeaconSecrets.Redactor.Track(password);
        }

        public static void Tick()
        {
            if (target == null) return;
            if (!Plugin.Ready || !BeaconJoin.Active || ZNet.instance == null || ZNet.GetConnectionStatus() != ZNet.ConnectionStatus.Connected)
            { Reset(); return; }
            if (window?.Panel != null || KitCanvas.Front == null || !CrucibleUiPlugin.Active) return;
            window = KitWindow.Create("Beacon_SaveOffer", new Vector2(500, 190));
            if (window == null) return;
            KitAlmanacDialog.Label(window.Panel, "Title", BeaconText.Format(BeaconTextKeys.OfferSave, target.ToString()), new Vector2(20, -15), new Vector2(460, 62), 18);
            var save = KitButton.Create(window.Panel, "Save", BeaconText.Text("ui_save"), Accept, new Vector2(30, -120), new Vector2(200, 36));
            var cancel = KitButton.Create(window.Panel, "Cancel", BeaconText.Text("ui_cancel"), Reset, new Vector2(270, -120), new Vector2(200, 36));
            BeaconPanel.Tip(save.gameObject,"save",BeaconText.Text("ui_help_save_password"));BeaconPanel.Tip(cancel.gameObject,"cancel");
            window.Focusable(save, 0, 0); window.Focusable(cancel, 1, 0);
            window.Closed += Reset; window.Open();
        }

        static void Accept()
        {
            if (waiting || BeaconImportSecrets.Busy || target == null || string.IsNullOrEmpty(secret) || Plugin.Log == null) return;
            var address = target;
            var entry = BeaconServers.List.Find(address);
            if (entry == null)
            {
                var name = address.ToString();
                for (var i = 2; BeaconServers.List.FindByName(name) != null; i++) name = address + " (" + i + ")";
                if (!BeaconServers.Amend(list => list.Add(name, address), Plugin.Log)) { Reset(); return; }
                entry = BeaconServers.List.Find(address)!;
            }
            waiting = true;
            var reference = entry.SecretRef;
            operation = BeaconSecretActions.Run(cancel => BeaconSecretActions.Store(reference, secret!, cancel), saved =>
            {
                Plugin.Log?.Info(saved ? BeaconSecrets.Redactor.Clean(BeaconText.Format(BeaconTextKeys.PasswordSaved, entry.Name)) : BeaconText.Text(BeaconTextKeys.SecretStoreFailed));
                Reset();
            }, () => { Plugin.Log?.Warning(BeaconText.Text(BeaconTextKeys.SecretStoreFailed)); Reset(); });
        }

        public static void Reset()
        {
            operation?.Dispose(); operation = null;
            BeaconSecrets.Redactor.Untrack(secret); secret = null; target = null; waiting = false;
            var old = window; window = null;
            if (old != null) { old.Closed -= Reset; old.Close(); old.Destroy(); }
        }
    }
}
