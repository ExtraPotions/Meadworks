// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The gold a raider carries, dropped where it dies (features/raids.md section 3, "The gold"): exactly the share in
    /// its tag, however it died - a player, a trap, another creature, a fall, fire, water - except when a stop killed it
    /// or its raid was stopped (<see cref="RaiderPurse.Forfeit"/>), or its raid was lost first: it left with them
    /// (<see cref="RaiderPurse.Lost"/>, <see cref="RaiderPurse.WalkOff"/>). Never multiplied by the raid's loot
    /// multiplier. Runs on the raider's owner as the game's death begins (<see cref="RaidCoinPatch"/>), while its ZDO
    /// still answers. The share is emptied before anything drops, so a death that runs twice, or an owner that changes,
    /// pays once. The coins fall as full stacks rather than one object per coin, as the game's own drop list would make
    /// them: a Warlord's quarter of a deadly raid's purse is hundreds of coins. A raider that walks off and vanishes never
    /// dies, and pays nothing here.
    /// </summary>
    internal static class RaidCoins
    {
        private const string CoinsPrefab = "Coins";

        private static readonly int CoinsHash = CoinsPrefab.GetStableHashCode();

        /// <summary>Metres the stacks scatter around the death spot: the game's own drop area.</summary>
        private const float Spread = 0.5f;

        /// <summary>The upward kick a drop gets, as the game's drop list gives its items.</summary>
        private const float Kick = 5f;

        private static bool _warnedMissing;

        /// <summary>A dying raider's share, on its owner. Nothing when it carries none, or forfeits it.</summary>
        public static void Drop(Character raider, ZDO zdo)
        {
            int coins = RaiderTag.Coins(zdo);
            if (coins <= 0)
            {
                return; // none, already paid, or the stop's mark (below zero), which the loot step still reads
            }
            RaiderTag.ClearCoins(zdo);
            if (RaiderPurse.Forfeit(zdo) || RaiderPurse.Lost(zdo))
            {
                return;
            }
            Scatter(coins, raider.GetCenterPoint());
            if (Log.Diagnostics)
            {
                Log.Diag($"raider {raider.name} fell carrying {coins} coins");
            }
        }

        private static void Scatter(int coins, Vector3 at)
        {
            GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(CoinsHash) : null;
            ItemDrop? item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (item == null)
            {
                WarnMissingOnce();
                return;
            }
            int stack = Mathf.Max(1, item.m_itemData.m_shared.m_maxStackSize);
            for (int left = coins; left > 0; left -= stack)
            {
                DropStack(prefab!, Mathf.Min(left, stack), at);
            }
        }

        private static void WarnMissingOnce()
        {
            if (!_warnedMissing)
            {
                _warnedMissing = true;
                Log.Warn("raid coins: the game's Coins item was not found, so raiders' shares are not dropped");
            }
        }

        // One stack, made the way the game drops a creature's items (its world level, a random turn, a small kick up), on
        // this machine, which owns the new item until a player picks it up.
        private static void DropStack(GameObject prefab, int amount, Vector3 at)
        {
            Vector3 pos = at + Random.insideUnitSphere * Spread;
            GameObject coins = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            ItemDrop item = coins.GetComponent<ItemDrop>();
            item.m_itemData.m_worldLevel = (byte)Game.m_worldLevel;
            item.SetStack(amount);
            Rigidbody body = coins.GetComponent<Rigidbody>();
            if (body != null)
            {
                Vector3 push = Random.insideUnitSphere;
                push.y = Mathf.Abs(push.y);
                body.AddForce(push * Kick, ForceMode.VelocityChange);
            }
        }
    }
}
