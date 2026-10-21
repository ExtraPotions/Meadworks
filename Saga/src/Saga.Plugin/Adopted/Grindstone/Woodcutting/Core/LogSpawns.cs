// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Core/LogSpawns.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Logs and stumps spawned by a fell or a break, on the machine that spawns them (the tree's or the log's owner,
    /// which owns the new objects). Instantiate runs Awake at once, inside SpawnLog or TreeLog.Destroy:
    /// <list type="bullet">
    /// <item>During a <see cref="Felling"/>, the new log gets the woodcutter on its ZDO and becomes the fell's log, and
    /// the new tree-type Destructible becomes its stub.</item>
    /// <item>During a <see cref="LogBreaking"/>, each half gets the whole log's feller, its clean mark and its size.</item>
    /// </list>
    /// </summary>
    public static class LogSpawns
    {
        private static readonly int CleanHash = Keys.CleanSplit.GetStableHashCode();
        private static readonly int SizeHash = Keys.WoodSize.GetStableHashCode();

        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Awake))]
        private static class LogAwake
        {
            [HarmonyPostfix]
            private static void Postfix(TreeLog __instance)
            {
                if (Felling.Open != null || LogBreaking.Open != null)
                    HookGuard.Run("log spawn", static log => Spawned(log), __instance);
            }
        }

        private static void Spawned(TreeLog log)
        {
            ZNetView nview = log.m_nview;
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
                return;
            if (Felling.Open != null)
                FellLog(Felling.Open, log, nview.GetZDO());
            else if (LogBreaking.Open != null)
                Inherit(LogBreaking.Open, nview.GetZDO());
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Awake))]
        private static class StubAwake
        {
            [HarmonyPostfix]
            private static void Postfix(Destructible __instance)
            {
                FellContext fell = Felling.Open;
                if (fell != null && fell.Stub == null && WoodSkill.IsWood(__instance))
                    fell.Stub = __instance;
            }
        }

        private static void FellLog(FellContext fell, TreeLog log, ZDO zdo)
        {
            if (fell.Log == null)
                fell.Log = log;
            fell.Woodcutter?.WriteTo(zdo);
        }

        private static void Inherit(BreakContext broken, ZDO zdo)
        {
            broken.Feller?.WriteTo(zdo);
            if (broken.Clean)
                zdo.Set(CleanHash, true);
            if (broken.Size >= 0f)
                zdo.Set(SizeHash, broken.Size);
        }
    }
}
