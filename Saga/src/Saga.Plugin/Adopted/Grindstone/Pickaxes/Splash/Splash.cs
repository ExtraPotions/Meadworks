// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Splash/Splash.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Splash: a miner's pickaxe swing on a chunk of a MineRock5 also damages the intact chunks touching it, on the rock's
    /// ZDO owner (the machine that applies every hit and spawns the drops). Once per swing: a swing sends one hit per
    /// chunk it touches, and only its first on a rock splashes (<see cref="SplashOnce"/>).
    /// <list type="bullet">
    /// <item><b>Amount.</b> Splash Damage At 100 times the miner's level share of 100, in steps of 10 levels from nothing
    /// below level 10 (<see cref="Amount"/>), read from the hit's own level: a level 100 miner splashes 15 by default. It
    /// comes from the level alone, so seams and clean strikes never multiply it.</item>
    /// <item><b>Touching chunks</b> (<see cref="TouchingChunks"/>): the rock's own intact chunks inside the hit chunk's
    /// support box, the box the game's support test uses. They share the amount equally; none, nothing happens.</item>
    /// <item><b>Damage</b> (<see cref="SplashHits"/>): each gets a pickaxe hit of its share through the game's own chunk
    /// damage (MineRock5.DamageArea), which applies the rock's resistances, saves the health and, on a break, plays the
    /// destroyed effect, spawns the chunk's drops and destroys the rock after its last chunk. <see cref="SplashMute"/>
    /// holds back the hit effect, the damage numbers and the noise; the breaks reach <see cref="MineBreak"/> with
    /// <see cref="BreakCause.Splash"/> through <see cref="ChunkBreaks"/>, so every break feature counts them.</item>
    /// <item><b>Support.</b> When splash broke anything, the game's support check runs once afterwards, as it does after
    /// a hit breaks a chunk (MineRock5.RPC_Damage), for rocks that use it (m_supportCheck); the chunks it drops are the
    /// miner's collapses.</item>
    /// </list>
    /// Splash never splashes again (<see cref="OwnerHit"/> hands only hits applied by RPC_Damage here) and gives no
    /// experience (skills are raised on the miner's client, by the swing, never by chunk damage).
    /// </summary>
    public static class Splash
    {
        /// <summary>
        /// Called by <see cref="OwnerHit"/> on the rock's owner after the game applied the first Pickaxes hit of a swing
        /// to chunk <paramref name="area"/> of a MineRock5 (MineRock5.RPC_Damage: DamageArea, then its support check),
        /// unless the hit destroyed the whole rock. <paramref name="rock"/>.Chunks5 is the MineRock5; <paramref name="hit"/> is the hit
        /// as the owner handled it (the rock's resistances already applied); <paramref name="miner"/> whose level splash
        /// follows; <paramref name="landed"/> the hit passed the tool tier check and took health off its chunk. Only a
        /// landed hit splashes.
        /// </summary>
        public static void OnOwnerHit(Rock rock, HitData hit, int area, Miner miner, bool landed)
        {
            MineRock5 chunks = rock?.Chunks5;
            float amount = Amount(miner);
            if (!landed || chunks == null || hit == null || amount <= 0f || !rock.IsOwner)
                return;
            List<int> touching = TouchingChunks.Of(chunks, area);
            if (touching.Count == 0)
                return;
            bool broke = SplashHits.Apply(rock, touching, hit, amount / touching.Count);
            if (broke && chunks.m_supportCheck && !SplashHits.Gone(rock))
                chunks.CheckSupport();
        }

        /// <summary>Splash grows in whole steps of this many levels, a tenth of Splash Damage At 100 per step.</summary>
        private const float LevelStep = 10f;

        /// <summary>
        /// The damage a miner's hit splashes in total, before it is shared: Splash Damage At 100 times their level as a
        /// share of 100, rounded down to a whole step of <see cref="LevelStep"/> levels (by default 0 below level 10,
        /// 1.5 from 10, 7.5 from 50, 15 at 100). 0 while Pickaxes is off or for no miner.
        /// </summary>
        public static float Amount(Miner miner)
        {
            if (miner == null || !PickSkill.Active)
                return 0f;
            // The tiny margin keeps a level that arrives as 29.99998 (float noise from the hit) on its step.
            float steps = Mathf.Floor(miner.Level / LevelStep + 0.0001f);
            return Mathf.Max(0f, PickaxePerkSettings.SplashDamage.Value) * Mathf.Clamp01(steps * LevelStep / 100f);
        }
    }
}
