// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same lock model as Kindle.Core (expDARE).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExtraPotions.Ember.Config
{
    /// <summary>
    /// The server's Locked Player Settings list (design 4.5): Ember Local setting ids (<c>Section.Key</c>, any case)
    /// the server forces to its own value. Unknown ids and non-Local settings are reported and ignored.
    /// </summary>
    public sealed class LockList
    {
        private readonly HashSet<string> locked;

        private LockList(IReadOnlyList<string> lockedIds, IReadOnlyList<string> unknown, IReadOnlyList<string> notLockable)
        {
            Locked = lockedIds;
            Unknown = unknown;
            NotLockable = notLockable;
            locked = new HashSet<string>(lockedIds, StringComparer.OrdinalIgnoreCase);
        }

        public static LockList Empty { get; } = new LockList(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        /// <summary>Canonical ids of the locked settings, in list order, without duplicates.</summary>
        public IReadOnlyList<string> Locked { get; }

        public IReadOnlyList<string> Unknown { get; }

        public IReadOnlyList<string> NotLockable { get; }

        public static LockList Parse(string? text)
        {
            var lockedIds = new List<string>();
            var unknown = new List<string>();
            var notLockable = new List<string>();
            foreach (var part in (text ?? "").Split(','))
            {
                var id = part.Trim();
                if (id.Length == 0) continue;
                var setting = EmberSettings.Find(id);
                var target = setting == null ? unknown : setting.Lockable ? lockedIds : notLockable;
                var name = setting?.Id ?? id;
                if (!target.Contains(name, StringComparer.OrdinalIgnoreCase)) target.Add(name);
            }
            return lockedIds.Count + unknown.Count + notLockable.Count == 0 ? Empty : new LockList(lockedIds, unknown, notLockable);
        }

        public bool IsLocked(string? id) => id != null && locked.Contains(id.Trim());

        /// <summary>One log line: the locked count, then any unknown or refused ids.</summary>
        public string Describe()
        {
            var text = Locked.Count + " locked";
            if (Unknown.Count > 0) text += "; unknown: " + string.Join(", ", Unknown);
            if (NotLockable.Count > 0) text += "; not lockable: " + string.Join(", ", NotLockable);
            return text;
        }
    }

    /// <summary>
    /// The server's own values of the locked settings, carried to clients in one hidden ServerAuthoritative text
    /// (Local settings are never synchronized themselves). Format: <c>id=value</c> parts joined by <c>;</c>, with
    /// backslash escapes for <c>\</c>, <c>;</c> and <c>=</c>. Values are the settings' serialized config texts.
    /// </summary>
    public static class LockedValues
    {
        public static string Encode(IEnumerable<KeyValuePair<string, string>> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            return string.Join(";", values.Select(v => Escape(v.Key) + "=" + Escape(v.Value ?? "")));
        }

        /// <summary>Id to value, ids in any case; malformed parts are skipped and the first value of an id wins.</summary>
        public static IReadOnlyDictionary<string, string> Decode(string? text)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

        /// <summary>The payload a server publishes: each locked id with the serialized own value, when it has one.</summary>
        public static string Compose(LockList list, Func<string, string?> ownText)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (ownText == null) throw new ArgumentNullException(nameof(ownText));
            return Encode(list.Locked.Select(id => new KeyValuePair<string, string?>(id, ownText(id)))
                .Where(p => p.Value != null).Select(p => new KeyValuePair<string, string>(p.Key, p.Value!)));
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

        /// <summary>Splits on unescaped separators, keeping escapes for the next stage.</summary>
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

        /// <summary>Removes escapes; null when the text ends inside an escape.</summary>
        private static string? Unescape(string text)
        {
            var sb = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\\')
                {
                    if (++i >= text.Length) return null;
                }
                sb.Append(text[i]);
            }
            return sb.ToString();
        }
    }

    /// <summary>Whether a Local setting is locked by the server right now, and the server's serialized value.</summary>
    public readonly struct PlayerLock
    {
        public PlayerLock(bool locked, string? serverText)
        {
            Locked = locked;
            ServerText = locked ? serverText : null;
        }

        public bool Locked { get; }

        public string? ServerText { get; }

        public override string ToString() => Locked ? "Locked (" + ServerText + ")" : "Own";
    }

    public static class PlayerLocks
    {
        /// <param name="serverBinds">
        /// True only on a remote client whose server runs Ember and has confirmed authority (the ServerAuthoritative
        /// value source is the server). The host, single player and a client of a server without Ember use their own values.
        /// </param>
        public static PlayerLock Evaluate(string id, bool serverBinds, LockList list, IReadOnlyDictionary<string, string> values)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (!serverBinds || !list.IsLocked(id) || !values.TryGetValue(id.Trim(), out var text)) return new PlayerLock(false, null);
            return new PlayerLock(true, text);
        }
    }
}
