// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/SplashOnce.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Splash once per swing, not once per hit: the splash amount is the whole swing's, shared by the chunks touching one
    /// hit chunk. A pickaxe swing sends one full hit per chunk collider it touched (Attack.DoMeleeAttack with
    /// m_pickaxeSpecial), and an intact deposit's owner re-sends its first hit to every chunk of the new fractured rock
    /// within 5 cm of the hit point (Destructible.Destroy calls MineRock5.Damage: one RPC_Damage per chunk). So:
    /// <list type="bullet">
    /// <item><b>Miner's client</b> (<see cref="MarkLocal"/>, called by <see cref="MineHit"/> before the hit is sent): every
    /// local pickaxe hit on a rock that can splash (a MineRock5, or an intact deposit that turns into a rock) after the
    /// first of the same swing (<see cref="MineSwing.Serial"/>) is marked: HitData.m_skillRaiseAmount =
    /// <see cref="NoSplash"/>. HitData sends that field whenever it is not 1, and nothing on a rock's path reads it:
    /// MineRock5, MineRock and Destructible never touch it, and the game raises the swing's skills on the attacker's
    /// client from Attack.m_raiseSkillAmount, not from the hit (only Projectile.Setup copies a hit's value, for a
    /// projectile's own later hits). An intact deposit's re-sent hit carries the mark along.</item>
    /// <item><b>Rock's owner</b> (<see cref="Carries"/>, checked by <see cref="OwnerHit"/>): a marked hit is applied as
    /// usual (damage, breaks, collapses credited to the miner) but does not splash. Of the unmarked hits one owner
    /// handler applies (an intact deposit's re-sends, nested in its Destructible.RPC_Damage), only the first that landed
    /// splashes (<see cref="OwnerHit"/>).</item>
    /// </list>
    /// </summary>
    public static class SplashOnce
    {
        /// <summary>HitData.m_skillRaiseAmount of a hit that must not splash; a pickaxe swing's own value is 1.</summary>
        public const float NoSplash = -1f;

        private static int markedSerial;

        /// <summary>On the miner's client, for each local pickaxe hit on <paramref name="rock"/> inside the swing, before it is sent.</summary>
        public static void MarkLocal(Rock rock, HitData hit)
        {
            if (rock.Chunks5 == null && !rock.Info.Fractures)
                return;
            if (markedSerial == MineSwing.Serial)
                hit.m_skillRaiseAmount = NoSplash;
            else
                markedSerial = MineSwing.Serial;
        }

        /// <summary>On the rock's owner: the hit may splash (it is not a later hit of its swing).</summary>
        public static bool Carries(HitData hit) => hit != null && hit.m_skillRaiseAmount != NoSplash;
    }
}
