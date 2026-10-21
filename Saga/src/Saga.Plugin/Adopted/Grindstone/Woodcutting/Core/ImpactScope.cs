// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Core/ImpactScope.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A felled log that is hitting something right now. ImpactEffect.OnCollisionEnter runs on the log's ZDO owner and
    /// damages whatever the log struck with a hit that has no attacker. A prefix opens the scope with the woodcutter
    /// stored on the log (<see cref="Woodcutter.FromZdo"/>), a finalizer closes it. Inside it, <see cref="WoodHit"/> tags
    /// impacts on wood for the chain, and <see cref="Timber"/> softens the log's hit on its own woodcutter.
    /// Logs without a stored woodcutter (felled before GrindstoneSkills, or by fire) open no scope.
    /// </summary>
    public static class ImpactScope
    {
        /// <summary>The woodcutter of the log whose impact is being handled; null outside a scope.</summary>
        public static Woodcutter Current { get; private set; }

        /// <summary>The log whose impact is being handled; null outside a scope.</summary>
        public static TreeLog Log { get; private set; }

        [HarmonyPatch(typeof(ImpactEffect), nameof(ImpactEffect.OnCollisionEnter))]
        private static class Impact
        {
            // Every collision of every ImpactEffect (ships, carts, logs) on every machine comes here: the cheap checks
            // run first, so only an owned impact while Woodcutting is on looks for a log.
            [HarmonyPrefix]
            private static void Prefix(ImpactEffect __instance, out bool __state) =>
                __state = Current == null && WoodSkill.Active && IsOwned(__instance)
                    && HookGuard.Run("log impact", static effect => Begin(effect), __instance, false);

            [HarmonyFinalizer]
            private static void Finalizer(bool __state) => End(__state);
        }

        private static bool IsOwned(ImpactEffect effect) => effect.m_nview != null && effect.m_nview.IsOwner();

        private static bool Begin(ImpactEffect effect)
        {
            if (Current != null || !WoodSkill.Active)
                return false;
            TreeLog log = effect.GetComponent<TreeLog>();
            ZNetView nview = log != null ? log.m_nview : null;
            Woodcutter woodcutter = nview != null && nview.IsValid() && nview.IsOwner() ? Woodcutter.FromZdo(nview.GetZDO()) : null;
            if (woodcutter == null)
                return false;
            Current = woodcutter;
            Log = log;
            return true;
        }

        private static void End(bool opened)
        {
            if (!opened)
                return;
            Current = null;
            Log = null;
        }
    }
}
