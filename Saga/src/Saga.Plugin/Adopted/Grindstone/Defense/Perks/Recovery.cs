// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Perks/Recovery.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Out-of-combat regeneration, on the local player's own client: every Regeneration Interval, a player who has not
    /// attacked, blocked or taken damage for Out Of Combat Delay seconds heals Regeneration percent of their max health
    /// at level 100. The game's own health regeneration modifiers (SEMan.ModifyHealthRegen: rested, meads, anything
    /// that stops regeneration) scale it as they scale food regeneration.
    /// <para>Combat is the player's own attacks (Humanoid.m_lastCombatTimer, reset when an attack starts), plus every
    /// hit with an attacker and all damage taken, which <see cref="IncomingHit"/> and <see cref="DamageIntake"/> mark
    /// here. Blocks are hits with an attacker, so they count.</para>
    /// </summary>
    public static class Recovery
    {
        private static float lastCombat = float.NegativeInfinity;
        private static float timer;

        public static void Reset() { lastCombat = float.NegativeInfinity; timer = 0; }

        public static void MarkCombat() => lastCombat = Time.time;

        /// <summary>The local player has been out of combat for the delay.</summary>
        public static bool OutOfCombat(Player player)
        {
            float delay = Mathf.Max(0f, DefenseSettings.OutOfCombatDelay.Value);
            return Time.time - lastCombat >= delay && player.m_lastCombatTimer >= delay;
        }

        /// <summary>Every frame for the local player (<see cref="LocalPlayerTick"/>).</summary>
        public static void Tick(Player player, float dt)
        {
            timer += dt;
            if (timer < DefenseSettings.RegenerationInterval.Value)
                return;
            timer = 0f;
            HookGuard.Run("defense regeneration", static p => Heal(p), player);
        }

        private static void Heal(Player player)
        {
            float share = DefenseSkill.LocalShare(DefenseSettings.Regeneration.Value);
            if (share <= 0f || player.IsDead() || player.GetHealth() >= player.GetMaxHealth() || !OutOfCombat(player))
                return;
            float multiplier = 1f;
            player.GetSEMan().ModifyHealthRegen(ref multiplier);
            player.Heal(player.GetMaxHealth() * share * multiplier);
        }
    }
}
