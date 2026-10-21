// Meadworks-owned reuse: Almanac 64a81614deb039db71d88ff70803ee648d3317dd src/Almanac.Core/Values/ValueCodec.cs; namespace adaptation only.
// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Thing.ConfigEditing.Editors;

namespace ExtraPotions.Thing.ConfigEditing.Values
{
    /// <summary>
    /// Parses and validates BepInEx config text for the portable setting kinds (invariant culture, as BepInEx writes),
    /// returning the canonical text so a parse of the result is stable. Constraints: <c>AcceptableValueRange</c> clamps
    /// (as BepInEx does) and reports it; <c>AcceptableValueList</c> refuses values outside the list instead of silently
    /// substituting the first value. Kinds without a portable codec report <see cref="ParseResult.NeedsModConverter"/>.
    /// </summary>
    public static class ValueCodec
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static ParseResult Parse(SettingShape shape, string? text)
        {
            var result = ParseKind(shape, text ?? "");
            if (!result.Ok) return result;
            if (shape.HasRange && (shape.Kind == SettingKind.Integer || shape.Kind == SettingKind.Floating)) result = Clamp(shape, result);
            if (shape.AcceptableList != null) result = CheckList(shape, result);
            return result;
        }

        private static ParseResult ParseKind(SettingShape shape, string text)
        {
            switch (shape.Kind)
            {
                case SettingKind.Bool:
                    return bool.TryParse(text.Trim(), out var b) ? ParseResult.Success(b ? "true" : "false", b) : ParseResult.Fail("Expected true or false.");
                case SettingKind.Integer: return Integer(shape, text);
                case SettingKind.Floating: return Floating(shape, text);
                case SettingKind.String:
                    var raw = BepInExText.Unescape(text);
                    return ParseResult.Success(BepInExText.Escape(raw), raw);
                case SettingKind.Enum: return Enum(shape, text);
                case SettingKind.KeyCode:
                    return KeyNames.TryCanonical(text.Trim(), shape.KeyNames, out var key, out var keyError)
                        ? ParseResult.Success(key, key) : ParseResult.Fail(keyError);
                case SettingKind.KeyboardShortcut:
                    return ShortcutValue.TryParse(text, shape.KeyNames, out var chord, out var chordError)
                        ? ParseResult.Success(chord.ToString(), chord) : ParseResult.Fail(chordError);
                case SettingKind.Color:
                    return ColorValue.TryParse(text, out var color) ? ParseResult.Success(color.ToHex(), color) : ParseResult.Fail("Expected a color as RRGGBBAA hex.");
                case SettingKind.Vector2: return Vector(text, "xy");
                case SettingKind.Vector3: return Vector(text, "xyz");
                case SettingKind.Vector4:
                case SettingKind.Quaternion: return Vector(text, "xyzw");
                default: return ParseResult.ConverterNeeded();
            }
        }

