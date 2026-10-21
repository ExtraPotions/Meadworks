// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original async keyring boundary around the game's unchanged client handshake.
using System;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Beacon.Joining;
using ExtraPotions.Beacon.Localization;
using ExtraPotions.Beacon.Secrets;
using ExtraPotions.Beacon.Servers;

namespace ExtraPotions.Beacon.Runtime
{
    public static partial class BeaconJoin
    {
        static CancellationTokenSource? readCancel;
        static Task<SecretReadResult>? read;
        static LinuxKeyringStore? readLinux;
        static long readFallbackRevision;
        static ZNet? deferredNet;
        static ZRpc? deferredRpc;
        static string? deferredSalt;
        static ServerAddress? deferredTarget;
        static string? resumedSecret;
        static bool resuming;
        static IDisposable? saveOperation;
        static string? savingSecret;

        internal static bool DeferHandshake(ZNet net, ZRpc rpc, bool needPassword, string salt)
        {
            if (!resuming && (!Active || FejdStartup.ServerPassword != null) && read != null)
            {
                ClearRead();
                coordinator.OnStoredUnreadable();
            }
            if (resuming || !needPassword || !Active || FejdStartup.ServerPassword != null ||
                !(BeaconSecrets.Store is IAsyncSecretStore store) || coordinator.Entry == null) return false;
            if (read != null && deferredNet == net && ReferenceEquals(deferredRpc, rpc)) return true;
            if (coordinator.OnPasswordRequested() != JoinAction.SendStored) return false;
            ClearRead();
            readCancel = new CancellationTokenSource();
            readLinux = store as LinuxKeyringStore;
            readFallbackRevision = readLinux?.FallbackRevision ?? 0;
            read = store.ReadAsync(coordinator.Entry.SecretRef, readCancel.Token);
            deferredNet = net; deferredRpc = rpc; deferredSalt = salt;
            deferredTarget = coordinator.Target;
            return true;
        }

        static void TickHandshake()
        {
            if (read == null) return;
            if (!coordinator.AwaitingConnection || deferredTarget != coordinator.Target || deferredNet == null || deferredNet != ZNet.instance || deferredRpc == null || !deferredRpc.IsConnected())
            { ClearRead(); return; }
            if (!read.IsCompleted) return;
            var result = !read.IsCanceled && !read.IsFaulted &&
                (readLinux == null || readLinux.IsReadCurrent(readFallbackRevision)) ? read.Result : default;
            var net = deferredNet; var rpc = deferredRpc; var salt = deferredSalt;
            ClearRead();
            var launchPassword = FejdStartup.ServerPassword != null;
            if (launchPassword) coordinator.OnStoredUnreadable();
            if (!launchPassword && result.Found && !string.IsNullOrEmpty(result.Secret))
            {
                resumedSecret = result.Secret;
                BeaconSecrets.Redactor.Track(resumedSecret);
            }
            else if (!launchPassword)
            {
                coordinator.OnStoredUnreadable();
                if (BeaconSecrets.Store.LastFailure != SecretFailure.None) Report(BeaconText.Text(BeaconTextKeys.SecretStoreFailed));
            }
            try
            {
                resuming = true;
                net.RPC_ClientHandshake(rpc, true, salt!);
            }
            catch (Exception error) { Plugin.Log?.Warning("Beacon resumed handshake: " + error.GetType().Name); }
            finally
            {
                resuming = false;
                BeaconSecrets.Redactor.Untrack(resumedSecret); resumedSecret = null;
            }
        }

        static void ResumeWithoutSecret()
        {
            if (read == null) return;
            var valid = coordinator.AwaitingConnection && deferredTarget == coordinator.Target && deferredNet != null &&
                deferredNet == ZNet.instance && deferredRpc != null && deferredRpc.IsConnected();
            var net = deferredNet; var rpc = deferredRpc; var salt = deferredSalt;
            ClearRead();
            coordinator.OnStoredUnreadable();
            if (!valid) return;
            try { resuming = true; net!.RPC_ClientHandshake(rpc!, true, salt!); }
            catch (Exception error) { Plugin.Log?.Warning("Beacon native handshake fallback: " + error.GetType().Name); }
            finally { resuming = false; }
        }

        static void SaveTypedAsync(string reference, string secret, string name)
        {
            ClearSave();
            savingSecret = secret; BeaconSecrets.Redactor.Track(secret);
            saveOperation = BeaconSecretActions.Run(cancel => BeaconSecretActions.Store(reference, secret, cancel), saved =>
            {
                if (saved) Plugin.Log?.Info(BeaconSecrets.Redactor.Clean(BeaconText.Format(BeaconTextKeys.PasswordSaved, name)));
                else Report(BeaconText.Text(BeaconTextKeys.SecretStoreFailed));
                ClearSave();
            }, () => { Report(BeaconText.Text(BeaconTextKeys.SecretStoreFailed)); ClearSave(); });
        }

        static void ClearSave()
        {
            saveOperation?.Dispose(); saveOperation = null;
            BeaconSecrets.Redactor.Untrack(savingSecret); savingSecret = null;
        }
        static void ClearRead()
        {
            readCancel?.Cancel(); readCancel?.Dispose(); readCancel = null;
            readLinux = null;
            read = null; deferredNet = null; deferredRpc = null; deferredSalt = null; deferredTarget = null;
        }
        static void ResetAsyncSecrets()
        {
            ClearRead(); ClearSave();
            BeaconSecrets.Redactor.Untrack(resumedSecret); resumedSecret = null; resuming = false;
        }
    }
}
