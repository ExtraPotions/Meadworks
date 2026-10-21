// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Beast Whisperer, on the taming creature's owner (Tameable.DecreaseRemainingTime runs there, every few seconds
    /// while it is being tamed, and on feeding): the progress step is X% larger, X being the best value among players
    /// within the game's own taming-boost range, read from their published stats.
    /// </summary>
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.DecreaseRemainingTime))]
    internal static class TamingPatch
    {
        private static readonly List<Player> Nearby = new List<Player>();

        private static void Prefix(Tameable __instance, ref float time)
        {
            Nearby.Clear();
            Player.GetPlayersInRange(__instance.transform.position, __instance.m_tamingSpeedMultiplierRange, Nearby);
            float best = 0f;
            foreach (Player player in Nearby)
            {
                best = Mathf.Max(best, PlayerStats.Of(player, PlayerStats.Taming));
            }
            time *= 1f + best;
        }
    }

    /// <summary>
    /// Fair Winds, on the ship's owner (sail physics run there; the owner is not always the helmsman): the sail force is
    /// X% larger, X being the published value of the player at the helm. Every physics step, so the helmsman's Player
    /// is kept per ship for its user id (looked up again when the user changes, or at most once a second while that
    /// player is not loaded here).
    /// </summary>
    [HarmonyPatch(typeof(Ship), nameof(Ship.GetSailForce))]
    internal static class SailPatch
    {
        private const float RetrySeconds = 1f;

        private static readonly ConditionalWeakTable<Ship, Helm> Helms = new ConditionalWeakTable<Ship, Helm>();

        private sealed class Helm
        {
            public long User;
            public Player? Player;
            public float RetryAt;
        }

        private static void Postfix(Ship __instance, ref Vector3 __result)
        {
            long user = __instance.m_shipControlls != null ? __instance.m_shipControlls.GetUser() : 0L;
            if (user != 0L)
            {
                __result *= 1f + PlayerStats.Of(Helmsman(__instance, user), PlayerStats.Sail);
            }
        }

        private static Player? Helmsman(Ship ship, long user)
        {
            Helm helm = Helms.GetOrCreateValue(ship);
            if (helm.User == user && (helm.Player != null || Time.time < helm.RetryAt))
            {
                return helm.Player;
            }
            helm.User = user;
            helm.Player = Player.GetPlayer(user);
            helm.RetryAt = Time.time + RetrySeconds;
            return helm.Player;
        }
    }

    /// <summary>
    /// Harvester, on the pickable's owner (Pickable.RPC_Pick drops the yield there): the picker - the player whose ZDO
    /// the sending peer owns - has an X% chance to get the base yield twice (the game's own bonus stays on top).
    /// </summary>
    [HarmonyPatch(typeof(Pickable), nameof(Pickable.RPC_Pick))]
    internal static class HarvestPatch
    {
        private static void Prefix(Pickable __instance, long sender, ref int bonus)
        {
            if (!__instance.m_nview.IsOwner() || __instance.m_picked)
            {
                return;
            }
            float chance = PlayerStats.Of(Picker(sender), PlayerStats.Harvest);
            if (chance > 0f && Random.value < chance)
            {
                bonus += __instance.m_dontScale ? __instance.m_amount
                    : Mathf.Max(__instance.m_minAmountScaled, Game.instance.ScaleDrops(__instance.m_itemPrefab, __instance.m_amount));
            }
        }

        private static Player? Picker(long peer)
        {
            foreach (Player player in Player.GetAllPlayers())
            {
                ZDO? zdo = player.m_nview != null ? player.m_nview.GetZDO() : null;
                if (zdo != null && zdo.GetOwner() == peer)
                {
                    return player;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Deep Vein and Heartwood, on the resource's owner. While the owner resolves a hit that may destroy a rock, a
    /// mineable area, a tree, a log or a destructible (stumps, small rocks), the hit is remembered; when that object's
    /// drop table is rolled, X more of its resource are added: X is the attacker's published Deep Vein total for a
    /// pickaxe hit, Heartwood for a chopping hit. "Its resource" is the item the rolled list holds most of (judgement
    /// call: ore over stone in a deposit, wood in a log).
    /// </summary>
    internal static class DropYield
    {
        private static HitData? _hit;

        public static void Begin(HitData hit) => _hit = hit;

        public static void End() => _hit = null;

        public static void AddExtra(List<GameObject> drops)
        {
            HitData? hit = _hit;
            _hit = null;
            if (hit == null || drops.Count == 0)
            {
                return;
            }
            int stat = hit.m_damage.m_pickaxe > 0f ? PlayerStats.YieldMining : hit.m_damage.m_chop > 0f ? PlayerStats.YieldLumber : -1;
            int extra = stat < 0 ? 0 : Mathf.RoundToInt(PlayerStats.OfAttacker(hit, stat));
            GameObject resource = MostCommon(drops);
            for (int i = 0; i < extra; i++)
            {
                drops.Add(resource);
            }
        }

        private static GameObject MostCommon(List<GameObject> drops)
        {
            GameObject best = drops[0];
            int bestCount = 0;
            foreach (GameObject candidate in drops)
            {
                int count = 0;
                foreach (GameObject other in drops)
                {
                    count += ReferenceEquals(other, candidate) ? 1 : 0;
                }
                if (count > bestCount)
                {
                    best = candidate;
                    bestCount = count;
                }
            }
            return best;
        }
    }

    /// <summary>The owner-side methods that resolve a hit named <c>hit</c> and may roll a drop table while doing so.</summary>
    [HarmonyPatch]
    internal static class DropYieldHitPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TreeBase), nameof(TreeBase.RPC_Damage));
            yield return AccessTools.Method(typeof(MineRock), nameof(MineRock.RPC_Hit));
            yield return AccessTools.Method(typeof(MineRock5), nameof(MineRock5.DamageArea));
            yield return AccessTools.Method(typeof(Destructible), nameof(Destructible.RPC_Damage));
        }

        private static void Prefix(HitData hit) => DropYield.Begin(hit);

        private static void Postfix() => DropYield.End();
    }

    /// <summary>A log's destruction (from its own damage RPC, on its owner).</summary>
    [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Destroy), new[] { typeof(HitData), typeof(bool) })]
    internal static class DropYieldLogPatch
    {
        private static void Prefix(HitData hitData) => DropYield.Begin(hitData);

        private static void Postfix() => DropYield.End();
    }

    /// <summary>Every drop-table roll; does nothing unless a hit above is being resolved.</summary>
    [HarmonyPatch(typeof(DropTable), nameof(DropTable.GetDropList), new[] { typeof(int) })]
    internal static class DropListPatch
    {
        private static void Postfix(List<GameObject> __result) => DropYield.AddExtra(__result);
    }
}
