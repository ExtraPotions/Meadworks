// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Core/Felling.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A tree falling, on the tree's ZDO owner. TreeBase.RPC_Damage runs there for every hit; when the tree's health
    /// reaches 0 it calls SpawnLog (the log and the stub are instantiated there, owned by this machine), then drops the
    /// canopy items and destroys the tree.
    /// <list type="bullet">
    /// <item>A prefix on RPC_Damage remembers the hit being handled; a finalizer puts back the one before (none).</item>
    /// <item>A prefix on SpawnLog opens the fell while Woodcutting is on: <see cref="LogSpawns"/> stores the woodcutter
    /// on the new log's ZDO and notes the stub.</item>
    /// <item>A postfix on SpawnLog closes it and hands the <see cref="FellContext"/> to every felling feature, each
    /// guarded, in a fixed order: Old growth (the tree's size, stored on the log), Timber (push and callout), Clean fell
    /// (the stump), Replanting (needs the stump decided), Finds, then experience (the credit to the woodcutter).</item>
    /// </list>
    /// </summary>
    public static class Felling
    {
        private static HitData handling;

        /// <summary>The fell being spawned right now; null outside SpawnLog.</summary>
        public static FellContext Open { get; private set; }

        [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.RPC_Damage))]
        private static class Damage
        {
            [HarmonyPrefix]
            private static void Prefix(HitData hit, out HitData __state)
            {
                __state = handling;
                handling = hit;
            }

            [HarmonyFinalizer]
            private static void Finalizer(HitData __state) => handling = __state;
        }

        [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.SpawnLog))]
        private static class Spawn
        {
            [HarmonyPrefix]
            private static void Prefix(TreeBase __instance, Vector3 hitDir) =>
                Open = WoodSkill.Active ? HookGuard.Run("fell", () => Begin(__instance, hitDir), null) : null;

            [HarmonyPostfix]
            private static void Postfix()
            {
                FellContext fell = Open;
                Open = null;
                if (fell != null && WoodSkill.Active)
                    Dispatch(fell);
            }

            [HarmonyFinalizer]
            private static void Finalizer() => Open = null;
        }

        private static FellContext Begin(TreeBase tree, Vector3 hitDir)
        {
            Transform transform = tree.transform;
            return new FellContext
            {
                Tree = tree,
                TreePrefab = WoodSkill.PrefabName(tree),
                Species = tree.m_logPrefab != null ? tree.m_logPrefab.name : WoodSkill.PrefabName(tree),
                Position = transform.position,
                Scale = transform.localScale,
                HitDir = hitDir,
                Hit = handling,
                Woodcutter = Woodcutter.FromHit(handling),
                Biome = Heightmap.FindBiome(transform.position),
            };
        }

        private static void Dispatch(FellContext fell)
        {
            HookGuard.Run("old growth", () => OldGrowth.OnFelled(fell));
            HookGuard.Run("timber", () => Timber.OnFelled(fell));
            HookGuard.Run("clean fell", () => CleanFell.OnFelled(fell));
            HookGuard.Run("replanting", () => Replanting.OnFelled(fell));
            HookGuard.Run("finds", () => Finds.OnFelled(fell));
            HookGuard.Run("fell experience", () => WoodXp.OnFelled(fell));
        }
    }
}
