// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original decisions for the ECR 3.30.2 tamed-star and raid-cap behavior.
using System;

namespace ExtraPotions.Wildblood.Stars
{
    /// <summary>
    /// Tamed Stars gates a new birth. When it is on, a tame an older build parked at the game's level
    /// goes back to its stored star roll. Taming does not change stars a creature already has, and turning
    /// the setting off does not take stars away. Raid extra stars stop at the biome cap. A remote client
    /// waits for the server's rules.
    /// </summary>
    public static class TamedStars
    {
        public const string OffspringCapability = "meadworks.wildblood.offspring_stars";
        public static readonly Version OffspringVersion = new Version(1, 0);

        /// <summary>The same cap the live controller publishes: stars 0 through 8 are levels 1 through 9.</summary>
        public const int MaxPublishedStars = 8;

        /// <summary>True when a new birth may take star inheritance. Both switches have to be on.</summary>
        public static bool InheritOffspring(bool starsKept, bool creatureStars) => starsKept && creatureStars;

        /// <summary>Identifies birth sources. Fresh offspring keep their assigned level; parking metadata permits a later restore.</summary>
        public static bool KeepsBirthLevel(SpawnSource source) =>
            source == SpawnSource.Breeding || source == SpawnSource.Egg || source == SpawnSource.Growup;

        /// <summary>
        /// When Tamed Stars and creature stars are on, a tamed creature with a parked roll below its stored stars
        /// publishes that roll. The result never lowers the level the creature already has. No parked roll,
        /// a boss, or a kept native level stays as it is. A mark still set at the stored level applies once more so an
        /// interrupted write can finish, then the caller clears the mark.
        /// </summary>
        public static ParkedStarRestore ParkedRestore(
            bool tamedStarsOn, bool creatureStarsOn, bool tamed, bool boss, bool keepsNativeLevel, bool birth, bool resolved,
            int currentLevel, int storedStars, int parkedGameLevel, bool parkedMark)
        {
            // Birth source is retained for callers; parking metadata, rather than source, controls a later restore.
            if (!tamedStarsOn || !creatureStarsOn || !tamed || boss || keepsNativeLevel || !resolved)
                return default;
            if (parkedGameLevel <= 0 && !parkedMark) return default;
            if (currentLevel < 1) currentLevel = 1;
            if (!parkedMark && currentLevel != parkedGameLevel) return default;
            int stars = storedStars < 0 ? 0 : storedStars;
            if (stars > MaxPublishedStars) stars = MaxPublishedStars;
            int level = stars + 1;
            if (level < currentLevel) return default;
            if (level == currentLevel && !parkedMark) return default;
            return new ParkedStarRestore(true, level);
        }

        /// <summary>Share of the bar to keep. A full bar stays full. No health stays empty. A bad reading stays full.</summary>
        public static float HealthFraction(float health, float max)
        {
            if (float.IsNaN(health) || float.IsInfinity(health)) return 1f;
            if (health <= 0f) return 0f;
            if (float.IsNaN(max) || float.IsInfinity(max) || max <= 0f) return 1f;
            if (health >= max) return 1f;
            return health / max;
        }

        /// <summary>A remote client still has its own saved rules until the server configuration package arrives.</summary>
        public static bool MayAdoptRules(bool remoteClient, bool authorityReady) => !remoteClient || authorityReady;

        /// <summary>Raid extra stars stop at the biome cap. A non-positive extra leaves the roll unchanged.</summary>
        public static int ClampRaiderStars(int stars, int extra, int cap)
        {
            if (extra <= 0) return stars;
            if (cap < 0) cap = 0;
            return Math.Max(stars, Math.Min(stars + extra, cap));
        }
    }

    /// <summary>Whether a parked tame should publish <see cref="Level"/>, and that level. Apply false leaves the creature alone.</summary>
    public readonly struct ParkedStarRestore
    {
        public ParkedStarRestore(bool apply, int level)
        {
            Apply = apply;
            Level = level;
        }

        public bool Apply { get; }
        public int Level { get; }
    }
}
