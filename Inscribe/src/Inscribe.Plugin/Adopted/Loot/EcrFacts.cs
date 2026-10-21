// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Elite Creatures Reborn's facts about one creature (ecr-integration.md section 3), read from its own ZDO. At a death
    /// this runs on the creature's ZDO owner in the <c>Character.OnDeath</c> prefix, before the game destroys the ZDO.
    /// Multiplayer: ECR's owner wrote the keys when it rolled the creature and the game replicates them, so whichever peer
    /// owns the creature when it dies reads the same stars; no RPC. With ECR absent nothing is read (a stale key from an
    /// old ECR install is ignored, section 8). The default value is "ECR absent".
    /// </summary>
    public readonly struct EcrFacts
    {
        private EcrFacts(bool full, bool resolved, int stars, bool worthless, int tier)
        {
            Present = true;
            ReadFull = full;
            Resolved = resolved;
            Stars = stars;
            Worthless = worthless;
            HasTier = tier >= 0;
            Tier = Math.Max(0, tier);
        }

        /// <summary>ECR is installed on this peer, so the keys were read.</summary>
        public bool Present { get; }

        /// <summary>The star and tier keys were read too (the synergy was on); otherwise only the copy marks.</summary>
        public bool ReadFull { get; }

        /// <summary><c>ecr_resolved</c>: ECR has rolled this creature.</summary>
        public bool Resolved { get; }

        /// <summary><c>ecr_stars</c>, never below 0 (a negative or unreadable value is 0).</summary>
        public int Stars { get; }

        /// <summary>
        /// Pays nothing, as ECR's own loot rules say: a Phantom copy (<c>ecr_phantom_of</c>), a Cloning decoy
        /// (<c>ecr_clone_of</c>), or the first of a Tethered pair to fall while its partner still stands (<c>ecr_tether</c>).
        /// </summary>
        public bool Worthless { get; }

        /// <summary><c>ecr_tier</c> is on the creature (not yet written by any ECR release, DECISIONS ECR-6).</summary>
        public bool HasTier { get; }

        public int Tier { get; }

        /// <summary>ECR's stars replace the game level: synergy on and the creature resolved (section 4).</summary>
        public bool UsesStars => ReadFull && Resolved;

        /// <summary>The ECR part of a roll input: the star table switch and the world tier terms.</summary>
        public EcrRoll Roll => new EcrRoll(UsesStars, ReadFull && HasTier, Tier);

        /// <summary>
        /// Reads the creature's ECR keys. <paramref name="full"/> (the synergy is on) adds the star and tier reads; the
        /// copy marks are read whenever ECR is present, synergy or not (ECR-7). ECR absent: nothing is read.
        /// </summary>
        public static EcrFacts Read(ZDO zdo, bool full)
        {
            if (!EcrPresence.Present || Runtime.EliteCreaturesOwner.Present)
            {
                return default;
            }
            bool worthless = zdo.GetZDOID(EcrKeys.PhantomOfHash) != ZDOID.None
                || zdo.GetZDOID(EcrKeys.CloneOfHash) != ZDOID.None || TetherStanding(zdo);
            if (!full)
            {
                return new EcrFacts(false, false, 0, worthless, -1);
            }
            bool resolved = zdo.GetBool(EcrKeys.ResolvedHash);
            int stars = Math.Max(0, zdo.GetInt(EcrKeys.StarsHash));
            return new EcrFacts(true, resolved, stars, worthless, zdo.GetInt(EcrKeys.TierHash, -1));
        }

        /// <summary>
        /// A Tethered boss whose partner is still in the fight: its ZDO is here with health left (no health key is full
        /// health). ECR's own test (<c>TetherPair.Standing</c>), made on the same owner at the same death, so both mods
        /// agree on which of the pair pays. A partner whose ZDO is gone has fallen.
        /// </summary>
        private static bool TetherStanding(ZDO zdo)
        {
            ZDOID partner = zdo.GetZDOID(EcrKeys.TetherHash);
            ZDO? other = partner != ZDOID.None && ZDOMan.instance != null ? ZDOMan.instance.GetZDO(partner) : null;
            return other != null && other.IsValid() && other.GetFloat(ZDOVars.s_health, 1f) > 0f;
        }
    }

    /// <summary>
    /// The Elite Creatures Reborn terms of one roll (<see cref="LootInput.Ecr"/>). The default - chests, <c>ecraft</c>
    /// test rolls, ECR absent or the synergy off - changes nothing.
    /// </summary>
    public readonly struct EcrRoll
    {
        public EcrRoll(bool starsFromEcr, bool hasTier, int tier)
        {
            StarsFromEcr = starsFromEcr;
            HasTier = hasTier;
            Tier = tier;
        }

        /// <summary><see cref="LootInput.Stars"/> are ECR's stars: use <c>drops.ecr.star_multipliers</c>.</summary>
        public bool StarsFromEcr { get; }

        /// <summary>An ECR world tier applies (<c>drops.ecr.tier_*</c>).</summary>
        public bool HasTier { get; }

        public int Tier { get; }
    }
}
