// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Core/LogBreaking.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Hits on logs and logs breaking, on the log's ZDO owner.
    /// <list type="bullet">
    /// <item>TreeLog.RPC_Damage handles every hit there. A prefix shows a woodcutting hit to <see cref="CleanSplits"/>
    /// before the game applies it.</item>
    /// <item>When the log's health reaches 0 the game calls TreeLog.Destroy(hit copy, cheated): it destroys the log's
    /// ZDO, spawns the drop table's items (a half) and the sub-logs (a whole log). A prefix reads everything into a
    /// <see cref="BreakContext"/> first, asks <see cref="OldGrowth"/> and <see cref="CleanSplits"/> for their yield
    /// bonus (added together) and scales the drop count for this call; <see cref="LogSpawns"/> passes the feller and
    /// the clean mark to the halves. Split experience is reported from that prefix, while the log ZDO still exists.
    /// A postfix hands the context to clean splits; a finalizer puts the drop table back.</item>
    /// </list>
    /// The yield bonus scales the number of drop rolls: the game rolls m_dropMin..m_dropMax, the prefix rolls that same
    /// count itself, multiplies it by 1 + bonus, rounds up or down at random by the remainder and fixes the table to it.
    /// </summary>
    public static class LogBreaking
    {
        private static readonly int CleanHash = Keys.CleanSplit.GetStableHashCode();
        private static readonly int SizeHash = Keys.WoodSize.GetStableHashCode();

        /// <summary>The break being handled right now; null outside TreeLog.Destroy.</summary>
        public static BreakContext Open { get; private set; }

        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.RPC_Damage))]
        private static class LogHit
        {
            [HarmonyPrefix]
            private static void Prefix(TreeLog __instance, HitData hit)
            {
                Woodcutter woodcutter = WoodSkill.Active && IsOwned(__instance) ? Woodcutter.FromHit(hit) : null;
                if (woodcutter != null)
                    HookGuard.Run("clean splits", static split => CleanSplits.OnLogHit(split.log, split.hit, split.woodcutter), (log: __instance, hit, woodcutter));
            }
        }

        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Destroy))]
        private static class Break
        {
            [HarmonyPrefix]
            private static void Prefix(TreeLog __instance, HitData hitData, out DropCount __state) =>
                __state = HookGuard.Run("log break", static broken => Begin(broken.log, broken.hitData), (log: __instance, hitData), (DropCount)null);

            [HarmonyPostfix]
            private static void Postfix()
            {
                BreakContext broken = Open;
                if (broken != null)
                    Dispatch(broken);
            }

            [HarmonyFinalizer]
            private static void Finalizer(TreeLog __instance, DropCount __state)
            {
                Open = null;
                __state?.Restore(__instance.m_dropWhenDestroyed);
            }
        }

        private static DropCount Begin(TreeLog log, HitData hit)
        {
            Open = null;
            if (!WoodSkill.Active || !IsOwned(log))
                return null;
            Open = Read(log, hit);
            Open.YieldBonus = Bonus(Open);
            HookGuard.Run("split experience", () => WoodXp.OnLogBroken(Open));
            return Open.DropsWood && Open.YieldBonus > 0f ? DropCount.Scale(log.m_dropWhenDestroyed, 1f + Open.YieldBonus) : null;
        }

        private static BreakContext Read(TreeLog log, HitData hit)
        {
            ZDO zdo = log.m_nview.GetZDO();
            return new BreakContext
            {
                Log = log,
                LogPrefab = WoodSkill.PrefabName(log),
                Position = log.transform.position,
                Scale = log.transform.localScale,
                Hit = hit,
                Breaker = Woodcutter.FromHit(hit),
                Feller = Woodcutter.FromZdo(zdo),
                Clean = zdo.GetBool(CleanHash),
                Size = zdo.GetFloat(SizeHash, -1f),
                DropsWood = log.m_dropWhenDestroyed != null && log.m_dropWhenDestroyed.m_drops.Count > 0,
            };
        }

        private static float Bonus(BreakContext broken)
        {
            float oldGrowth = HookGuard.Run("old growth", () => OldGrowth.Bonus(broken), 0f);
            float clean = HookGuard.Run("clean splits", () => CleanSplits.Bonus(broken), 0f);
            return Mathf.Max(0f, oldGrowth) + Mathf.Max(0f, clean);
        }

        private static void Dispatch(BreakContext broken)
        {
            HookGuard.Run("clean splits", () => CleanSplits.OnLogBroken(broken));
        }

        private static bool IsOwned(TreeLog log) =>
            log != null && log.m_nview != null && log.m_nview.IsValid() && log.m_nview.IsOwner();

        /// <summary>A drop table's roll count, fixed for one call and put back afterwards.</summary>
        public sealed class DropCount
        {
            private int min;
            private int max;

            public static DropCount Scale(DropTable table, float multiplier)
            {
                DropCount saved = new DropCount { min = table.m_dropMin, max = table.m_dropMax };
                float scaled = Random.Range(table.m_dropMin, table.m_dropMax + 1) * multiplier;
                int count = Mathf.FloorToInt(scaled) + (Random.value < scaled - Mathf.Floor(scaled) ? 1 : 0);
                table.m_dropMin = count;
                table.m_dropMax = count;
                return saved;
            }

            public void Restore(DropTable table)
            {
                if (table == null)
                    return;
                table.m_dropMin = min;
                table.m_dropMax = max;
            }
        }
    }
}
