// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects.Combat3
{
    /// <summary>
    /// The hits the attacker-side combat effects act on, and the weapon behind each. Character.Damage runs where a hit is
    /// built: the local player's own client for its swings, projectiles and spells, which then sends the hit to the
    /// target's owner, so anything changed in the HitData travels with it. A hit counts when its attacker is the local
    /// player and it has a skill (a weapon, projectile or spell hit; Bramblehide's returned damage has none), and it
    /// is not one of the player's own secondary hits (<see cref="SecondaryHits"/>).
    /// <para>
    /// The weapon: a projectile or area effect carries the weapon that launched it, set here while it resolves its
    /// hits (Projectile.OnHit, Aoe.OnHit), so an arrow still in flight counts its own bow after the player switched to
    /// a sword; any other hit is the weapon in hand.
    /// </para>
    /// </summary>
    internal static class LocalHits
    {
        private static bool _inShot;
        private static ItemDrop.ItemData? _shotWeapon;

        /// <summary>The local player when it built this weapon hit, else null (also null while effects are off).</summary>
        public static Player? Attacker(HitData hit)
        {
            Player? player = Player.m_localPlayer;
            if (player == null || SecondaryHits.Active || !ItemEffects.Enabled || hit.m_skill == Skills.SkillType.None)
            {
                return null;
            }
            return hit.m_attacker == player.GetZDOID() ? player : null;
        }

        /// <summary>The weapon behind the local player's hit being resolved.</summary>
        public static ItemDrop.ItemData? Weapon(Player player) => _inShot ? _shotWeapon : player.GetCurrentWeapon();

        public static void EnterShot(Character? owner, ItemDrop.ItemData? weapon)
        {
            Player? player = Player.m_localPlayer;
            _inShot = player != null && ReferenceEquals(owner, player);
            _shotWeapon = _inShot ? weapon : null;
        }

        public static void ExitShot()
        {
            _inShot = false;
            _shotWeapon = null;
        }
    }

    /// <summary>
    /// A projectile's hit (on its owner, the shooter's client): the weapon context while it resolves, and Bursting Shot
    /// (<see cref="ExplosiveShot"/>) once it has really hit (not bounced).
    /// </summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
    internal static class ProjectileHitPatch
    {
        private static void Prefix(Projectile __instance, out bool __state)
        {
            __state = __instance.m_didHit;
            LocalHits.EnterShot(__instance.m_owner, __instance.m_weapon);
        }

        private static void Postfix(Projectile __instance, Collider collider, Vector3 hitPoint, bool __state)
        {
            LocalHits.ExitShot();
            if (!__state && __instance.m_didHit)
            {
                ExplosiveShot.OnImpact(__instance, collider, hitPoint);
            }
        }

        private static System.Exception? Finalizer(System.Exception? __exception)
        {
            LocalHits.ExitShot();
            return __exception;
        }
    }

    /// <summary>An area effect's hit (a staff's blast): its own weapon while it resolves.</summary>
    [HarmonyPatch(typeof(Aoe), nameof(Aoe.OnHit))]
    internal static class AoeHitPatch
    {
        private static void Prefix(Aoe __instance) => LocalHits.EnterShot(__instance.m_owner, __instance.m_itemData);

        private static System.Exception? Finalizer(System.Exception? __exception)
        {
            LocalHits.ExitShot();
            return __exception;
        }
    }
}
