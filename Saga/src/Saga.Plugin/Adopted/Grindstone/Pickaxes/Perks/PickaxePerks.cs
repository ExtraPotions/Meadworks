// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Perks/PickaxePerks.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The perks of mining (section 17, <see cref="PickaxePerkSettings"/>), each growing linearly from nothing at Pickaxes
    /// level 0 to its setting at level 100:
    /// <list type="bullet">
    /// <item>Extra ore, on the rock's owner at the miner's level (carried by the hit, <see cref="Miner"/>): every broken
    /// chunk of an ore deposit, or single-piece deposit, has the level's share of Extra Ore Chance At 100 to roll its own
    /// drop table once more. Every break counts: a hit, a splash or a collapse (<see cref="BreakCause"/>).</item>
    /// <item>The clean strike's roll, on the rock's owner: a chunk of an ore deposit whose break finds the breaking miner's
    /// clean strike mark on it (<see cref="CleanStrikeMarks"/>) rolls its table once more. A fixed rule, no setting.</item>
    /// <item>Pickaxe wear, on the miner's own client at their own level: a swing that hit rock gets the level's share of
    /// Pickaxe Wear Reduction At 100 of its durability loss back (<see cref="ToolWear"/>).</item>
    /// </list>
    /// Plain stone rocks get no extra rolls from either, so stone doesn't pile up; swings on them still wear the pickaxe
    /// less. The extra rolls are spawned by <see cref="ExtraDrops"/> once every break feature ran, at the world's resource
    /// rate. Everything stands aside while <see cref="PickSkill.Active"/> is off.
    /// </summary>
    public static class PickaxePerks
    {
        /// <summary>
        /// Called by <see cref="MineBreak"/> on the rock's owner, after <see cref="Veins.OnBreak"/>, for every chunk (or
        /// single piece) a miner broke. Takes the breaking miner's clean strike mark on the chunk first, on every break, so
        /// marks never linger; then adds an ore deposit's extra rolls with <see cref="RockBreak.AddRolls(int)"/>.
        /// </summary>
        public static void OnBreak(RockBreak broken)
        {
            if (broken == null)
                return;
            bool cleanStrike = CleanStrikeMarks.Take(broken);
            if (!PickSkill.Active || broken.Miner == null || broken.Rock == null || !broken.IsOre)
                return;
            if (cleanStrike)
                broken.AddRolls(1);
            if (Roll(ExtraOreChance(broken.Miner)))
                broken.AddRolls(1);
        }

        /// <summary>
        /// Called by <see cref="MineSwing"/> on the miner's own client, once per swing that hit rock (<paramref name="rocks"/>
        /// is never empty), when the game raises Pickaxes for it: Attack.DoMeleeAttack has already drained the pickaxe's
        /// durability for the swing, so part of that drain goes back, at the local player's own level.
        /// </summary>
        public static void OnSwingHitRock(Attack attack, IReadOnlyList<Rock> rocks)
        {
            Humanoid character = attack != null ? attack.m_character : null;
            if (character == null || character != Player.m_localPlayer || !PickSkill.Active || rocks == null || rocks.Count == 0)
                return;
            float share = PickSkill.Share(PickaxePerkSettings.WearReduction.Value, PickSkill.Local());
            ToolWear.GiveBack(attack.m_weapon, share);
        }

        /// <summary>The chance, 0..1, that a chunk this miner broke rolls its drop table once more.</summary>
        private static float ExtraOreChance(Miner miner) =>
            Mathf.Clamp01(PickSkill.Share(PickaxePerkSettings.ExtraOreChance.Value, miner.Level));

        /// <summary>True with the given chance: never at 0, always at 1.</summary>
        private static bool Roll(float chance) => chance >= 1f || (chance > 0f && Random.value < chance);
    }
}
