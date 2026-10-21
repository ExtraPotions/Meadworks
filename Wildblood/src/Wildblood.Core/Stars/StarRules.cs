// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: world tier (design A3), spawn source decisions (A10, CreatureManager's source kinds by name),
// scope precedence (A4), enemy level-up rate (A13), Max Stars clamp, protected prefabs (A11) and the star decision.
using System;
using System.Linq;
using ExtraPotions.Wildblood.Config;

namespace ExtraPotions.Wildblood.Stars
{
    /// <summary>World tier: one per listed boss key set in the world (by anyone), unless overridden; 0 when disabled.</summary>
    public static class WorldTier
    {
        public static int Compute(bool enabled, string bossKeys, int overrideTier, Func<string, bool> isKeySet)
        {
            if (isKeySet == null) throw new ArgumentNullException(nameof(isKeySet));
            if (!enabled) return 0;
            if (overrideTier >= 0) return overrideTier;
            return NameList.Parse(bossKeys).Count(k => isKeySet(k.ToLowerInvariant()));
        }

        /// <summary>The world hardened: the tier rose since it was last seen ("The world hardens").</summary>
        public static bool Hardened(int previous, int current) => current > previous;
    }

    /// <summary>How a creature came into the world (set by a one-call context around the vanilla spawn paths).</summary>
    public enum SpawnSource { Natural, Spawner, SpawnArea, Ability, BossAltar, Command, PlayerSummon, Breeding, Egg, Growup, TamedRestore, Unknown }

    public static class SpawnSources
    {
        /// <summary>Natural spawns roll; births, eggs, grow-up, tamed restore, player summons and commands never reroll.</summary>
        public static bool Rolls(SpawnSource source)
        {
            switch (source)
            {
                case SpawnSource.Command:
                case SpawnSource.PlayerSummon:
                case SpawnSource.Breeding:
                case SpawnSource.Egg:
                case SpawnSource.Growup:
                case SpawnSource.TamedRestore:
                    return false;
                default:
                    return true;
            }
        }
    }

    /// <summary>Star weight rows: precedence, enemy level-up rate, Max Stars and the pick.</summary>
    public static class StarWeights
    {
        /// <summary>The narrowest non-empty row: prefab over group over biome over global.</summary>
        public static float[]? Resolve(float[]? prefab, float[]? group, float[]? biome, float[]? global)
        {
            foreach (var row in new[] { prefab, group, biome, global })
                if (row != null && row.Length > 0) return row;
            return null;
        }

        /// <summary>The world modifier's enemy level-up rate multiplies the weight of every starred count.</summary>
        public static float[] LevelUpRate(float[] weights, float rate)
        {
            var r = float.IsNaN(rate) || rate < 0f ? 1f : rate;
            return weights.Select((w, stars) => stars == 0 ? w : w * r).ToArray();
        }

        /// <summary>Max Stars: weights above the cap fold into the cap, so a creature that would roll higher rolls the cap.</summary>
        public static float[] Cap(float[] weights, int maxStars)
        {
            var cap = Math.Max(0, maxStars);
            if (weights.Length <= cap + 1) return (float[])weights.Clone();
            var result = new float[cap + 1];
            Array.Copy(weights, result, cap + 1);
            for (var i = cap + 1; i < weights.Length; i++) result[cap] += weights[i];
            return result;
        }

        /// <summary>
        /// The weights a roll draws from: the world's enemy level-up rate first (when Respect Enemy Level Up Rate is on),
        /// then the Max Stars fold. The rate comes from the game's <c>Game.m_enemyLevelUpRate</c>.
        /// </summary>
        public static float[] Effective(float[] weights, int maxStars, bool respectLevelUpRate, float levelUpRate) =>
            Cap(respectLevelUpRate ? LevelUpRate(weights, levelUpRate) : weights, maxStars);

        public static int Pick(float[] weights, IDice dice) => dice.PickWeighted(weights);
    }

    /// <summary>The inputs of one creature's star decision.</summary>
    public readonly struct StarRequest
    {
        public StarRequest(StarMode mode, SpawnSource source, bool resolved, bool protectedPrefab, int existingLevel, float[] weights, int maxStars)
        {
            Mode = mode;
            Source = source;
            Resolved = resolved;
            ProtectedPrefab = protectedPrefab;
            ExistingLevel = existingLevel;
            Weights = weights ?? throw new ArgumentNullException(nameof(weights));
            MaxStars = maxStars;
        }

        public StarMode Mode { get; }
        public SpawnSource Source { get; }

        /// <summary>The creature was rolled before (ZDO <c>wildblood.resolved</c>): never reroll.</summary>
        public bool Resolved { get; }
        public bool ProtectedPrefab { get; }
        public int ExistingLevel { get; }

        /// <summary>The star weights already resolved for this creature (preset cell or Custom rows, rate applied).</summary>
        public float[] Weights { get; }
        public int MaxStars { get; }

        public StarRequest With(StarMode mode, int existingLevel, bool? resolved = null, bool? protectedPrefab = null, SpawnSource? source = null, int? maxStars = null) =>
            new StarRequest(mode, source ?? Source, resolved ?? Resolved, protectedPrefab ?? ProtectedPrefab, existingLevel, Weights, maxStars ?? MaxStars);
    }

    /// <summary>The stars Wildblood applies star power for, and whether this call rolled them (then the level is written).</summary>
    public readonly struct StarDecision : IEquatable<StarDecision>
    {
        public StarDecision(int stars, bool rolled)
        {
            Stars = stars;
            Rolled = rolled;
        }

        public int Stars { get; }
        public bool Rolled { get; }

        public bool Equals(StarDecision other) => Stars == other.Stars && Rolled == other.Rolled;
        public override bool Equals(object? obj) => obj is StarDecision other && Equals(other);
        public override int GetHashCode() => Stars * 2 + (Rolled ? 1 : 0);
        public override string ToString() => Stars + (Rolled ? " stars (rolled)" : " stars (kept)");
    }

    public static class StarRoller
    {
        /// <summary>
        /// Protected prefabs and Off: no Wildblood stars (the level stays as it is). Keep Existing, resolved creatures and
        /// non-rolling sources: the level the creature has. Otherwise a pick from the weights capped by Max Stars.
        /// </summary>
        public static StarDecision Decide(StarRequest request, IDice dice)
        {
            if (request.ProtectedPrefab || request.Mode == StarMode.Off) return new StarDecision(0, false);
            if (request.Mode == StarMode.KeepExisting || request.Resolved || !SpawnSources.Rolls(request.Source))
                return new StarDecision(StarMath.StarsOfLevel(request.ExistingLevel), false);
            return new StarDecision(StarWeights.Pick(StarWeights.Cap(request.Weights, request.MaxStars), dice), true);
        }
    }
}
