// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// What a thrown weapon does when it comes down (effects <c>recall</c>, <c>apportation</c>), for a spear and for a
    /// Throwing Grip weapon alike.
    /// <para>
    /// A thrown weapon is a projectile that carries the very item which left the thrower's inventory
    /// (<c>Projectile.m_spawnItem</c>, set in Setup, on the thrower's client only: the field is never sent). The
    /// projectile's owner - the thrower's client, which created it - calls SpawnOnHit when it hits anything (and when
    /// its time runs out, if set to), and SpawnOnHit drops that item into the world as a networked ItemDrop with all its
    /// data. A prefix there, on the thrower's own client: Bifrost Step moves the thrower beside the creature hit
    /// (<see cref="Apportation"/>), then Returning puts the item back into the thrower's inventory and hand and clears
    /// the projectile's item, so nothing is dropped (<see cref="Recall"/>); with no room it falls as usual.
    /// </para>
    /// Item safety: an item rides one projectile only (a throw that fires two, with Twincast, drops it once), and a
    /// projectile drops it at most once (cleared after any spawn). Nothing is sent: the inventory is the thrower's own,
    /// a recalled item never became a world object, and the thrower's own position replicates as always.
    /// </summary>
    internal static class ThrownLanding
    {
        /// <summary>The local, living player who threw this item-carrying projectile, or null.</summary>
        public static Player? Thrower(Projectile projectile)
        {
            if (projectile.m_spawnItem == null || !(projectile.m_owner is Player player))
            {
                return null;
            }
            return ReferenceEquals(player, Player.m_localPlayer) && player != null && !player.IsDead() ? player : null;
        }

        /// <summary>Projectile.Setup postfix, the thrower's client, for a projectile carrying <paramref name="item"/>.</summary>
        public static void OnThrown(Projectile projectile, ItemDrop.ItemData item)
        {
            ThrownWeapon.ShowItem(projectile, item);
            if (RidesAnother(projectile, item))
            {
                projectile.m_spawnItem = null;
                return;
            }
            ItemLocalSums? sums = ItemLocalCache.Get(item);
            if (sums != null && sums.Get(EffectKind.Recall) > 0f)
            {
                // A throw that never hits anything (out over the sea) comes back when its time runs out.
                projectile.m_spawnOnTtl = true;
            }
        }

        /// <summary>SpawnOnHit prefix: <paramref name="hit"/> is what the projectile struck, null when its time ran out.</summary>
        public static void OnLanding(Projectile projectile, Player thrower, GameObject? hit)
        {
            ItemDrop.ItemData item = projectile.m_spawnItem;
            ItemLocalSums? sums = ItemLocalCache.Get(item);
            if (sums == null)
            {
                return;
            }
            if (hit != null && sums.Get(EffectKind.Apportation) > 0f)
            {
                Apportation.TryBlink(thrower, hit);
            }
            if (sums.Get(EffectKind.Recall) > 0f && Recall.TryReturn(thrower, item))
            {
                projectile.m_spawnItem = null;
            }
        }

        // The game fires a burst's projectiles in one loop and records each as the weapon's last projectile after its
        // Setup: while this one is set up, the last one is its sibling. A sibling still carrying the item keeps it.
        private static bool RidesAnother(Projectile projectile, ItemDrop.ItemData item)
        {
            GameObject previous = item.m_lastProjectile;
            if (previous == null || previous == projectile.gameObject)
            {
                return false;
            }
            Projectile other = previous.GetComponent<Projectile>();
            return other != null && ReferenceEquals(other.m_spawnItem, item);
        }
    }

    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
    internal static class ThrownSetupPatch
    {
        private static void Postfix(Projectile __instance, Character owner, ItemDrop.ItemData item)
        {
            if (item != null && __instance.m_spawnItem != null && owner != null && ReferenceEquals(owner, Player.m_localPlayer))
            {
                ThrownLanding.OnThrown(__instance, item);
            }
        }
    }

    /// <summary>
    /// Projectile.SpawnOnHit (private; the projectile's owner only, from its hit and its timeout). The postfix clears
    /// a player's thrown item once it was spawned, so a projectile that stays after its hit and later times out can never
    /// drop a second copy (the item is read nowhere else).
    /// </summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.SpawnOnHit))]
    internal static class ThrownLandingPatch
    {
        private static void Prefix(Projectile __instance, GameObject? go)
        {
            Player? thrower = ThrownLanding.Thrower(__instance);
            if (thrower != null)
            {
                ThrownLanding.OnLanding(__instance, thrower, go);
            }
        }

        private static void Postfix(Projectile __instance)
        {
            if (__instance.m_spawnItem != null && __instance.m_owner is Player)
            {
                __instance.m_spawnItem = null;
            }
        }
    }
}
