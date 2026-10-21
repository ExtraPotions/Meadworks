// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ExtraPotions.Almanac.Values
{
    /// <summary>Outcome of parsing BepInEx text for a setting: canonical text as BepInEx writes it, the parsed value, or an error.</summary>
    public sealed class ParseResult
    {
        private ParseResult() { }

        public bool Ok { get; private set; }
        /// <summary>Canonical BepInEx file text (strings escaped).</summary>
        public string Text { get; private set; } = "";
        public object? Value { get; private set; }
        public string? Error { get; private set; }
        /// <summary>The value was outside <c>AcceptableValueRange</c> and was clamped, as BepInEx does.</summary>
        public bool WasClamped { get; private set; }
        /// <summary>No portable codec: the plugin must use the mod's converter or delegates.</summary>
        public bool NeedsModConverter { get; private set; }

        internal static ParseResult Success(string text, object? value, bool clamped = false) => new ParseResult { Ok = true, Text = text, Value = value, WasClamped = clamped };
        internal static ParseResult Fail(string error) => new ParseResult { Error = error };
        internal static ParseResult ConverterNeeded() => new ParseResult { Error = "This type is converted by the mod's own converter.", NeedsModConverter = true };

        public override string ToString() => Ok ? "Ok " + Text : "Error " + Error;
    }

    /// <summary>RGBA color as BepInEx writes it (<c>RRGGBBAA</c>, upper case, no '#').</summary>
    public readonly struct ColorValue : IEquatable<ColorValue>
    {
        public ColorValue(byte r, byte g, byte b, byte a) { R = r; G = g; B = b; A = a; }

        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public byte A { get; }

        public string ToHex() => R.ToString("X2", CultureInfo.InvariantCulture) + G.ToString("X2", CultureInfo.InvariantCulture)
            + B.ToString("X2", CultureInfo.InvariantCulture) + A.ToString("X2", CultureInfo.InvariantCulture);

        /// <summary>Accepts <c>RRGGBB</c> or <c>RRGGBBAA</c>, with or without '#'.</summary>
        public static bool TryParse(string? text, out ColorValue color)
        {
            color = default;
            var hex = (text ?? "").Trim();
            if (hex.StartsWith("#", StringComparison.Ordinal)) hex = hex.Substring(1);
            if (hex.Length != 6 && hex.Length != 8) return false;
            var bytes = new byte[4];
            bytes[3] = 0xFF;
            for (var i = 0; i < hex.Length / 2; i++)
                if (!byte.TryParse(hex.Substring(i * 2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out bytes[i])) return false;
            color = new ColorValue(bytes[0], bytes[1], bytes[2], bytes[3]);
            return true;
        }

        public bool Equals(ColorValue other) => R == other.R && G == other.G && B == other.B && A == other.A;
        public override bool Equals(object? obj) => obj is ColorValue other && Equals(other);
        public override int GetHashCode() => (R << 24) | (G << 16) | (B << 8) | A;
        public override string ToString() => ToHex();
    }

    /// <summary>
    /// A BepInEx <c>KeyboardShortcut</c> as text: main key first, then modifiers, joined with " + "; empty is "None".
    /// Equality ignores modifier order and case. Key names are Unity <c>KeyCode</c> names (no spaces).
    /// </summary>
    public sealed class ShortcutValue : IEquatable<ShortcutValue>
    {
        public static readonly ShortcutValue Empty = new ShortcutValue("", new string[0]);

        private ShortcutValue(string mainKey, IReadOnlyList<string> modifiers)
        {
            MainKey = mainKey;
            Modifiers = modifiers;
        }

        public string MainKey { get; }
        public IReadOnlyList<string> Modifiers { get; }
        public bool IsEmpty => MainKey.Length == 0;

        public static ShortcutValue Parse(string? text, IReadOnlyList<string>? knownKeys = null)
        {
            if (!TryParse(text, knownKeys, out var value, out var error)) throw new FormatException(error);
            return value;
        }

        public static bool TryParse(string? text, IReadOnlyList<string>? knownKeys, out ShortcutValue value, out string error)
        {
            value = Empty;
            error = "";
            var trimmed = (text ?? "").Trim();
            if (trimmed.Length == 0 || string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase)) return true;
            var keys = new List<string>();
            foreach (var part in trimmed.Split('+'))
            {
                var name = part.Trim();
                if (!KeyNames.TryCanonical(name, knownKeys, out var canonical, out error)) return false;
                if (string.Equals(canonical, "None", StringComparison.OrdinalIgnoreCase)) continue;
                if (!keys.Any(k => string.Equals(k, canonical, StringComparison.OrdinalIgnoreCase))) keys.Add(canonical);
            }
            if (keys.Count == 0) return true;
            value = new ShortcutValue(keys[0], keys.Skip(1).ToArray());
            return true;
        }

        public override string ToString() => IsEmpty ? "None" : string.Join(" + ", new[] { MainKey }.Concat(Modifiers));

        public bool Equals(ShortcutValue? other)
        {
            if (other is null) return false;
            if (!string.Equals(MainKey, other.MainKey, StringComparison.OrdinalIgnoreCase) || Modifiers.Count != other.Modifiers.Count) return false;
            return Modifiers.All(m => other.Modifiers.Any(o => string.Equals(m, o, StringComparison.OrdinalIgnoreCase)));
        }

        public override bool Equals(object? obj) => Equals(obj as ShortcutValue);
        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(MainKey) ^ Modifiers.Count;
    }

    /// <summary>Unity <c>KeyCode</c> name checks without Unity: identifier characters only; matched against a known list when given.</summary>
    public static class KeyNames
    {
        public static bool TryCanonical(string name, IReadOnlyList<string>? knownKeys, out string canonical, out string error)
        {
            canonical = "";
            error = "";
            if (name.Length == 0 || !name.All(c => char.IsLetterOrDigit(c) || c == '_'))
            {
                error = "Not a key name: '" + name + "'";
                return false;
            }
            if (knownKeys != null && knownKeys.Count > 0)
            {
                var match = knownKeys.FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                {
                    error = "Unknown key: '" + name + "'";
                    return false;
                }
                canonical = match;
                return true;
            }
            canonical = name;
            return true;
        }
    }

    /// <summary>BepInEx string escaping for config files (backslash escapes for control characters, quotes and backslash).</summary>
    public static class BepInExText
    {
        public static string Escape(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text!.Length + 2);
            foreach (var c in text)
            {
                switch (c)
                {
                    case '\0': sb.Append("\\0"); break;
                    case '\a': sb.Append("\\a"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\v': sb.Append("\\v"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\'': sb.Append("\\'"); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        public static string Unescape(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text!.IndexOf('\\') < 0) return text;
            var sb = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c != '\\' || i == text.Length - 1)
                {
                    sb.Append(c);
                    continue;
                }
                var next = text[++i];
                switch (next)
                {
                    case '0': sb.Append('\0'); break;
                    case 'a': sb.Append('\a'); break;
                    case 'b': sb.Append('\b'); break;
                    case 't': sb.Append('\t'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'v': sb.Append('\v'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'r': sb.Append('\r'); break;
                    case '\'': sb.Append('\''); break;
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    default: sb.Append('\\').Append(next); break;
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>String lists stored in one string setting with an <c>AlmanacHints.ListSeparator</c>.</summary>
    public static class ListText
    {
        public static IReadOnlyList<string> Split(string? text, string separator)
        {
            if (string.IsNullOrEmpty(text)) return new string[0];
            if (string.IsNullOrEmpty(separator)) return new[] { text!.Trim() };
            return text!.Split(new[] { separator }, StringSplitOptions.None).Select(s => s.Trim()).ToArray();
        }

        public static string Join(IEnumerable<string> items, string separator) => string.Join(separator ?? "", items);
    }
}
