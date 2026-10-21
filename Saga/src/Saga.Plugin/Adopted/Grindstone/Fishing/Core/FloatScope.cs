// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Core/FloatScope.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// One physics step of the local player's fishing float. FishingFloat.FixedUpdate runs only on the float's owner, the
    /// angler's client; it reels while the angler blocks, drains stamina while a fish is on, loses the fish at 0 stamina,
    /// lands it at 0.5 m of line and snaps the line when the float is 10 m past the line or 30 m from the rod. For a
    /// float with a <see cref="FloatFight"/> the prefix opens the scope and, before the game's step:
    /// <list type="bullet">
    /// <item>reads the water once the float lands (<see cref="Sense"/>);</item>
    /// <item>without a fish: counts towards a snag, or makes a snagged line heavy (<see cref="Snags"/>);</item>
    /// <item>with a fish: holds it through the grace (<see cref="Grace"/>) or builds line tension (<see cref="Tension"/>),
    /// either of which can take the step over (the game's step is skipped);</item>
    /// <item>speeds the reel for a spent fish, slows it for a snag, for this step only.</item>
    /// </list>
    /// The game raises Fishing inside the step (once per second of reeling, twice with a fish on): the prefix on
    /// global::Skills.RaiseSkill scales that raise by <see cref="FishXp.ReelScale"/>. Credits that arrive inside the step (a catch)
    /// go through <see cref="RaiseUnscoped"/>. The finalizer puts the reel speed back and notices a snag landed.
    /// </summary>
    public static class FloatScope
    {
        public struct State
        {
            public bool Open;
            public bool Swapped;
            public bool WasSnagged;
            public float Speed;
            public float SpeedMaxSkill;
        }

        private static bool unscoped;

        /// <summary>The float whose step is running, or null.</summary>
        public static FishingFloat Current { get; private set; }

        public static FloatFight Fight { get; private set; }

        /// <summary>Whether a fish was on the line when the running step began.</summary>
        public static bool FishOnLine { get; private set; }

        [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.FixedUpdate))]
        private static class Step
        {
            [HarmonyPrefix]
            private static bool Prefix(FishingFloat __instance, out State __state) => Begin(__instance, out __state);

            [HarmonyFinalizer]
            private static void Finalizer(FishingFloat __instance, State __state) => End(__instance, __state);
        }

        public static float ScaleFor(Player player, float factor)
        {
                if (Current == null || unscoped || player != Player.m_localPlayer)
                    return factor;
                factor *= HookGuard.Run("fishing reel experience", static onLine => FishXp.ReelScale(onLine), FishOnLine, 1f);
            
            return factor;
        }

        /// <summary>
        /// Raises the local player's Fishing by an amount that is already final, bypassing an open scope: a catch is
        /// credited inside the float's step and would otherwise be scaled as reeling. It still goes through
        /// Player.RaiseSkill (Rested) and the world's skill-gain rate.
        /// </summary>
        public static void RaiseUnscoped(Player player, float amount)
        {
            if (player == null || amount <= 0f)
                return;
            bool was = unscoped;
            unscoped = true;
            try
            {
                player.RaiseSkill(FishSkill.Skill, amount);
            }
            finally
            {
                unscoped = was;
            }
        }

        private static bool Begin(FishingFloat fishingFloat, out State state)
        {
            state = default;
            FloatFight fight = FishSkill.Active ? FloatFight.Of(fishingFloat) : null;
            Player angler = fight != null ? Angler.LocalOf(fishingFloat) : null;
            if (angler == null)
                return true;
            Fish fish = fishingFloat.GetCatch();
            Current = fishingFloat;
            Fight = fight;
            FishOnLine = fish != null;
            state.Open = true;
            state.WasSnagged = fight.Snagged;
            bool run = HookGuard.Run("fishing float step", static step => Before(step.fishingFloat, step.fight, step.angler, step.fish),
                (fishingFloat, fight, angler, fish), true);
            if (run)
                Swap(fishingFloat, ReelFactor(fight, fish != null), ref state);
            return run;
        }

        /// <summary>The features before the game's step; false when one of them took the step over.</summary>
        private static bool Before(FishingFloat fishingFloat, FloatFight fight, Player angler, Fish fish)
        {
            float dt = Time.fixedDeltaTime;
            Sense.OnStep(fishingFloat, fight, angler);
            if (fish == null)
            {
                fight.Unhooked();
                Snags.Step(fishingFloat, fight, angler, dt);
                return true;
            }
            if (fight.Fish != fish)
                fight.Hooked(fish, false);
            if (Grace.Step(fishingFloat, fight, angler, fish, dt))
                return false;
            return !Tension.Step(fishingFloat, fight, angler, fish, dt);
        }

        private static float ReelFactor(FloatFight fight, bool fishOnLine)
        {
            if (!fishOnLine)
                return Snags.ReelFactor(fight);
            return fight.Spent ? 1f + FishSkill.Percent(FishingFightSettings.SpentReelSpeed.Value) : 1f;
        }

        private static void Swap(FishingFloat fishingFloat, float factor, ref State state)
        {
            if (Mathf.Approximately(factor, 1f))
                return;
            state.Swapped = true;
            state.Speed = fishingFloat.m_pullLineSpeed;
            state.SpeedMaxSkill = fishingFloat.m_pullLineSpeedMaxSkill;
            fishingFloat.m_pullLineSpeed *= factor;
            fishingFloat.m_pullLineSpeedMaxSkill *= factor;
        }

        private static void End(FishingFloat fishingFloat, State state)
        {
            if (!state.Open)
                return;
            if (state.Swapped)
            {
                fishingFloat.m_pullLineSpeed = state.Speed;
                fishingFloat.m_pullLineSpeedMaxSkill = state.SpeedMaxSkill;
            }
            if (state.WasSnagged)
                HookGuard.Run("snag landing", static step => Snags.AfterStep(step.fishingFloat, step.fight, step.angler),
                    (fishingFloat, fight: Fight, angler: Player.m_localPlayer));
            Current = null;
            Fight = null;
            FishOnLine = false;
        }
    }
}
