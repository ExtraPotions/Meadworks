// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; the carrier format follows Timber.Core Config/PlayerLocks.cs LockedValues (expDARE).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExtraPotions.Threshold.Config
{
    /// <summary>
    /// The server's own values of its Locked Player Settings, carried to clients in one hidden ServerAuthoritative text
    /// (Local settings are never synchronized themselves). Format: <c>id=value</c> parts joined by <c>;</c>, with
    /// backslash escapes for <c>\</c>, <c>;</c> and <c>=</c>. Values are the settings' serialized config texts.
    /// </summary>
    public static class LockedPlayerValues
    {
        public static string Encode(IEnumerable<KeyValuePair<string, string>> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            return string.Join(";", values.Select(v => Escape(v.Key) + "=" + Escape(v.Value ?? "")));
        }

        /// <summary>Id to value; malformed parts are skipped and the first value of an id wins.</summary>
        public static IReadOnlyDictionary<string, string> Decode(string? text)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return result;
            foreach (var part in Split(text!, ';'))
            {
                var pieces = Split(part, '=').ToList();
                if (pieces.Count != 2) continue;
                var key = Unescape(pieces[0]);
                var value = Unescape(pieces[1]);
                if (string.IsNullOrWhiteSpace(key) || value == null || result.ContainsKey(key!.Trim())) continue;
                result.Add(key.Trim(), value);
            }
            return result;
        }

        /// <summary>The payload a server publishes: each lockable id named in its list with its own serialized value.</summary>
        public static string Compose(string? lockedList, Func<string, string?> ownText)
        {
            if (ownText == null) throw new ArgumentNullException(nameof(ownText));
            return Encode(ThresholdSettings.ParseLocked(lockedList).Select(id => new KeyValuePair<string, string?>(id, ownText(id)))
                .Where(p => p.Value != null).Select(p => new KeyValuePair<string, string>(p.Key, p.Value!)));
        }

        /// <summary>
        /// The server's serialized value for <paramref name="id"/> while it is locked; null when the player's own value applies.
        /// <paramref name="serverBinds"/> is true only on a remote client whose Threshold server has confirmed authority.
        /// </summary>
        public static string? ServerText(string id, bool serverBinds, IReadOnlyList<string> locked, IReadOnlyDictionary<string, string> values)
        {
            if (locked == null) throw new ArgumentNullException(nameof(locked));
            if (values == null) throw new ArgumentNullException(nameof(values));
            return serverBinds && id != null && locked.Contains(id) && values.TryGetValue(id, out var text) ? text : null;
        }

        private static string Escape(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (c == '\\' || c == ';' || c == '=') sb.Append('\\');
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static IEnumerable<string> Split(string text, char separator)
        {
            var start = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\\') { i++; continue; }
                if (text[i] == separator)
                {
                    yield return text.Substring(start, i - start);
                    start = i + 1;
                }
            }
            yield return text.Substring(start);
        }

        private static string? Unescape(string text)
        {
            var sb = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\\' && ++i >= text.Length) return null;
                sb.Append(text[i]);
            }
            return sb.ToString();
        }
    }
}
