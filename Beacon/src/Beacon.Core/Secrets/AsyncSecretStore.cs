// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Threading;
using System.Threading.Tasks;

namespace ExtraPotions.Beacon.Secrets
{
    public readonly struct SecretReadResult
    {
        readonly string? secret;
        public SecretReadResult(bool found, string secret) { Found = found; this.secret = found ? secret : ""; }
        public bool Found { get; }
        public string Secret => secret ?? "";
    }

    /// <summary>OS keyring operations must not block the Unity thread. Contains is metadata only after RefreshAsync.</summary>
    public interface IAsyncSecretStore : ISecretStore
    {
        Task<bool> RefreshAsync(CancellationToken cancellation = default);
        Task<bool> StoreAsync(string reference, string secret, CancellationToken cancellation = default);
        Task<SecretReadResult> ReadAsync(string reference, CancellationToken cancellation = default);
        Task<bool> ForgetAsync(string reference, CancellationToken cancellation = default);
        Task<int> ForgetAllAsync(CancellationToken cancellation = default);
    }
}
