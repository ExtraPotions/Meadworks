// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: protected password storage (design section 4). AutoConnector keeps passwords in plain text; Beacon never does.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Beacon.Secrets
{
    public enum SecretAvailability { Available, SessionOnly, Unavailable }

    public enum SecretBackend { WindowsDataProtection, LinuxKeyring, SessionOnly, Disabled, Memory }

    public enum SecretPlatform { Windows, Linux, Other }

    /// <summary>Q1: what Beacon does on Linux without a usable keyring. (a) SessionOnly was accepted on 2026-10-07.</summary>
    public enum LinuxFallback { SessionOnly, Disabled }

    /// <summary>Why the last store or read failed; reported to the player once, never with the secret.</summary>
    public enum SecretFailure { None, StoredElsewhere, ProtectFailed, Unavailable }

    /// <summary>
    /// A per-platform secret back end (design 4.4). References are opaque ids from the server list; secrets are only
    /// read at join time. Implementations never log, print or persist a secret outside protected storage.
    /// </summary>
    public interface ISecretStore
    {
        SecretBackend Backend { get; }
        SecretAvailability Availability { get; }
        SecretFailure LastFailure { get; }
        bool Store(string reference, string secret);
        bool TryRead(string reference, out string secret);
        bool Contains(string reference);
        bool Forget(string reference);
        int ForgetAll();
    }

    /// <summary>Secret references: 1 to 64 characters of letters, digits, '-' and '_'.</summary>
    public static class SecretReference
    {
        public static bool IsValid(string? reference)
        {
            if (string.IsNullOrEmpty(reference) || reference!.Length > 64) return false;
            foreach (var c in reference)
                if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c == '-' || c == '_')) return false;
            return true;
        }

        public static void Require(string reference, string secret)
        {
            if (!IsValid(reference)) throw new ArgumentException("Invalid secret reference.", nameof(reference));
            if (string.IsNullOrEmpty(secret)) throw new ArgumentException("An empty secret is not stored.", nameof(secret));
        }

        public static void Require(string reference)
        {
            if (!IsValid(reference)) throw new ArgumentException("Invalid secret reference.", nameof(reference));
        }
    }

    /// <summary>
    /// Secrets kept in process memory only: the deterministic test double, and the Q1 (a) session-only fallback (passwords
    /// are remembered until the game closes, then asked again).
    /// </summary>
    public sealed class MemorySecretStore : ISecretStore
    {
        readonly Dictionary<string, string> secrets = new Dictionary<string, string>(StringComparer.Ordinal);

        public MemorySecretStore(SecretBackend backend = SecretBackend.Memory, SecretAvailability availability = SecretAvailability.Available)
        {
            Backend = backend;
            Availability = availability;
        }

        public static MemorySecretStore SessionOnly() => new MemorySecretStore(SecretBackend.SessionOnly, SecretAvailability.SessionOnly);

        public SecretBackend Backend { get; }
        public SecretAvailability Availability { get; }
        public SecretFailure LastFailure => SecretFailure.None;

        public bool Store(string reference, string secret)
        {
            SecretReference.Require(reference, secret);
            secrets[reference] = secret;
            return true;
        }

        public bool TryRead(string reference, out string secret)
        {
            SecretReference.Require(reference);
            if (secrets.TryGetValue(reference, out var s)) { secret = s; return true; }
            secret = "";
            return false;
        }

        public bool Contains(string reference) => SecretReference.IsValid(reference) && secrets.ContainsKey(reference);

        public bool Forget(string reference)
        {
            SecretReference.Require(reference);
            return secrets.Remove(reference);
        }

        public int ForgetAll()
        {
            var n = secrets.Count;
            secrets.Clear();
            return n;
        }
    }

    /// <summary>Q1 fallback "do not store": every store fails and the player is asked each time.</summary>
    public sealed class DisabledSecretStore : ISecretStore
    {
        public SecretBackend Backend => SecretBackend.Disabled;
        public SecretAvailability Availability => SecretAvailability.Unavailable;
        public SecretFailure LastFailure { get; private set; }

        public bool Store(string reference, string secret)
        {
            SecretReference.Require(reference, secret);
            LastFailure = SecretFailure.Unavailable;
            return false;
        }

        public bool TryRead(string reference, out string secret)
        {
            SecretReference.Require(reference);
            secret = "";
            return false;
        }

        public bool Contains(string reference) => false;
        public bool Forget(string reference) => false;
        public int ForgetAll() => 0;
    }

    public static class SecretStores
    {
        public const LinuxFallback DefaultLinuxFallback = LinuxFallback.SessionOnly;

        /// <summary>
        /// Back end per platform (design 4.2, 4.3, Q1): Windows Data Protection on Windows; the Secret Service keyring on
        /// Linux when it is usable, else the chosen fallback; any other platform uses the fallback too.
        /// </summary>
        public static SecretBackend Choose(SecretPlatform platform, bool keyringUsable, LinuxFallback fallback)
        {
            if (platform == SecretPlatform.Windows) return SecretBackend.WindowsDataProtection;
            if (platform == SecretPlatform.Linux && keyringUsable) return SecretBackend.LinuxKeyring;
            return fallback == LinuxFallback.Disabled ? SecretBackend.Disabled : SecretBackend.SessionOnly;
        }

        /// <summary>The localization key of the panel's "where are passwords stored" line.</summary>
        public static string StorageLineKey(SecretBackend backend) => backend switch
        {
            SecretBackend.WindowsDataProtection => "storage_windows",
            SecretBackend.LinuxKeyring => "storage_keyring",
            SecretBackend.Disabled => "storage_disabled",
            _ => "storage_session",
        };
    }
}
