// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// One hit on one character as <see cref="HitPatch"/> hands it to each feature: the parts every one of them asks for,
    /// looked up once per side of the hit instead of once per feature - the struck character's controller, the attacker
    /// the hit names and the attacker's controller. A player has no controller, so neither is looked for on one. Whether
    /// this machine owns the struck character is read live (<see cref="Owned"/>), since a death in the middle of a hit
    /// resets its view.
    /// </summary>
    internal readonly struct Struck
    {
        public readonly Character Victim;
        public readonly HitData? Hit;

        /// <summary>The struck character's controller, resolved or not; null for a player.</summary>
        public readonly EliteController? Elite;

        /// <summary>The character the hit names as its attacker, when it is loaded here; null for none.</summary>
        public readonly Character? Attacker;

        /// <summary>The attacker's controller, resolved or not; null for a player or no attacker.</summary>
        public readonly EliteController? AttackerElite;

        private Struck(Character victim, HitData? hit, EliteController? elite)
        {
            Victim = victim;
            Hit = hit;
            Elite = elite;
            Attacker = hit != null ? hit.GetAttacker() : null;
            AttackerElite = ControllerOf(Attacker);
        }

        /// <summary>True while this machine owns the struck character: the one place a hit's effects are decided.</summary>
        public bool Owned => Victim.m_nview != null && Victim.m_nview.IsValid() && Victim.m_nview.IsOwner();

        public static Struck Of(Character victim, HitData? hit) => new Struck(victim, hit, ControllerOf(victim));

        /// <summary>The same hit with its attacker looked up again: a reflect may just have killed it on this machine.</summary>
        public Struck Again() => new Struck(Victim, Hit, Elite);

        private static EliteController? ControllerOf(Character? character) =>
            character != null && !character.IsPlayer() ? character.GetComponent<EliteController>() : null;
    }
}
