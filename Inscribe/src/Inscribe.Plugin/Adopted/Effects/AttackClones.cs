// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Per-swing weapon affixes. Humanoid.StartAttack clones the weapon's shared Attack for every swing, so a prefix on
    /// Attack.Start may change that clone's fields for this swing only (never the shared template, pitfall 4): Long
    /// Reach, Sweeping Arc, Volley and Twincast (projectile count), Rune-Edged (damage multiplier) - and decide the
    /// cost rewrites Rune-Edged and Blood Price make, which the cost getters below apply to that clone only. Attacks
    /// start on the attacker's own client; everything here filters to the local player's swings.
    /// </summary>
    internal static class AttackClones
    {
        /// <summary>The local player's current swing clone, and what it was given.</summary>
        public static Attack? Current;
        public static float RuneEitr, BloodHealth;
        public static bool Twin, Multi;
        public static int BaseProjectiles;

        /// <summary>False = refuse the swing (Blood Price when the cost would kill).</summary>
        public static bool OnStart(Attack clone, Humanoid character, ItemDrop.ItemData weapon)
        {
            Current = null;
            ItemLocalSums? sums = ItemLocalCache.Get(weapon);
            if (sums == null)
            {
                return true;
            }
            Current = clone;
            RuneEitr = BloodHealth = 0f;
            clone.m_attackRange *= 1f + sums.Get(EffectKind.AttackReach);
            clone.m_attackAngle += sums.Get(EffectKind.AttackArc);
            Projectiles(clone, sums);
            return Costs(clone, character, weapon, sums);
        }

        private static void Projectiles(Attack clone, ItemLocalSums sums)
        {
            Twin = sums.Get(EffectKind.Twincast) > 0f;
            Multi = sums.Get(EffectKind.Multishot) > 0f && clone.m_attackType == Attack.AttackType.Projectile;
            BaseProjectiles = Mathf.Max(1, clone.m_projectiles);
            if (Twin)
            {
                clone.m_projectiles = BaseProjectiles * 2;
            }
            if (Multi)
            {
                // Volley's spread (judgement call): at least 4 degrees either way, so three arrows never fly as one.
                clone.m_projectiles = BaseProjectiles * 3;
                clone.m_projectileAccuracy = Mathf.Max(clone.m_projectileAccuracy, 4f);
                clone.m_projectileAccuracyMin = Mathf.Max(clone.m_projectileAccuracyMin, 4f);
            }
        }

        // The stamina cost is measured the way the game will measure it (the clone needs its character and weapon for
        // that; Start assigns the same two values first thing).
        private static bool Costs(Attack clone, Humanoid character, ItemDrop.ItemData weapon, ItemLocalSums sums)
        {
            float rune = sums.Get(EffectKind.RuneEdge), blood = sums.Get(EffectKind.BloodPrice);
            if (rune <= 0f && blood <= 0f)
            {
                return true;
            }
            clone.m_character = character;
            clone.m_weapon = weapon;
            float stamina = clone.GetAttackStamina();
            if (stamina <= 0f)
            {
                return true;
            }
            if (blood > 0f)
            {
                return PayInBlood(character, stamina);
            }
            if (character.HaveEitr(stamina / 2f + 0.1f))
            {
                RuneEitr = stamina / 2f;
                clone.m_damageMultiplier *= 1f + rune;
            }
            return true;
        }

        private static bool PayInBlood(Humanoid character, float cost)
        {
            if (character.GetHealth() <= cost + 1f)
            {
                Hud.instance?.FlashHealthBar();
                Current = null;
                return false;
            }
            BloodHealth = cost;
            return true;
        }

        public static bool Is(Attack attack) => Current != null && ReferenceEquals(attack, Current);
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
    internal static class AttackStartPatch
    {
        private static bool Prefix(Attack __instance, Humanoid character, ItemDrop.ItemData weapon, ref bool __result)
        {
            if (!ReferenceEquals(character, Player.m_localPlayer) || weapon == null)
            {
                return true;
            }
            if (AttackClones.OnStart(__instance, character, weapon))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    /// <summary>Stamina of the marked swing: halved (Rune-Edged, the other half is eitr) or nothing (Blood Price).</summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.GetAttackStamina))]
    internal static class SwingStaminaPatch
    {
        private static void Postfix(Attack __instance, ref float __result)
        {
            if (!AttackClones.Is(__instance) || __result <= 0f)
            {
                return;
            }
            if (AttackClones.BloodHealth > 0f)
            {
                __result = 0f;
            }
            else if (AttackClones.RuneEitr > 0f)
            {
                __result *= 0.5f;
            }
        }
    }

    /// <summary>Eitr of the marked swing: doubled (Twincast) or plus the Rune-Edged half.</summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.GetAttackEitr), new[] { typeof(Character), typeof(ItemDrop.ItemData) })]
    internal static class SwingEitrPatch
    {
        private static void Postfix(Attack __instance, ref float __result)
        {
            if (!AttackClones.Is(__instance))
            {
                return;
            }
            if (AttackClones.Twin)
            {
                __result *= 2f;
            }
            __result += AttackClones.RuneEitr;
        }
    }

    /// <summary>
    /// Health per attack: Blood Thrift lowers a staff's own health cost (item-local), Blood Price adds the swing's
    /// stamina cost as health. The game never lets a health cost kill (it spends at most health - 1).
    /// </summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.GetAttackHealth))]
    internal static class SwingHealthPatch
    {
        private static void Postfix(Attack __instance, ref float __result)
        {
            if (__result > 0f)
            {
                ItemLocalSums? sums = ItemLocalCache.Get(__instance.m_weapon);
                if (sums != null)
                {
                    __result *= Mathf.Max(0f, 1f - sums.Get(EffectKind.AttackHealthCost));
                }
            }
            if (AttackClones.Is(__instance))
            {
                __result += AttackClones.BloodHealth;
            }
        }
    }
}
