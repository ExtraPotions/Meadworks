// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Bestiary.Config;

namespace ExtraPotions.Bestiary.Spawning
{
    /// <summary>Prefab ids for the four creatures on existing bodies. The ids are permanent: renaming them deletes them from old worlds.</summary>
    public static class KitCreatures
    {
        public const string BogHook = "MW_BogHook";
        public const string Javelineer = "MW_FulingJavelineer";
        public const string SlagCharred = "MW_SlagCharred";
        public const string MarrowLantern = "MW_MarrowLantern";

        public const string Draugr = "Draugr";
        public const string Fuling = "Goblin";
        public const string Charred = "Charred_Melee";
        public const string DeepNorthSkeleton = "Skeleton_DeepNorth";
        public const string ShadowPerson = "ShadowPerson";

        /// <summary>Spawn level written on the replacement. Wildblood applies stars afterwards, if it is installed.</summary>
        public const int SpawnLevel = 1;
    }

    /// <summary>
    /// A share of one wild spawn. Elites, brutes, shamans, spawners, piles and raids are other prefabs or other
    /// spawn paths, so an exact prefab match leaves them alone.
    /// </summary>
    public static class WildShare
    {
        public static bool Replace(bool enabled, string wanted, string prefab, bool fromSpawner, bool eventSpawn,
            BiomeMask biomes, BiomeMask here, float sharePercent, double roll)
        {
            if (!enabled || fromSpawner || eventSpawn || prefab != wanted || sharePercent <= 0f) return false;
            return (biomes & here) != 0 && roll < sharePercent / 100.0;
        }
    }

    /// <summary>Which Ashlands regions hold one Slag Charred. The hash is salted so it does not follow the Rime Giant's mountains.</summary>
    public static class SlagRegions
    {
        public const int MinEdge = 40;
        public const float Apart = 1000f;
        private const uint Salt = 0x534C4147;

        public static string ClaimKey(int region) => BestiaryKeys.SlagCharredClaimPrefix + region;

        public static uint Hash24(int worldSeed, int region)
        {
            unchecked
            {
                uint hash = (uint)worldSeed * 0x9E3779B1u ^ (uint)region * 0x85EBCA6Bu ^ Salt;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return hash & 0xFFFFFF;
            }
        }

        public static bool Holds(int worldSeed, int region, float regionsPercent)
        {
            if (regionsPercent <= 0f) return false;
            if (regionsPercent >= 100f) return true;
            return Hash24(worldSeed, region) / (float)0x1000000 < regionsPercent / 100f;
        }

        public static bool Waiting(int region, int edgeCount, BiomeMask biome, BiomeMask biomes, bool holds, bool claimed) =>
            region >= 0 && edgeCount >= MinEdge && (biomes & biome) != 0 && holds && !claimed;
    }

    /// <summary>The server grants one claim. A second ask for the same key is refused, and so is an ask from too far away.</summary>
    public static class SlagClaim
    {
        // shortcut: 128 m, widen if a real Ashlands roll is refused because the player stood farther from the point
        public const float NearMeters = 128f;

        public static bool Near(float meters) => meters >= 0f && meters <= NearMeters;

        public static bool Take(bool eligible, bool senderNear) => eligible && senderNear;

        /// <summary>Check and record in one step. <paramref name="eligible"/> is the region rule before the key is known.</summary>
        public static bool TryTake(ISet<string> claimed, string key, bool eligible, bool senderNear)
        {
            if (!Take(eligible && !claimed.Contains(key), senderNear)) return false;
            claimed.Add(key);
            return true;
        }
    }

    /// <summary>Three plates. Fire breaks one at a time. Physical damage still gets through, so the creature dies with no fire.</summary>
    public static class SlagPlates
    {
        public static float Through(int plates, int max, float armoredPercent)
        {
            if (max <= 0 || plates <= 0) return 1f;
            if (plates > max) plates = max;
            float armored = Math.Max(0f, Math.Min(100f, armoredPercent)) / 100f;
            return 1f - (1f - armored) * plates / max;
        }

