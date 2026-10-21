// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Timber Bite (<c>chop_damage</c>) and Stone Bite (<c>pickaxe_damage</c>): this axe's hits on trees and logs deal
    /// X% more chop damage, this pickaxe's hits on rock and ore X% more pickaxe damage. Item-local: the weapon's own sum.
    /// <para>
    /// The attacker's client builds a fresh hit for every object it strikes and hands it to the object's
    /// <c>Damage(HitData)</c>, which sends it to the object's owner; this prefix scales the hit's chop or pickaxe part
    /// there, before it is sent, so the owner applies the larger number with its own resistances and tool tier. A
    /// tree, a log or a tree-type destructible (a stump) takes the chop bonus; a rock, a deposit or another
    /// destructible the pickaxe bonus. Nothing else changes: creatures and building pieces never see it, and the
    /// weapon's tooltip keeps its base numbers. Only hits the local player deals with the weapon in hand.
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class ToolDamagePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TreeBase), nameof(TreeBase.Damage));
            yield return AccessTools.Method(typeof(TreeLog), nameof(TreeLog.Damage));
            yield return AccessTools.Method(typeof(MineRock), nameof(MineRock.Damage));
            yield return AccessTools.Method(typeof(MineRock5), nameof(MineRock5.Damage));
            yield return AccessTools.Method(typeof(Destructible), nameof(Destructible.Damage));
        }

        private static void Prefix(MonoBehaviour __instance, HitData hit)
        {
            ItemLocalSums? sums = WeaponSums(hit);
            if (sums == null || !(__instance is IDestructible target))
            {
                return;
            }
            if ((target.GetDestructibleType() & DestructibleType.Tree) != 0)
            {
                hit.m_damage.m_chop *= 1f + sums.Get(EffectKind.ChopDamage);
            }
            else
            {
                hit.m_damage.m_pickaxe *= 1f + sums.Get(EffectKind.PickaxeDamage);
            }
        }

        // The local player's weapon in hand, when this hit is the local player's own.
        private static ItemLocalSums? WeaponSums(HitData hit)
        {
            Player? player = Player.m_localPlayer;
            if (hit == null || player == null || hit.m_attacker != player.GetZDOID())
            {
                return null;
            }
            return ItemLocalCache.Get(player.GetCurrentWeapon());
        }
    }
}
