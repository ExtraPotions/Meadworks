// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// The ZDO facts the drop rules read (drops.md sections 2 and 11): the game's own attacker record for players, our
    /// <c>ecf_ally_hit</c> flag for tamed allies, which the game's record ignores, and our <c>ecf_filled</c> flag on world
    /// containers. Hashes are computed once. (The player-side loot-find keys are <see cref="FindKeys"/>.)
    /// </summary>
    public static class LootKeys
    {
        /// <summary>Bool on a creature's ZDO: a tamed or summoned ally landed a hit on it. Written by the creature's owner.</summary>
        public const string AllyHit = "ecf_ally_hit";

        public static readonly int AllyHitHash = AllyHit.GetStableHashCode();

        /// <summary>
        /// Bool on a world container's ZDO: this mod has rolled its loot into it (drops.md section 11). Written by the
        /// container's owner at the moment the game fills the container with its default items, before our items go in,
        /// so a container never rolls twice.
        /// </summary>
        public const string Filled = "ecf_filled";

        public static readonly int FilledHash = Filled.GetStableHashCode();

        /// <summary>A player's side: tamed (a wolf pack, a lox) or summoned by a player (skeletons from a staff).</summary>
        public static bool IsPlayerAlly(Character character) =>
            character.IsTamed() || character.GetFaction() == Character.Faction.PlayerSpawned;

        /// <summary>At least one player hit it (the game's own record, the one that credits kills).</summary>
        public static bool PlayerHit(ZDO zdo) => zdo.GetInt(ZDOVars.s_attackers) > 0;

        public static bool AllyHitSet(ZDO zdo) => zdo.GetBool(AllyHitHash);
    }
}
