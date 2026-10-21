// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Bites/Tackle.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Casting and the line, on the angler's client.
    /// <list type="bullet">
    /// <item><b>Cast distance:</b> a fishing rod's cast throws a projectile (Attack.FireProjectileBurst, with the rod's
    /// launch speed, drawn like a bow) that becomes the float where it lands. For the local player's rod the prefix raises
    /// the attack's launch speeds by the square root of 1 + Cast Distance At 100's share, since a throw's range grows with
    /// the square of its speed, and the finalizer puts them back.</item>
    /// <item><b>Line length:</b> the game snaps the line when the float is more than m_maxDistance (30 m) from the rod.
    /// When the float lands (<see cref="FloatSetup"/>) that grows by Line Length At 100's share; only the float's owner, the
    /// angler, reads it.</item>
    /// </list>
    /// </summary>
    public static class Tackle
    {
        public struct Launch
        {
            public bool Raised;
            public float Speed;
            public float SpeedMin;
        }

        [HarmonyPatch(typeof(Attack), nameof(Attack.FireProjectileBurst))]
        private static class Cast
        {
            [HarmonyPrefix]
            private static void Prefix(Attack __instance, out Launch __state)
            {
                Launch launch = default;
                if (FishSkill.Active)
                    HookGuard.Run("cast distance", () => launch = Raise(__instance));
                __state = launch;
            }

            [HarmonyFinalizer]
            private static void Finalizer(Attack __instance, Launch __state)
            {
                if (!__state.Raised)
                    return;
                __instance.m_projectileVel = __state.Speed;
                __instance.m_projectileVelMin = __state.SpeedMin;
            }
        }

        public static void OnLanded(FishingFloat fishingFloat, float level) =>
            fishingFloat.m_maxDistance *= 1f + FishSkill.Share(FishingCatchSettings.LineLengthAt100.Value, level);

        public static bool IsRod(ItemDrop.ItemData item) =>
            item?.m_shared != null && item.m_shared.m_animationState == ItemDrop.ItemData.AnimationState.FishingRod;

        private static Launch Raise(Attack attack)
        {
            if (attack.m_character == null || attack.m_character != Player.m_localPlayer || !IsRod(attack.m_weapon))
                return default;
            float farther = FishSkill.Share(FishingCatchSettings.CastDistanceAt100.Value, FishSkill.Local());
            if (farther <= 0f)
                return default;
            Launch launch = new Launch { Raised = true, Speed = attack.m_projectileVel, SpeedMin = attack.m_projectileVelMin };
            float speed = Mathf.Sqrt(1f + farther);
            attack.m_projectileVel *= speed;
            attack.m_projectileVelMin *= speed;
            return launch;
        }
    }
}
