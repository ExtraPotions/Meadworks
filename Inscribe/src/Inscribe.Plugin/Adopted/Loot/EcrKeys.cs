// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Elite Creatures Reborn's creature ZDO keys that ExtraPotions.Inscribe.Adopted reads (ecr-integration.md section 3). ECR is our own
    /// mod in this repository; the names were read from its source (<c>Traits/TraitKeys.cs</c>, <c>Traits/AspectStore.cs</c>)
    /// and are the whole contract between the two mods: no ECR type or assembly is referenced, and a renamed key only
    /// degrades to "no ECR data" (section 8). ECR writes them with the string overloads of <c>ZDO.Set</c>, which hash the
    /// name with <c>GetStableHashCode</c> (a ZDOID under the pair <c>ZDO.GetHashZDOID</c> makes), so the hashes below
    /// address the same values. Read only, never written.
    /// </summary>
    internal static class EcrKeys
    {
        /// <summary>Bool: ECR has rolled this creature's traits (<c>TraitStore.Save</c>).</summary>
        public const string Resolved = "ecr_resolved";

        /// <summary>Int: ECR's star count, 0 to its ceiling (<c>TraitStore.Save</c>).</summary>
        public const string Stars = "ecr_stars";

        /// <summary>
        /// ZDOID: on a Phantom copy, the boss it belongs to (<c>AspectStore.SetPhantomOf</c>). Its presence is what makes a
        /// copy a copy; ECR turns the copy's own drops off.
        /// </summary>
        public const string PhantomOf = "ecr_phantom_of";

        /// <summary>ZDOID: on a Cloning decoy, the creature it stands in for (<c>CloneStore</c>); hollow like a Phantom copy.</summary>
        public const string CloneOf = "ecr_clone_of";

        /// <summary>
        /// ZDOID: on each of a Tethered pair, the other boss (<c>AspectStore.SetTether</c>). ECR pays only the last of the
        /// pair to fall (<c>Loot/TetherLoot</c>); a Twin pair has no such rule, each Twin pays its own.
        /// </summary>
        public const string Tether = "ecr_tether";

        /// <summary>
        /// Int 0-7: the world tier the creature was rolled at. <b>Not written by ECR yet</b> (DECISIONS ECR-6, decided by
        /// the user 2026-09-24: ECR writes it when it builds world tiers): until an ECR release writes it, it reads as
        /// absent and the tier terms stay inert. If ECR picks another name, this constant is the only thing that changes.
        /// </summary>
        public const string Tier = "ecr_tier";

        public static readonly int ResolvedHash = Resolved.GetStableHashCode();
        public static readonly int StarsHash = Stars.GetStableHashCode();
        public static readonly KeyValuePair<int, int> PhantomOfHash = ZDO.GetHashZDOID(PhantomOf);
        public static readonly KeyValuePair<int, int> CloneOfHash = ZDO.GetHashZDOID(CloneOf);
        public static readonly KeyValuePair<int, int> TetherHash = ZDO.GetHashZDOID(Tether);
        public static readonly int TierHash = Tier.GetStableHashCode();
    }
}
