// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Reimplemented from ProgressivePowers 0.3.6 behavior only (README "Attuning to several powers";
// no code): attunement slots at the boss stones, stored as Saga character data.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Saga.Powers
{
    public enum AttuneKind { Attune, Replace, Release }

    /// <summary>What using a boss stone would do, so the stone can name the replaced power before the player commits.</summary>
    public readonly struct AttunePreview
    {
        public AttunePreview(AttuneKind kind, Boss? replaced)
        {
            Kind = kind;
            Replaced = replaced;
        }

        public AttuneKind Kind { get; }
        public Boss? Replaced { get; }
    }

    /// <summary>
    /// A character's attunements (design A2), oldest first, plus the powers set aside when the limit was lowered. Using the
    /// stone of a held power releases it; another stone attunes, replacing the oldest when every slot is taken. Lowering
    /// the limit sets the oldest aside without forgetting them and raising it restores them; they are dropped only when
    /// the player attunes or releases under the lower limit. Immutable.
    /// </summary>
    public sealed class Attunement
    {
        public const string AttunedKey = SagaCore.DataPrefix + "powers.attuned";
        public const string AsideKey = SagaCore.DataPrefix + "powers.aside";

        private Attunement(IReadOnlyList<Boss> attuned, IReadOnlyList<Boss> aside)
        {
            Attuned = attuned;
            Aside = aside;
        }

        public static Attunement Empty { get; } = new Attunement(Array.Empty<Boss>(), Array.Empty<Boss>());

        /// <summary>Active attunements, oldest first.</summary>
        public IReadOnlyList<Boss> Attuned { get; }

        /// <summary>Attunements set aside by a lower limit, oldest first.</summary>
        public IReadOnlyList<Boss> Aside { get; }

        private static int Clamp(int limit) => Math.Min(8, Math.Max(1, limit));

        /// <summary>The state under a limit: the newest attunements up to the limit are active, older ones aside.</summary>
        public Attunement Limit(int limit)
        {
            var all = Aside.Concat(Attuned).ToList();
            var keep = Math.Min(all.Count, Clamp(limit));
            return new Attunement(all.Skip(all.Count - keep).ToList(), all.Take(all.Count - keep).ToList());
        }

        public AttunePreview Preview(Boss stone, int limit)
        {
            var current = Limit(limit);
            if (current.Attuned.Contains(stone)) return new AttunePreview(AttuneKind.Release, null);
            return current.Attuned.Count >= Clamp(limit) ? new AttunePreview(AttuneKind.Replace, current.Attuned[0]) : new AttunePreview(AttuneKind.Attune, null);
        }

        /// <summary>The state after using <paramref name="stone"/> under <paramref name="limit"/>; anything set aside is dropped.</summary>
        public Attunement Use(Boss stone, int limit)
        {
            var current = Limit(limit).Attuned.ToList();
            if (current.Remove(stone)) return new Attunement(current, Array.Empty<Boss>());
            if (current.Count >= Clamp(limit)) current.RemoveAt(0);
            current.Add(stone);
            return new Attunement(current, Array.Empty<Boss>());
        }

        public string AttunedText => Write(Attuned);
        public string AsideText => Write(Aside);

        private static string Write(IEnumerable<Boss> bosses) => string.Join(",", bosses.Select(PowerCatalog.Name));

        /// <summary>
        /// <see cref="Read"/> then <see cref="Limit"/>, reused while both stored texts and the limit are unchanged. The power HUD
        /// asks for this every frame; parsing and limiting allocated each time.
        /// </summary>
        public static Attunement ReadLimited(string? attuned, string? aside, int limit)
        {
            var memo = lastRead;
            if (memo != null && memo.Limit == limit && string.Equals(memo.Attuned, attuned, StringComparison.Ordinal) && string.Equals(memo.Aside, aside, StringComparison.Ordinal))
                return memo.State;
            var state = Read(attuned, aside).Limit(limit);
            lastRead = new ReadMemo(attuned, aside, limit, state);
            return state;
        }

        private sealed class ReadMemo
        {
            internal ReadMemo(string? attuned, string? aside, int limit, Attunement state) { Attuned = attuned; Aside = aside; Limit = limit; State = state; }
            internal readonly string? Attuned, Aside;
            internal readonly int Limit;
            internal readonly Attunement State;
        }

        private static ReadMemo? lastRead;

        /// <summary>Reads the two character data values; unknown names and repeats are dropped, a power both attuned and aside counts as attuned.</summary>
        public static Attunement Read(string? attuned, string? aside)
        {
            var active = Parse(attuned);
            return new Attunement(active, Parse(aside).Where(b => !active.Contains(b)).ToList());
        }

        private static List<Boss> Parse(string? text)
        {
            var list = new List<Boss>();
            foreach (var part in (text ?? "").Split(','))
                if (PowerCatalog.TryParse(part, out var boss) && part.Trim() == PowerCatalog.Name(boss) && !list.Contains(boss)) list.Add(boss);
            return list;
        }
    }
}
