// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Foraging/Core/ForagePick.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A local player's pick of forage, on the picker's client where Pickable.Interact runs. The game already raises a
    /// skill on a pick and rolls one extra item from it (m_pickRaiseSkill, m_maxLevelBonusChance: Farming and 25% on
    /// wild plants). For the one call the plant's fields are turned to Foraging and the Extra Yield Chance, so the
    /// game's own code raises Foraging (scaled by <see cref="ForageXp"/>), rolls the extra item from the Foraging level
    /// and shows its "+1"; afterwards the fields are put back. A top-level pick then sweeps the plants of the same kind
    /// around it (<see cref="ForageSweep"/>).
    /// </summary>
    [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
    public static class ForagePick
    {
        public sealed class State
        {
            public Pickable Pickable;
            public global::Skills.SkillType Skill;
            public float Chance;
            public EffectList Effect;
            public bool ScopeOpened;
            public bool Finished;
        }

        [HarmonyPrefix]
        private static void Prefix(Pickable __instance, Humanoid character, out State __state)
        {
            __state = null;
            Player player = character as Player;
            if (player == null || player != Player.m_localPlayer || !Forage.CanPick(__instance))
                return;
            ForageEntry entry = HookGuard.Run("Foraging pick", () => Forage.Of(__instance), null);
            if (entry == null)
                return;
            __state = Swap(__instance);
            __state.ScopeOpened = ForageXp.Begin(__instance, entry);
        }

        [HarmonyPostfix]
        private static void Postfix(Pickable __instance, Humanoid character, bool repeat, State __state)
        {
            if (__state == null)
                return;
            Finish(__state);
            if (!repeat && !ForageSweep.Running)
                HookGuard.Run("Foraging sweep", () => ForageSweep.Around(__instance, character as Player));
        }

        [HarmonyFinalizer]
        private static void Finalizer(State __state)
        {
            if (__state != null)
                Finish(__state);
        }

        private static State Swap(Pickable pickable)
        {
            State state = new State
            {
                Pickable = pickable,
                Skill = pickable.m_pickRaiseSkill,
                Chance = pickable.m_maxLevelBonusChance,
                Effect = pickable.m_bonusEffect,
            };
            pickable.m_pickRaiseSkill = ForagingSkill.Type;
            pickable.m_maxLevelBonusChance = Mathf.Clamp01(ForagePerkSettings.ExtraYieldAt100.Value / 100f);
            if (pickable.m_bonusEffect == null)
                pickable.m_bonusEffect = new EffectList();
            return state;
        }

        /// <summary>Puts the plant's fields back and closes the experience scope, once.</summary>
        private static void Finish(State state)
        {
            if (state.Finished)
                return;
            state.Finished = true;
            if (state.Pickable != null)
            {
                state.Pickable.m_pickRaiseSkill = state.Skill;
                state.Pickable.m_maxLevelBonusChance = state.Chance;
                state.Pickable.m_bonusEffect = state.Effect;
            }
            ForageXp.End(state.ScopeOpened);
        }
    }
}
