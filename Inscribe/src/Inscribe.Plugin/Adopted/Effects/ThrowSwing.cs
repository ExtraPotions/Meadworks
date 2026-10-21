// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Throwing Grip (effect <c>throwable</c>): the local player's secondary attack with a one-handed sword, axe, mace or
    /// knife that carries it is the spear throw. Humanoid.StartAttack clones the weapon's secondary attack for every
    /// swing; a prefix on Attack.Start, ahead of <see cref="AttackClones"/> (so Long Reach and the cost rewrites apply
    /// to the throw), overwrites that clone's settings with the spear throw's (<see cref="ThrownWeapon.Throw"/>), for
    /// this swing only. The game then does what it does for a spear: the throw animation, the projectile with the
    /// weapon's own damage, quality, skill and status effect (FireProjectileBurst reads them from the weapon, not the
    /// attack), the weapon out of the hand and the inventory, and the very item dropped where the projectile lands.
    /// Judgement call: the stamina is the weapon's own secondary cost or the throw's, whichever is higher, so a throw is
    /// never cheaper than the swing it replaces. The thrower's client decides; every other client sees the animation
    /// trigger and the projectile through the game's own sync.
    /// </summary>
    internal static class ThrowSwing
    {
        // An attack's settings are its public fields; its runtime state (character, weapon, timers) is private. They are
        // copied by a method compiled once (plain field assignments, no boxing); by reflection if that cannot be built.
        private static readonly FieldInfo[] Settings = typeof(Attack).GetFields(BindingFlags.Public | BindingFlags.Instance);
        private static readonly Action<Attack, Attack>? CopySettings = BuildCopier();

        /// <summary>True while the local player's Humanoid.StartAttack runs for a secondary attack.</summary>
        public static bool Secondary;

        public static void OnStart(Attack clone, ItemDrop.ItemData weapon)
        {
            Attack? spear = ThrownWeapon.Throw;
            if (!Secondary || spear == null || !IsThrowable(weapon))
            {
                return;
            }
            ItemLocalSums? sums = ItemLocalCache.Get(weapon);
            if (sums == null || sums.Get(EffectKind.Throwable) <= 0f)
            {
                return;
            }
            float stamina = clone.m_attackStamina;
            Copy(spear, clone);
            clone.m_attackStamina = Mathf.Max(stamina, spear.m_attackStamina);
        }

        private static void Copy(Attack from, Attack to)
        {
            if (CopySettings != null)
            {
                CopySettings(to, from);
                return;
            }
            foreach (FieldInfo field in Settings)
            {
                field.SetValue(to, field.GetValue(from));
            }
        }

        // to.field = from.field for every setting; null (the reflection loop) when a field cannot be assigned this way.
        private static Action<Attack, Attack>? BuildCopier()
        {
            try
            {
                ParameterExpression to = Expression.Parameter(typeof(Attack), "to");
                ParameterExpression from = Expression.Parameter(typeof(Attack), "from");
                List<Expression> body = new List<Expression>();
                foreach (FieldInfo field in Settings)
                {
                    body.Add(Expression.Assign(Expression.Field(to, field), Expression.Field(from, field)));
                }
                return body.Count == 0 ? null : Expression.Lambda<Action<Attack, Attack>>(Expression.Block(body), to, from).Compile();
            }
            catch (Exception e)
            {
                Core.Log.Warn($"Throwing Grip copies the throw by reflection: {e.Message}");
                return null;
            }
        }

        // A single one-handed sword, axe, mace or knife: the classes the inscription rolls on, checked again because an
        // admin can inscribe anything.
        private static bool IsThrowable(ItemDrop.ItemData weapon)
        {
            ItemDrop.ItemData.SharedData shared = weapon.m_shared;
            if (shared.m_itemType != ItemDrop.ItemData.ItemType.OneHandedWeapon || shared.m_maxStackSize > 1 || weapon.m_dropPrefab == null)
            {
                return false;
            }
            Skills.SkillType skill = shared.m_skillType;
            return skill == Skills.SkillType.Swords || skill == Skills.SkillType.Axes || skill == Skills.SkillType.Clubs
                || skill == Skills.SkillType.Knives;
        }
    }

    /// <summary>Marks the local player's secondary attacks for <see cref="ThrowSwingPatch"/> (Attack.Start has no flag).</summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    internal static class ThrowSecondaryPatch
    {
        private static void Prefix(Humanoid __instance, bool secondaryAttack) =>
            ThrowSwing.Secondary = secondaryAttack && ReferenceEquals(__instance, Player.m_localPlayer);

        private static void Postfix() => ThrowSwing.Secondary = false;
    }

    /// <summary>Before <see cref="AttackStartPatch"/>: the swing clone becomes the throw first, then gets the other per-swing changes.</summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
    internal static class ThrowSwingPatch
    {
        [HarmonyPriority(Priority.High)]
        private static void Prefix(Attack __instance, Humanoid character, ItemDrop.ItemData weapon)
        {
            if (weapon != null && ReferenceEquals(character, Player.m_localPlayer))
            {
                ThrowSwing.OnStart(__instance, weapon);
            }
        }
    }
}
