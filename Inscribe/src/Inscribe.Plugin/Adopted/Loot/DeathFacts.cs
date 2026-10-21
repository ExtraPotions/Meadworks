// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Everything the drop roll reads from a dying creature, taken once at the death hook while its ZDO is still valid
    /// (the game destroys the object at the end of its death handling). Readable on any peer for <c>LootPreview</c>;
    /// the roll itself only ever uses it on the ZDO owner.
    /// </summary>
    public readonly struct DeathFacts
    {
        private DeathFacts(Character character, ZDO zdo)
        {
            PrefabHash = zdo.GetPrefab();
            var wildblood = Runtime.InscribeLootInterop.DeathFacts(character);
            Wildblood = wildblood;
            Stars = Runtime.InscribeLootInterop.StarSynergy && wildblood != null ? Math.Max(0, wildblood.Item1) : Math.Max(0, character.GetLevel() - 1);
            Ally = LootKeys.IsPlayerAlly(character);
            PlayerHit = LootKeys.PlayerHit(zdo);
            AllyHit = LootKeys.AllyHitSet(zdo);
            Cheated = zdo.GetBool(ZDOVars.s_cheated);
            FlaggedBoss = (character.IsBoss() || wildblood?.Item3 == true)
                && !global::ExtraPotions.Inscribe.Runes.BossPhases.IsEarlierPhase(Utils.GetPrefabName(character.gameObject));
            Position = character.transform.position;
            Center = character.GetCenterPoint();
            Ecr = EcrFacts.Read(zdo, ModSettings.EcrSynergy.Value);
        }

        internal Tuple<int, bool, bool, bool, int>? Wildblood { get; }
        public int PrefabHash { get; }

        /// <summary>Vanilla level minus one (0 = unstarred).</summary>
        public int Stars { get; }

        /// <summary>
        /// Elite Creatures Reborn's keys on the creature (ecr-integration.md): default when ECR is absent; the star and
        /// tier keys only when the synced synergy switch is on.
        /// </summary>
        public EcrFacts Ecr { get; }

        /// <summary>The star count the roll uses: ECR's for an ECR-resolved creature with the synergy on, else <see cref="Stars"/>.</summary>
        public int RollStars => Ecr.UsesStars ? Ecr.Stars : Stars;

        /// <summary>Tamed, or summoned by a player (treated like tamed: a summon is a player's own creature).</summary>
        public bool Ally { get; }

        public bool PlayerHit { get; }
        public bool AllyHit { get; }
        public bool Cheated { get; }

        /// <summary>The game marks it as a boss (a boss not in the boss map still uses the boss rarity rows).</summary>
        public bool FlaggedBoss { get; }

        public Vector3 Position { get; }
        public Vector3 Center { get; }

        /// <summary>A living non-player creature with a valid ZDO; false for players and despawned objects.</summary>
        public static bool TryRead(Character character, out DeathFacts facts)
        {
            facts = default;
            if (character == null || character.IsPlayer() || character.m_nview == null || !character.m_nview.IsValid())
            {
                return false;
            }
            facts = new DeathFacts(character, character.m_nview.GetZDO());
            return true;
        }

        /// <summary>Whether this death may drop our loot (drops.md section 2); <paramref name="reason"/> says why not.</summary>
        public bool Qualifies(DropRules drops, out string reason)
        {
            if (Wildblood?.Item4 == true) { reason = "Wildblood suppresses loot for this creature"; return false; }
            if (Ally && !drops.Tamed)
            {
                reason = "tamed or summoned";
                return false;
            }
            if (drops.RequirePlayer && !PlayerHit && !AllyHit)
            {
                reason = "no player or pet hit it";
                return false;
            }
            if (Ecr.Worthless && drops.Ecr.SkipWorthless)
            {
                reason = "Elite Creatures Reborn pays nothing for it (Phantom copy, Cloning decoy, or first of a Tethered pair)";
                return false;
            }
            reason = "";
            return true;
        }
    }
}
