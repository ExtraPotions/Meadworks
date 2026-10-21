// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/MineSwing.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using HarmonyLib;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The local player's melee swing that is running right now, on the miner's own client. Attack.DoMeleeAttack (every
    /// pickaxe attack is melee) sends each hit, drains the weapon's durability once, then raises each skill it used once
    /// (Pickaxes when it hit rock). A prefix opens the scope for the local player, <see cref="MineHit"/> records every
    /// rock a pickaxe hit of the swing landed on (<see cref="Record"/>), and a finalizer closes it. Woodcutting's
    /// <see cref="SwingScope"/> patches the same two methods for its own skill; both scopes open side by side.
    /// <para>The Pickaxes raise inside the scope is the "this swing hit rock" moment, once per swing: its factor is
    /// scaled by <see cref="MineXp.SwingScale"/>, then, when the swing hit rock, the wear perk
    /// (<see cref="PickaxePerks.OnSwingHitRock"/>) and <see cref="Echo.OnSwingHitRock"/> run, each guarded.</para>
    /// </summary>
    public static class MineSwing
    {
        private static readonly List<Rock> NoRocks = new List<Rock>();
        private static List<Rock> rocks;

        public static bool Active { get; private set; }

        /// <summary>The running attack; null outside a scope.</summary>
        public static Attack Attack { get; private set; }

        /// <summary>Counts swings: a new number for every scope, so features can tell hits of one swing from the next.</summary>
        public static int Serial { get; private set; }

        /// <summary>Every rock a pickaxe hit of this swing landed on, each once, in hit order; empty outside a scope.</summary>
        public static IReadOnlyList<Rock> Rocks => rocks ?? NoRocks;

        [HarmonyPatch(typeof(Attack), nameof(Attack.DoMeleeAttack))]
        private static class Melee
        {
            [HarmonyPrefix]
            private static void Prefix(Attack __instance, out bool __state) => __state = Begin(__instance);

            [HarmonyFinalizer]
            private static void Finalizer(bool __state) => End(__state);
        }

        public static float ScaleFor(Player player, float factor)
        {
                if (!Active || !PickSkill.Active || player == null || player != Player.m_localPlayer)
                    return factor;
                IReadOnlyList<Rock> hit = Rocks;
                Attack attack = Attack;
                factor *= HookGuard.Run("mine swing experience", static swing => MineXp.SwingScale(swing.hit, swing.attack), (hit, attack), 1f);
                if (hit.Count == 0)
                    return factor;
                HookGuard.Run("pickaxe wear", static swing => PickaxePerks.OnSwingHitRock(swing.attack, swing.hit), (hit, attack));
                HookGuard.Run("echo", static swing => Echo.OnSwingHitRock(swing.attack, swing.hit), (hit, attack));
            
            return factor;
        }

        /// <summary>
        /// Raises the local player's Pickaxes by an amount that is already final, bypassing an open scope: clean strike
        /// and discovery credits arrive inside a swing (from the local hit), and would otherwise be scaled again and run
        /// the swing hooks. It still goes through Player.RaiseSkill (Rested) and the world's skill-gain rate.
        /// </summary>
        public static void RaiseUnscoped(Player player, float amount)
        {
            if (player == null || amount <= 0f)
                return;
            bool wasActive = Active;
            Active = false;
            try
            {
                player.RaiseSkill(PickSkill.Skill, amount);
            }
            finally
            {
                Active = wasActive;
            }
        }

        /// <summary>Notes a rock a pickaxe hit of this swing landed on; ignored outside a scope.</summary>
        public static void Record(Rock rock)
        {
            if (!Active || rock == null)
                return;
            if (rocks == null)
                rocks = new List<Rock>(2);
            if (!rocks.Exists(known => known.Target == rock.Target))
                rocks.Add(rock);
        }

        private static bool Begin(Attack attack)
        {
            if (Active || attack == null || attack.m_character == null || attack.m_character != Player.m_localPlayer)
                return false;
            Active = true;
            Attack = attack;
            Serial++;
            rocks = null;
            return true;
        }

        private static void End(bool opened)
        {
            if (!opened)
                return;
            Active = false;
            Attack = null;
            rocks = null;
        }
    }
}
