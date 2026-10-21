// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: secret-free logging by construction (design 4.1 and section 8, C06).
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Beacon.Secrets
{
    /// <summary>
    /// Masks every secret Beacon currently holds in any text before it reaches a log, the console or an error report.
    /// Secrets are tracked while in memory for a join and untracked when the join ends.
    /// </summary>
    public sealed class SecretRedactor
    {
        public const string Mask = "***";

        readonly Dictionary<string, int> secrets = new Dictionary<string, int>(StringComparer.Ordinal);

        public void Track(string? secret)
        {
            if (!string.IsNullOrEmpty(secret)) secrets[secret!] = secrets.TryGetValue(secret!, out var count) ? count + 1 : 1;
        }

        public void Untrack(string? secret)
        {
            if (!string.IsNullOrEmpty(secret) && secrets.TryGetValue(secret!, out var count))
            { if (count > 1) secrets[secret!] = count - 1; else secrets.Remove(secret!); }
        }

        public void Clear() => secrets.Clear();

        public string Clean(string? message)
        {
            if (string.IsNullOrEmpty(message)) return "";
            var text = message!;
            foreach (var secret in secrets.Keys.OrderByDescending(s => s.Length))
                text = text.Replace(secret, Mask);
            return text;
        }
    }
}
