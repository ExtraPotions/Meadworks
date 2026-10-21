// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original decisions for the ECR 3.30.2 tamed-star and raid-cap behavior.
using System;

namespace ExtraPotions.Wildblood.Stars
{
    /// <summary>
    /// Tamed Stars gates a new birth. Taming does not change stars a creature already has.
    /// Raid extra stars stop at the biome cap. A remote client waits for the server's rules.
    /// </summary>
    public static class TamedStars
    {
        public const string OffspringCapability = "meadworks.wildblood.offspring_stars";
        public static readonly Version OffspringVersion = new Version(1, 0);

        /// <summary>True when a new birth may take star inheritance. Both switches have to be on.</summary>
        public static bool InheritOffspring(bool starsKept, bool creatureStars) => starsKept && creatureStars;

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
}
