// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original converter boundary: commit the secret-free list first, then use the existing async platform-store actions.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Beacon.Localization;

namespace ExtraPotions.Beacon.Runtime
{
    internal static class BeaconImportSecrets
    {
        static readonly List<Pending> pending = new List<Pending>();
        static readonly List<string> reports = new List<string>();
        internal static bool Busy => pending.Count > 0;
        internal static IReadOnlyList<string> Reports => reports.AsReadOnly();
        internal static event Action<string>? Reported;
        internal static void Start(string reference, string name, string password)
        {
            if (pending.Count == 0) reports.Clear();
            var item = new Pending(password); pending.Add(item);
            item.Operation = BeaconSecretActions.Run(cancel => BeaconSecretActions.Store(reference, password, cancel), stored =>
            {
                try
                {
                    var line = BeaconSecrets.Redactor.Clean(BeaconText.Format(stored ? BeaconTextKeys.PasswordSaved : BeaconTextKeys.ImportPasswordNotStored, name));
                    reports.Add(line); Plugin.Log?.Info(line); Reported?.Invoke(line);
                }
                finally { item.Dispose(); }
            }, () =>
            {
                try
                {
                    var line = BeaconSecrets.Redactor.Clean(BeaconText.Format(BeaconTextKeys.ImportPasswordNotStored, name));
                    reports.Add(line); Plugin.Log?.Warning(line); Reported?.Invoke(line);
                }
                finally { item.Dispose(); }
            });
        }
        sealed class Pending : IDisposable
        {
            string? secret;
            internal IDisposable? Operation;
            internal Pending(string secret) { this.secret = secret; BeaconSecrets.Redactor.Track(secret); }
            public void Dispose()
            {
                Operation?.Dispose(); Operation = null;
                BeaconSecrets.Redactor.Untrack(secret); secret = null;
                pending.Remove(this);
            }
        }
        internal static void Reset()
        {
            foreach (var item in pending.ToArray()) item.Dispose();
            reports.Clear();
            Reported = null;
            BeaconCommand.ResetPreview();
        }
    }
}
