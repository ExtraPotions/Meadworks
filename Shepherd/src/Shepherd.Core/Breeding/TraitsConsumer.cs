// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using TraitsProvider = System.Tuple<System.Func<object, string>, System.Func<string, string, int, float, int, string>,
    System.Action<object, string>, System.Func<string, string>, System.Func<string, string>>;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>The optional v1 BCL contract; Wildblood owns trait algorithms, Shepherd owns births and levels.</summary>
    public sealed class TraitsConsumer
    {
        public const string Id = "meadworks.creatures.traits";
        public static readonly Version Version = new Version(1, 0);
        private readonly TraitsProvider? provider;
        public TraitsConsumer(TraitsProvider? provider) { this.provider = provider; }
        public bool Available => provider != null;

        public string Read(object? target, string fallback = "")
        {
            if (target == null) return fallback;
            try { return provider?.Item1(target) ?? fallback; }
            catch (Exception) { return fallback; }
        }

        /// <param name="mutationChance">Shepherd's Mutation Chance percent; the provider applies the lower of it and its own chance.</param>
        public BirthPlan Inherit(BirthPlan plan, string mother, string sire, int seed, float mutationChance = 100f)
        {
            string inherited;
            float chance = float.IsNaN(mutationChance) ? 0f : Math.Max(0f, Math.Min(100f, mutationChance));
            try { inherited = provider?.Item2(mother, sire, plan.Level, chance, seed) ?? ""; }
            catch (Exception) { inherited = ""; }
            return new BirthPlan(plan.Inheritance, plan.Lineage, WithTraits(plan.Egg, inherited));
        }

        public void Apply(object target, string traits)
        {
            // Empty is valid plain inheritance: the provider must still mark the newborn resolved.
            try { provider?.Item3(target, traits); }
            catch (Exception) { }
        }

        public string EncodeEgg(EggRecord egg)
        {
            string encoded;
            try { encoded = provider?.Item4(egg.Traits) ?? egg.Traits; }
            catch (Exception) { encoded = egg.Traits; }
            return EggCodec.Encode(WithTraits(egg, encoded));
        }

        public OffspringCarry Hatch(EggRecord egg, string token)
        {
            string traits;
            try { traits = provider?.Item5(egg.Traits) ?? egg.Traits; }
            catch (Exception) { traits = egg.Traits; }
            bool lineage = egg.Generation != 0 || egg.BornDay != 0 || egg.MotherToken.Length != 0 || egg.SireToken.Length != 0
                || egg.MotherName.Length != 0 || egg.SireName.Length != 0 || egg.Breeder.Length != 0;
            return new OffspringCarry(lineage ? Lineage.Hatch(egg, token) : null, traits);
        }

        public string DecodeLegacyEgg(string text)
        {
            try { return provider?.Item5(text) ?? ""; }
            catch (Exception) { return ""; }
        }

        private static EggRecord WithTraits(EggRecord egg, string traits) => new EggRecord(egg.MotherToken, egg.SireToken,
            egg.MotherName, egg.SireName, egg.Generation, egg.BornDay, egg.Breeder, traits);
    }

    /// <summary>One existing growth/hatch claim may carry traits without recording lineage. It never rolls traits.</summary>
    public sealed class OffspringCarry
    {
        public OffspringCarry(LineageRecord? lineage, string traits) { Lineage = lineage; Traits = traits ?? ""; }
        public LineageRecord? Lineage { get; }
        public string Traits { get; }
    }
}
