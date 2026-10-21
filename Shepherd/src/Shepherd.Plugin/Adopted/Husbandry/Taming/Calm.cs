// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Taming/Calm.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; Crucible boundaries; owned native lifecycle.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using HarmonyLib;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Calm, the Husbandry milestone at Calm Level: a creature you have started taming (tameness above 0%) no longer
    /// counts you as an enemy. Every creature picks its target through the static <c>BaseAI.IsEnemy(a, b)</c> with
    /// itself as <c>a</c>: <c>FindEnemy</c> (so it never targets you, and boars and hens, which flee from their target,
    /// never flee from you), <c>UpdateTarget</c> (a target that stops being an enemy is dropped) and its own swing's hit
    /// filter (a swing already under way does not hurt you). Without a target it is not alerted by you, and taming only
    /// pauses while a creature is alerted, so taming goes on while you stand beside it.
    /// <list type="bullet">
    /// <item>Only that direction changes: with you as <c>a</c> the answer stays the game's, so you can still hit it.
    /// Hurting it breaks the calm for you for Calm Break Time (<see cref="CalmProvocation"/>).</item>
    /// <item>Only you: other players below the level still scare it.</item>
    /// <item>Runs wherever the creature's AI runs, its owner, which reads your published level. This is a hot path
    /// (every creature, every target check), so the cheap tests come first and nothing allocates.</item>
    /// </list>
    /// </summary>
    public static class Calm
    {
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IsEnemy), typeof(Character), typeof(Character))]
        private static class Enemy
        {
            [HarmonyPostfix]
            private static void Postfix(Character a, Character b, ref bool __result)
            {
                if (!__result || !(b is Player player) || a == null || a.IsPlayer())
                    return;
                try
                {
                    if (IsCalm(a, player))
                        __result = false;
                }
                catch (Exception exception)
                {
                    NativeHusbandry.Report(exception, "calm");
                }
            }
        }

        /// <summary>True when <paramref name="creature"/> is being tamed and calm towards <paramref name="player"/>.</summary>
        public static bool IsCalm(Character creature, Player player)
        {
            Tameable tameable = Herd.TameableOf(creature);
            if (tameable == null || !HusbandrySkill.Active || !Herd.IsBeingTamed(tameable))
                return false;
            return HusbandrySkill.Reaches(HusbandrySkill.Of(player), HusbandryTamingSettings.CalmLevel.Value)
                && !CalmProvocation.IsWary(tameable, player);
        }
    }
}
