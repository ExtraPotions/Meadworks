// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: polls the platform store's existing async operations on the Unity thread.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Beacon.Secrets;

namespace ExtraPotions.Beacon.Runtime
{
    internal static class BeaconSecretActions
    {
        interface IJob : IDisposable { bool Poll(); }
        static readonly List<IJob> jobs = new List<IJob>();
        sealed class Job<T> : IJob
        {
            readonly CancellationTokenSource cancel = new CancellationTokenSource();
            Task<T>? task;
            Action<T>? done;
            Action? failed;
            bool disposed;
            internal Job(Func<CancellationToken, Task<T>> start, Action<T> done, Action failed)
            {
                this.done = done; this.failed = failed;
                try { task = start(cancel.Token); } catch { task = Task.FromException<T>(new InvalidOperationException()); }
            }
            public bool Poll()
            {
                if (disposed) return true;
                if (task == null || !task.IsCompleted) return false;
                try { if (!task.IsCanceled && !task.IsFaulted) done?.Invoke(task.Result); else { _ = task.Exception; failed?.Invoke(); } }
                catch (Exception error) { Plugin.Log?.Warning("Beacon password operation: " + error.GetType().Name); }
                finally { Dispose(); }
                return true;
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (task != null) task.ContinueWith(completed => { _ = completed.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                cancel.Cancel(); cancel.Dispose(); done = null; failed = null; task = null;
            }
        }

        public static IDisposable Run<T>(Func<CancellationToken, Task<T>> start, Action<T> done, Action failed)
        { var job = new Job<T>(start, done, failed); jobs.Add(job); return job; }
        public static Task<bool> Store(string reference, string secret, CancellationToken cancel) => BeaconSecrets.Store is IAsyncSecretStore asyncStore
            ? asyncStore.StoreAsync(reference, secret, cancel) : Task.FromResult(BeaconSecrets.Store.Store(reference, secret));
        public static Task<bool> Forget(string reference, CancellationToken cancel) => BeaconSecrets.Store is IAsyncSecretStore asyncStore
            ? asyncStore.ForgetAsync(reference, cancel) : Task.FromResult(BeaconSecrets.Store.Forget(reference));
        public static Task<int> ForgetAll(CancellationToken cancel) => BeaconSecrets.Store is IAsyncSecretStore asyncStore
            ? asyncStore.ForgetAllAsync(cancel) : Task.FromResult(BeaconSecrets.Store.ForgetAll());
        public static Task<bool> Refresh(CancellationToken cancel) => BeaconSecrets.Store is IAsyncSecretStore asyncStore
            ? asyncStore.RefreshAsync(cancel) : Task.FromResult(true);
        public static void Tick() { foreach (var job in jobs.ToArray()) if (job.Poll()) jobs.Remove(job); }
        public static void Reset() { foreach (var job in jobs) job.Dispose(); jobs.Clear(); }
    }
}
