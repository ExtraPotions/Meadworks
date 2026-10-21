// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>
    /// The egg record text: <c>v1;mother;sire;motherName;sireName;gen;born;breeder;traits</c> (design 10.1). Every
    /// field is escaped (<c>\\</c> for a backslash, <c>\;</c> for a separator) and the whole text is at most
    /// <see cref="MaxLength"/> characters: an oversized traits blob is dropped first, then names are shortened to
    /// <see cref="TrimmedNameLength"/>. Any other version, shape or number decodes to nothing (the egg hatches with the
    /// game's behavior).
    /// </summary>
    public static class EggCodec
    {
        public const int MaxLength = 1024;
        public const int TrimmedNameLength = 64;
        private const string Version = "v1";
        private const int FieldCount = 9;

        public static string Encode(EggRecord egg)
        {
            if (egg == null) throw new ArgumentNullException(nameof(egg));
            string text = Join(egg, egg.Traits, int.MaxValue);
            if (text.Length > MaxLength) text = Join(egg, "", int.MaxValue);
            if (text.Length > MaxLength) text = Join(egg, "", TrimmedNameLength);
            if (text.Length > MaxLength) throw new ArgumentException("The egg's parent tokens do not fit in " + MaxLength + " characters.", nameof(egg));
            return text;
        }

        public static bool TryDecode(string? text, out EggRecord? egg)
        {
            egg = null;
            if (string.IsNullOrEmpty(text) || text!.Length > MaxLength) return false;
            var fields = Split(text);
            if (fields == null || fields.Count != FieldCount || fields[0] != Version) return false;
            if (!NonNegative(fields[5], out int generation) || !NonNegative(fields[6], out int born)) return false;
            egg = new EggRecord(fields[1], fields[2], fields[3], fields[4], generation, born, fields[7], fields[8]);
            return true;
        }

        private static string Join(EggRecord egg, string traits, int nameLength)
        {
            var builder = new StringBuilder(Version);
            foreach (var field in new[]
            {
                egg.MotherToken, egg.SireToken, Trim(egg.MotherName, nameLength), Trim(egg.SireName, nameLength),
                egg.Generation.ToString(CultureInfo.InvariantCulture), egg.BornDay.ToString(CultureInfo.InvariantCulture),
                Trim(egg.Breeder, nameLength), traits,
            })
            {
                builder.Append(';');
                foreach (char c in field)
                {
                    if (c == '\\' || c == ';') builder.Append('\\');
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }

        private static string Trim(string text, int length) => text.Length <= length ? text : text.Substring(0, length);

        private static List<string>? Split(string text)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\')
                {
                    if (i + 1 >= text.Length || (text[i + 1] != '\\' && text[i + 1] != ';')) return null;
                    current.Append(text[++i]);
                }
                else if (c == ';')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(c);
            }
            fields.Add(current.ToString());
            return fields;
        }

        private static bool NonNegative(string text, out int value) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
