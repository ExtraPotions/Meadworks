// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original integration of the public libsecret API; no external implementation adopted.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExtraPotions.Beacon.Secrets
{
    /// <summary>Nonblocking Secret Service backend with the approved session-only/disabled fallback.</summary>
    public sealed class LinuxKeyringStore : IAsyncSecretStore, IDisposable
    {
        readonly ISecretStore session = MemorySecretStore.SessionOnly();
        readonly ISecretStore disabled = new DisabledSecretStore();
        LinuxFallback selectedFallback;
        long fallbackRevision;
        ISecretStore FallbackStore => selectedFallback == LinuxFallback.Disabled ? disabled : session;
        readonly SemaphoreSlim gate = new SemaphoreSlim(1);
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly HashSet<string> references = new HashSet<string>(StringComparer.Ordinal);
        volatile bool unavailable, disposed;
        public LinuxKeyringStore(LinuxFallback selectedFallback)
        {
            SetFallback(selectedFallback);
        }
        public SecretBackend Backend { get { lock (references) return unavailable ? FallbackStore.Backend : SecretBackend.LinuxKeyring; } }
        public SecretAvailability Availability { get { lock (references) return unavailable ? FallbackStore.Availability : SecretAvailability.Available; } }
        public long FallbackRevision { get { lock (references) return fallbackRevision; } }
        public void SetFallback(LinuxFallback policy)
        {
            if (policy != LinuxFallback.SessionOnly && policy != LinuxFallback.Disabled) throw new ArgumentOutOfRangeException(nameof(policy));
            lock (references)
            {
                if (disposed || selectedFallback == policy) return;
                selectedFallback = policy;
                fallbackRevision++;
            }
        }
        // A completed session read can wait on the game thread while the policy changes.
        public bool IsReadCurrent(long revision)
        {
            lock (references) return !disposed && (!unavailable ||
                selectedFallback == LinuxFallback.SessionOnly && fallbackRevision == revision);
        }
        public SecretFailure LastFailure { get; private set; }
        public bool Contains(string reference)
        {
            if (!SecretReference.IsValid(reference) || disposed) return false;
            lock (references) return unavailable ? FallbackStore.Contains(reference) : references.Contains(reference);
        }
        // The synchronous interface must never turn into a blocking native call on the game thread.
        public bool Store(string reference, string secret) { SecretReference.Require(reference, secret); return false; }
        public bool TryRead(string reference, out string secret) { SecretReference.Require(reference); secret = ""; return false; }
        public bool Forget(string reference) { SecretReference.Require(reference); return false; }
        public int ForgetAll() => 0;

        public Task<bool> RefreshAsync(CancellationToken cancellation = default) => Run(token =>
        {
            var found = LibSecretNative.References(token);
            lock (references)
            {
                if (disposed || token.IsCancellationRequested) return false;
                references.Clear(); references.UnionWith(found);
            }
            return true;
        }, () => false, false, cancellation);

        public Task<bool> StoreAsync(string reference, string secret, CancellationToken cancellation = default)
        {
            SecretReference.Require(reference, secret);
            if (secret.IndexOf('\0') >= 0) throw new ArgumentException("A password cannot contain a NUL character.", nameof(secret));
            return Run(token =>
            {
                var stored = LibSecretNative.Store(reference, secret, token);
                if (stored) lock (references)
                {
                    if (disposed || token.IsCancellationRequested) return false;
                    references.Add(reference);
                }
                return stored;
            }, () => { lock (references) return FallbackStore.Store(reference, secret); }, false, cancellation);
        }

        public Task<SecretReadResult> ReadAsync(string reference, CancellationToken cancellation = default)
        {
            SecretReference.Require(reference);
            return Run(token => LibSecretNative.Read(reference, token), () =>
            {
                lock (references) return FallbackStore.TryRead(reference, out var secret) ? new SecretReadResult(true, secret) : default;
            }, default, cancellation);
        }

        public Task<bool> ForgetAsync(string reference, CancellationToken cancellation = default)
        {
            SecretReference.Require(reference);
            return Run(token =>
            {
                var removed = LibSecretNative.Forget(reference, token);
                if (removed) lock (references) references.Remove(reference);
                return removed;
            }, () => { lock (references) return FallbackStore.Forget(reference); }, false, cancellation);
        }

        public Task<int> ForgetAllAsync(CancellationToken cancellation = default) => Run(token =>
        {
            // Search our schema only. Locked items that clear cannot remove stay visible in metadata.
            var found = LibSecretNative.References(token);
            var removed = 0;
            foreach (var reference in found)
                if (LibSecretNative.Forget(reference, token)) { removed++; lock (references) references.Remove(reference); }
            return removed;
        }, () => { lock (references) return FallbackStore.ForgetAll(); }, 0, cancellation);

        Task<T> Run<T>(Func<CancellationToken, T> operation, Func<T> onUnavailable, T canceled, CancellationToken cancellation)
        {
            long revision;
            lock (references)
            {
                if (disposed || cancellation.IsCancellationRequested) return Task.FromResult(canceled);
                revision = fallbackRevision;
            }
            return Task.Run(async () =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token);
                var entered = false;
                T Fallback()
                {
                    lock (references)
                    {
                        if (disposed || linked.IsCancellationRequested || revision != fallbackRevision) return canceled;
                        return onUnavailable();
                    }
                }
                try
                {
                    await gate.WaitAsync(linked.Token).ConfigureAwait(false);
                    entered = true;
                    if (disposed || linked.IsCancellationRequested) return canceled;
                    if (unavailable) return Fallback();
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    try
                    {
                        var result = operation(timeout.Token);
                        if (disposed || linked.IsCancellationRequested) return canceled;
                        LastFailure = SecretFailure.None;
                        return result;
                    }
                    catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException ||
                        error is BadImageFormatException || error is KeyringUnavailableException || error is OperationCanceledException)
                    {
                        if (linked.IsCancellationRequested) return canceled;
                        unavailable = true;
                        LastFailure = SecretFailure.Unavailable;
                        return Fallback();
                    }
                }
                catch (OperationCanceledException) { return canceled; }
                finally { if (entered) gate.Release(); }
            });
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            // In-flight native calls retain their cancellable/schema until returning. No wait on Unity's thread.
            lock (references) { references.Clear(); session.ForgetAll(); }
        }
    }
    internal sealed class KeyringUnavailableException : Exception { }
}