        private static ParseResult Integer(SettingShape shape, string text)
        {
            if (!decimal.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, Inv, out var value))
                return ParseResult.Fail("Expected a whole number.");
            if (!IntegerBounds(shape.ShortTypeName, out var min, out var max)) { min = long.MinValue; max = ulong.MaxValue; }
            if (value < min || value > max)
                return ParseResult.Fail("Expected a whole number from " + min.ToString(Inv) + " to " + max.ToString(Inv) + ".");
            return ParseResult.Success(value.ToString(Inv), value);
        }

        private static bool IntegerBounds(string type, out decimal min, out decimal max)
        {
            switch (type)
            {
                case "SByte": min = sbyte.MinValue; max = sbyte.MaxValue; return true;
                case "Byte": min = byte.MinValue; max = byte.MaxValue; return true;
                case "Int16": min = short.MinValue; max = short.MaxValue; return true;
                case "UInt16": min = ushort.MinValue; max = ushort.MaxValue; return true;
                case "Int32": min = int.MinValue; max = int.MaxValue; return true;
                case "UInt32": min = uint.MinValue; max = uint.MaxValue; return true;
                case "Int64": min = long.MinValue; max = long.MaxValue; return true;
                case "UInt64": min = ulong.MinValue; max = ulong.MaxValue; return true;
                default: min = 0; max = 0; return false;
            }
        }

        private static ParseResult Floating(SettingShape shape, string text)
        {
            var trimmed = text.Trim();
            if (shape.ShortTypeName == "Decimal")
                return decimal.TryParse(trimmed, NumberStyles.Float, Inv, out var m) ? ParseResult.Success(m.ToString(Inv), m) : ParseResult.Fail("Expected a number such as 1.5.");
            if (!double.TryParse(trimmed, NumberStyles.Float, Inv, out var d) || double.IsNaN(d) || double.IsInfinity(d))
                return ParseResult.Fail("Expected a number such as 1.5.");
            if (shape.ShortTypeName == "Single")
            {
                if (Math.Abs(d) > float.MaxValue) return ParseResult.Fail("The number is too large.");
                var f = (float)d;
                return ParseResult.Success(f.ToString(Inv), (double)f);
            }
            return ParseResult.Success(d.ToString("R", Inv), d);
        }

        private static ParseResult Clamp(SettingShape shape, ParseResult parsed)
        {
            switch (parsed.Value)
            {
                case decimal m:
                    var min = (decimal)shape.RangeMin;
                    var max = (decimal)shape.RangeMax;
                    if (m >= min && m <= max) return parsed;
                    var clamped = m < min ? min : max;
                    var text = shape.Kind == SettingKind.Integer ? decimal.Truncate(clamped).ToString(Inv) : clamped.ToString(Inv);
                    return ParseResult.Success(text, clamped, clamped: true);
                case double d:
                    if (d >= shape.RangeMin && d <= shape.RangeMax) return parsed;
                    var c = d < shape.RangeMin ? shape.RangeMin : shape.RangeMax;
                    var formatted = shape.ShortTypeName == "Single" ? ((float)c).ToString(Inv) : c.ToString("R", Inv);
                    return ParseResult.Success(formatted, c, clamped: true);
                default:
                    return parsed;
            }
        }

        private static ParseResult CheckList(SettingShape shape, ParseResult parsed)
        {
            // ParseKind ignores constraints, so canonicalising an allowed value never recurses into this check.
            foreach (var allowed in shape.AcceptableList!)
            {
                var canonical = ParseKind(shape, allowed);
                var allowedText = canonical.Ok ? canonical.Text : allowed;
                if (string.Equals(allowedText, parsed.Text, StringComparison.Ordinal)) return parsed;
            }
            return ParseResult.Fail("Allowed values: " + string.Join(", ", shape.AcceptableList!) + ".");
        }

        private static ParseResult Enum(SettingShape shape, string text)
        {
            var names = shape.EnumNames;
            if (!shape.IsFlags)
            {
                var name = names.FirstOrDefault(n => string.Equals(n, text.Trim(), StringComparison.OrdinalIgnoreCase));
                return name != null ? ParseResult.Success(name, name) : ParseResult.Fail("Allowed values: " + string.Join(", ", names) + ".");
            }
            var chosen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var part in text.Split(','))
            {
                var name = names.FirstOrDefault(n => string.Equals(n, part.Trim(), StringComparison.OrdinalIgnoreCase));
                if (name == null) return ParseResult.Fail("Unknown flag '" + part.Trim() + "'. Allowed: " + string.Join(", ", names) + ".");
                chosen.Add(name);
            }
            if (chosen.Count > 1) chosen.Remove("None");
            var ordered = names.Where(chosen.Contains).ToArray();
            return ParseResult.Success(string.Join(", ", ordered), ordered);
        }

        private static ParseResult Vector(string text, string axes)
        {
            var body = text.Trim();
            if (body.Length < 2 || body[0] != '{' || body[body.Length - 1] != '}') return ParseResult.Fail("Expected {\"x\":1.0,...}.");
            body = body.Substring(1, body.Length - 2);
            var values = new Dictionary<char, float>();
            foreach (var pair in body.Split(','))
            {
                var colon = pair.IndexOf(':');
                if (colon < 0) return ParseResult.Fail("Expected \"axis\":number pairs.");
                var name = pair.Substring(0, colon).Trim();
                if (name.Length != 3 || name[0] != '"' || name[2] != '"' || axes.IndexOf(name[1]) < 0 || values.ContainsKey(name[1]))
                    return ParseResult.Fail("Unexpected component " + name + ".");
                if (!float.TryParse(pair.Substring(colon + 1).Trim(), NumberStyles.Float, Inv, out var component) || float.IsNaN(component) || float.IsInfinity(component))
                    return ParseResult.Fail("Component " + name + " is not a number.");
                values[name[1]] = component;
            }
            if (values.Count != axes.Length) return ParseResult.Fail("Expected components " + string.Join(", ", axes.ToCharArray()) + ".");
            var ordered = axes.Select(a => values[a]).ToArray();
            var json = "{" + string.Join(",", axes.Select((a, i) => "\"" + a + "\":" + JsonFloat(ordered[i]))) + "}";
            return ParseResult.Success(json, ordered);
        }

        /// <summary>Unity <c>JsonUtility</c> style: whole numbers keep ".0". Exact native byte form is verified in A08.</summary>
        private static string JsonFloat(float value)
        {
            var text = value.ToString("R", Inv);
            return text.IndexOfAny(new[] { '.', 'E', 'e' }) >= 0 ? text : text + ".0";
        }
    }
}