        public static int AfterFire(int plates, float stored, float fire, float perPlate, out float storedLeft)
        {
            if (plates <= 0 || fire <= 0f || perPlate <= 0f)
            {
                storedLeft = plates <= 0 ? 0f : stored;
                return Math.Max(0, plates);
            }
            stored += fire;
            while (plates > 0 && stored >= perPlate)
            {
                stored -= perPlate;
                plates--;
            }
            storedLeft = plates == 0 ? 0f : stored;
            return plates;
        }

        // Valheim HitData.HitType ordinals for the three damage-over-time ticks.
        public const int BurningTick = 5;
        public const int FreezingTick = 6;
        public const int PoisonTick = 7;

        /// <summary>A direct hit. Burn, freeze, and poison ticks are not.</summary>
        public static bool Direct(int hitType) => hitType != BurningTick && hitType != FreezingTick && hitType != PoisonTick;

        /// <summary>Fire counted toward a plate. Burn ticks are not a hit.</summary>
        public static float Counted(bool weaponHit, float fire) => weaponHit && fire > 0f ? fire : 0f;

        /// <summary>Prop <paramref name="index"/> stays on while that many plates remain. Index 0 is the first prop parented.</summary>
        public static bool Shown(int index, int plates) => index >= 0 && index < plates;
    }

    /// <summary>The same scale <c>Attack.GetLevelDamageFactor</c> uses. <c>Character.Damage</c> does not apply it.</summary>
    public static class StarHits
    {
        public static float Scale(int level) => 1f + Math.Max(0, level - 1) * 0.5f;

        public static float Damage(float baseDamage, int level) => baseDamage * Scale(level);
    }

    /// <summary>World-clock ticks. A missing value (0) is ready, so a hook that has never thrown still throws.</summary>
    public static class HookCooldown
    {
        public static bool Ready(long nowTicks, long readyTicks) => nowTicks >= readyTicks;

        public static long Arm(long nowTicks, float intervalSeconds)
        {
            double seconds = Math.Max(0d, intervalSeconds);
            return nowTicks + (long)Math.Round(seconds * TimeSpan.TicksPerSecond);
        }
    }

    /// <summary>A pull is a living hook its sender owns, close to the target, and the point it named is that hook.</summary>
    public static class HookPull
    {
        public const float Cap = 20f;

        // shortcut: 3 m, widen if a real pull is refused because the hook moved between the send and the check
        public const float PointSlack = 3f;

        public static float Clamp(float meters, float pullSetting)
        {
            float cap = Math.Min(Cap, Math.Max(0f, pullSetting));
            return Math.Min(Math.Max(0f, meters), cap);
        }

        public static bool Allow(bool ownsLivingHook, float hookToTarget, float range, float pointToHook) =>
            ownsLivingHook && hookToTarget <= range && pointToHook <= PointSlack;
    }

    /// <summary>
    /// A fraction of Deep North skeleton spawners can roll a Marrow Lantern. The same spawner still rolls its skeleton
    /// when this roll misses. ShadowPerson is never a source.
    /// </summary>
    public static class MarrowPlaces
    {
        private const uint Salt = 0x4D415252;

        public static uint HashPlace(int worldSeed, int x, int z)
        {
            unchecked
            {
                uint hash = (uint)worldSeed * 0x9E3779B1u ^ (uint)x * 0x85EBCA6Bu ^ (uint)z * 0xC2B2AE35u ^ Salt;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return hash & 0xFFFFFF;
            }
        }

        public static bool PlaceHolds(int worldSeed, int x, int z, float placesPercent)
        {
            if (placesPercent <= 0f) return false;
            if (placesPercent >= 100f) return true;
            return HashPlace(worldSeed, x, z) / (float)0x1000000 < placesPercent / 100f;
        }

        public static bool ThisSpawn(bool enabled, string prefab, bool placeHolds, float chancePercent, double roll)
        {
            if (!enabled || !placeHolds || chancePercent <= 0f || prefab == KitCreatures.ShadowPerson) return false;
            return prefab == KitCreatures.DeepNorthSkeleton && roll < chancePercent / 100.0;
        }
    }
}
