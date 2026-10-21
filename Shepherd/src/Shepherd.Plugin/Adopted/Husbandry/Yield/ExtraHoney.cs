// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Yield/ExtraHoney.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; NativeHusbandry exception guard; extra count by the portable YieldMath.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Shepherd.Yield;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Extra honey and honey experience, on the hive's owner. Beehive.Interact on the harvester's client sends
    /// RPC_Extract to the owner, which spawns one honey per level and empties the hive. A prefix on RPC_Extract notes the
    /// honey count on the owner; a postfix, once the hive is really empty, finds the harvester (the loaded player whose
    /// character the caller owns), rolls per honey their share of "Extra Honey At 100" for one more, at their published
    /// Husbandry level, and sends their client one Honey credit per honey (<see cref="HusbandryCredit"/>). So a second
    /// press within a round trip, or two players harvesting together, are paid only for the honey the owner hands out.
    /// Extra honey is spawned as RPC_Extract spawns it: a networked instance at the hive's spawn point.
    /// </summary>
    public static class ExtraHoney
    {
        private const float Spread = 0.5f;
        private const float StackStep = 0.25f;

        [HarmonyPatch(typeof(Beehive), nameof(Beehive.RPC_Extract))]
        private static class Extract
        {
            [HarmonyPrefix]
            private static void Prefix(Beehive __instance, out int __state) =>
                __state = HusbandrySkill.Active && __instance.m_nview != null && __instance.m_nview.IsOwner()
                    ? __instance.GetHoneyLevel() : 0;

            [HarmonyPostfix]
            private static void Postfix(Beehive __instance, long caller, int __state)
            {
                if (__state > 0 && __instance.GetHoneyLevel() == 0)
                    NativeHusbandry.Run("extra honey", static call => Reward(call.hive, call.caller, call.count),
                        (hive: __instance, caller, count: __state));
            }
        }

        private static void Reward(Beehive hive, long caller, int count)
        {
            Player? harvester = PlayerOfPeer(caller);
            if (harvester == null)
                return;
            long playerId = harvester.GetPlayerID();
            for (int i = 0; i < count; i++)
                HusbandryCredit.Send(playerId, HusbandryCredit.Kind.Honey, "");
            float share = HusbandrySkill.Share(HusbandryYieldSettings.ExtraHoney.Value, HusbandrySkill.Of(harvester));
            int extra = YieldMath.ExtraHoney(count, share, YieldDice.Instance);
            for (int i = 0; i < extra; i++)
                Spawn(hive, count + i);
        }

        /// <summary>The loaded player whose character ZDO the peer owns (a player always owns their own), or null.</summary>
        private static Player? PlayerOfPeer(long peer)
        {
            foreach (Player player in Player.GetAllPlayers())
            {
                ZDO? zdo = player != null && player.m_nview != null ? player.m_nview.GetZDO() : null;
                if (zdo != null && zdo.GetOwner() == peer)
                    return player;
            }
            return null;
        }

        private static void Spawn(Beehive hive, int index)
        {
            if (hive.m_honeyItem == null || hive.m_spawnPoint == null)
                return;
            Vector2 circle = Random.insideUnitCircle * Spread;
            Vector3 position = hive.m_spawnPoint.position + new Vector3(circle.x, StackStep * index, circle.y);
            ItemDrop honey = Object.Instantiate(hive.m_honeyItem, position, Quaternion.identity);
            if (honey != null)
                honey.SetStack(Game.instance.ScaleDrops(hive.m_honeyItem.m_itemData, 1));
        }
    }
}
