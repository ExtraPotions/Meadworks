// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from GrindstoneSkills 0.14.1, Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later (ValheimMods monorepo at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Husbandry/Experience/HerdExperience.cs,
// HerdSnapshot.cs and TameDiscovery.cs). Full notice in THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
//
// Changed by expDARE, 2026: Unity-free (doubles instead of Mathf); settings passed in as ExperienceRates; the
// discovery record works on the character's custom data dictionary under a Shepherd key and honours the
// GrindstoneSkills list read-only, so a kind tamed before the switch is no new discovery.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Shepherd.Husbandry
{
    /// <summary>
    /// What a keeper's client last saw of one creature, all from its replicated ZDO: taming time left, taming time,
    /// tamed, the game's feeding stamp and Shepherd's birth counter.
    /// </summary>
    public readonly struct HerdSnapshot
    {
        public HerdSnapshot(float tamingLeft, float tamingTime, bool tamed, long lastFed, int births)
        {
            TamingTime = Math.Max(1f, tamingTime);
            Tamed = tamed;
            TamingLeft = tamed ? 0f : tamingLeft;
            LastFed = lastFed;
            Births = births;
        }

        public float TamingLeft { get; }
        public float TamingTime { get; }
        public bool Tamed { get; }
        public long LastFed { get; }
        public int Births { get; }

        /// <summary>Untamed with taming under way (tameness above 0%).</summary>
        public bool BeingTamed => !Tamed && TamingLeft < TamingTime;
    }

    /// <summary>The experience settings (design 4.8), before the creature tier and the Experience Multiplier.</summary>
    public readonly struct ExperienceRates
    {
        public ExperienceRates(double taming, double tamed, double discovery, double feeding, double birth)
        {
            Taming = Math.Max(0, taming); Tamed = Math.Max(0, tamed); Discovery = Math.Max(1, discovery);
            Feeding = Math.Max(0, feeding); Birth = Math.Max(0, birth);
        }

        public double Taming { get; }
        public double Tamed { get; }
        public double Discovery { get; }
        public double Feeding { get; }
        public double Birth { get; }
    }

    /// <summary>
    /// The experience between two snapshots of a creature, before its tier: a share of Taming Experience for the taming
    /// progress, Tamed Experience when it became tame while being tamed (times the Discovery Multiplier for the
    /// character's first of that kind), Feeding Experience when it ate while tamed or being tamed, and Birth Experience
    /// per birth (at most four per scan). Runs on the keeper's own client.
    /// </summary>
    public static class HerdExperience
    {
        public const int MaxBirthsPerScan = 4;

        /// <param name="firstOfKind">Records the kind for the character and answers whether it was the first; asked only on a tame.</param>
        public static double Between(HerdSnapshot before, HerdSnapshot now, ExperienceRates rates, Func<bool> firstOfKind)
        {
            if (firstOfKind == null) throw new ArgumentNullException(nameof(firstOfKind));
            double amount = Taming(before, now, rates) + Tamed(before, now, rates, firstOfKind);
            if (now.LastFed != before.LastFed && now.LastFed != 0L && (now.Tamed || now.BeingTamed))
                amount += rates.Feeding;
            int births = Math.Min(MaxBirthsPerScan, Math.Max(0, now.Births - before.Births));
            return amount + births * rates.Birth;
        }

        private static double Taming(HerdSnapshot before, HerdSnapshot now, ExperienceRates rates)
        {
            if (before.Tamed || now.Tamed || now.TamingLeft >= before.TamingLeft)
                return 0;
            return (double)(before.TamingLeft - now.TamingLeft) / now.TamingTime * rates.Taming;
        }

        private static double Tamed(HerdSnapshot before, HerdSnapshot now, ExperienceRates rates, Func<bool> firstOfKind)
        {
            if (!now.Tamed || !before.BeingTamed)
                return 0;
            return firstOfKind() ? rates.Tamed * rates.Discovery : rates.Tamed;
        }
    }

    /// <summary>
    /// The kinds of creature a character has tamed, for the discovery multiplier: prefab names, comma-separated, in the
    /// player's custom data under <see cref="Key"/>, saved with the character. GrindstoneSkills' own list
    /// (<see cref="GrindstoneKey"/>) is read but never edited.
    /// </summary>
    public static class TameDiscovery
    {
        public const string Key = "shepherd.tamed_kinds";
        public const string GrindstoneKey = "grindstone_tamed";
        private const char Separator = ',';

        public static bool HasTamed(IDictionary<string, string> data, string prefab) =>
            data != null && !string.IsNullOrEmpty(prefab) && (Listed(data, Key, prefab) || Listed(data, GrindstoneKey, prefab));

        /// <summary>Records the kind as tamed. True when this was the character's first of that kind.</summary>
        public static bool TryRecord(IDictionary<string, string> data, string prefab)
        {
            if (data == null || string.IsNullOrEmpty(prefab) || prefab.IndexOf(Separator) >= 0 || HasTamed(data, prefab))
                return false;
            data.TryGetValue(Key, out string? tamed);
            data[Key] = string.IsNullOrEmpty(tamed) ? prefab : tamed + Separator + prefab;
            return true;
        }

        private static bool Listed(IDictionary<string, string> data, string key, string prefab) =>
            data.TryGetValue(key, out string? list) && Array.IndexOf((list ?? "").Split(Separator), prefab) >= 0;
    }
}
