// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the trait blob of capability meadworks.creatures.traits v1 (design 9.1) and the egg escape.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ExtraPotions.Wildblood.Stars;

namespace ExtraPotions.Wildblood.Traits
{
    /// <summary>A creature's traits: mutation mask (EliteCreaturesReborn bit order), modifiers in catalog order, origin biome.</summary>
    public sealed class TraitSet : IEquatable<TraitSet>
    {
        public TraitSet(int mutations, IEnumerable<Modifier> modifiers, Biome biome)
        {
            Mutations = mutations & Traits.Mutations.AllBits;
            Modifiers = (modifiers ?? throw new ArgumentNullException(nameof(modifiers))).Distinct().OrderBy(m => m).ToList();
            Biome = biome;
        }

        public static TraitSet Plain(Biome biome) => new TraitSet(0, Array.Empty<Modifier>(), biome);

        public int Mutations { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
        public Biome Biome { get; }

        public bool IsPlain => Mutations == 0 && Modifiers.Count == 0;

        public bool Equals(TraitSet? other) =>
            other != null && Mutations == other.Mutations && Biome == other.Biome && Modifiers.SequenceEqual(other.Modifiers);

        public override bool Equals(object? obj) => Equals(obj as TraitSet);

        public override int GetHashCode() => (Mutations, Biome, Modifiers.Count).GetHashCode();

        public override string ToString() => TraitBlob.Encode(this);
    }

    /// <summary>
    /// <c>wb1;m=&lt;mutation ids&gt;;c=&lt;modifier ids&gt;;b=&lt;biome&gt;</c>, ids comma separated, at most 256 characters; ""
    /// for a creature with no traits. Decoding skips unknown ids (a newer Wildblood's traits), refuses other versions, and
    /// accepts EliteCreaturesReborn's legacy <c>ecr:&lt;mask&gt;</c>.
    /// </summary>
    public static class TraitBlob
    {
        public const string Version = "wb1";
        public const int MaxLength = 256;
        public const string LegacyPrefix = "ecr:";

        public static string Encode(TraitSet traits)
        {
            if (traits == null) throw new ArgumentNullException(nameof(traits));
            if (traits.IsPlain) return "";
            var text = Version + ";m=" + string.Join(",", Mutations.Of(traits.Mutations).Select(Mutations.Id))
                + ";c=" + string.Join(",", traits.Modifiers.Select(Modifiers.Id))
                + ";b=" + BiomeId(traits.Biome);
            if (text.Length > MaxLength) throw new ArgumentException("Trait blob longer than " + MaxLength + " characters.", nameof(traits));
            return text;
        }

        public static bool TryDecode(string? blob, out TraitSet traits)
        {
            traits = TraitSet.Plain(Biome.Other);
            if (string.IsNullOrEmpty(blob)) return true;
            if (blob!.Length > MaxLength) return false;
            if (blob.StartsWith(LegacyPrefix, StringComparison.Ordinal))
            {
                if (!int.TryParse(blob.Substring(LegacyPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mask) || mask < 0) return false;
                traits = new TraitSet(mask, Array.Empty<Modifier>(), Biome.Other);
                return true;
            }
            var parts = blob.Split(';');
            if (parts[0] != Version) return false;
            var mutations = 0;
            var modifiers = new List<Modifier>();
            var biome = Biome.Other;
            foreach (var part in parts.Skip(1))
            {
                var eq = part.IndexOf('=');
                if (eq < 0) continue;
                var key = part.Substring(0, eq);
                var ids = part.Substring(eq + 1).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                switch (key)
                {
                    case "m":
                        foreach (var id in ids) if (Mutations.TryParseId(id, out var m)) mutations |= Mutations.Bit(m);
                        break;
                    case "c":
                        foreach (var id in ids) if (Modifiers.TryParseId(id, out var c)) modifiers.Add(c);
                        break;
                    case "b":
                        biome = ParseBiome(ids.FirstOrDefault());
                        break;
                }
            }
            traits = new TraitSet(mutations, modifiers, biome);
            return true;
        }

        public static string BiomeId(Biome biome) => biome.ToString().ToLowerInvariant();

        public static Biome ParseBiome(string? id)
        {
            foreach (Biome b in Enum.GetValues(typeof(Biome)))
                if (string.Equals(BiomeId(b), (id ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) return b;
            return Biome.Other;
        }

        /// <summary>Escapes a blob for Shepherd's egg record: letters, digits, '.', '_' and '-' stay; anything else is %XX (UTF-8).</summary>
        public static string EggEncode(string blob)
        {
            var sb = new StringBuilder();
            foreach (var b in Encoding.UTF8.GetBytes(blob ?? ""))
            {
                var c = (char)b;
                if (b < 128 && (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-')) sb.Append(c);
                else sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>The blob back from an egg record; "" when the escape is broken.</summary>
        public static string EggDecode(string egg)
        {
            var bytes = new List<byte>();
            var s = egg ?? "";
            for (var i = 0; i < s.Length; i++)
            {
                if (s[i] != '%') { bytes.Add((byte)s[i]); continue; }
                if (i + 2 >= s.Length || !byte.TryParse(s.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b)) return "";
                bytes.Add(b);
                i += 2;
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }
    }
}
