// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The kraken's mind replaces the serpent's AI it was copied from: on a kraken the AI's update runs
    /// <see cref="KrakenBrain.Think"/> instead, on every machine (the game's AI only acts on the owner; the brain checks).
    /// Every other creature's AI runs untouched.
    /// </summary>
    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
    public static class KrakenAIPatch
    {
        private static bool Prefix(MonsterAI __instance, float dt, ref bool __result)
        {
            // Every monster's AI passes here many times a second, server included: nothing to look up without a kraken.
            KrakenBrain? brain = KrakenBrain.Loaded.Count == 0 ? null : Find(__instance);
            if (brain == null)
            {
                return true;
            }
            SafeCall.Run("MonsterAI.UpdateAI kraken", static (mind, step) => mind.Think(step), brain, dt);
            __result = false;
            return false;
        }

        /// <summary>The loaded kraken on this game object, or null (a handful of krakens at most are ever loaded).</summary>
        public static KrakenBrain? Find(Component part)
        {
            foreach (KrakenBrain brain in KrakenBrain.Loaded)
            {
                if (brain != null && brain.gameObject == part.gameObject)
                {
                    return brain;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// While the kraken holds a ship or leaves, its body is fixed and placed by hand (<see cref="KrakenBrain.Place"/>):
    /// the game's own movement for it (swimming, which would pull it back to the surface) is skipped. Runs on the owner.
    /// </summary>
    [HarmonyPatch(typeof(Character), "UpdateMotion")]
    public static class KrakenMotionPatch
    {
        private static bool Prefix(Character __instance) =>
            KrakenBrain.Loaded.Count == 0 || !(KrakenAIPatch.Find(__instance)?.Pinned ?? false);
    }

    /// <summary>A kraken staggered (on its owner, where the game staggers it): its brain calls off what was coming.</summary>
    [HarmonyPatch(typeof(Character), "RPC_Stagger")]
    public static class KrakenStaggerPatch
    {
        private static void Postfix(Character __instance)
        {
            if (KrakenBrain.Loaded.Count > 0)
            {
                KrakenAIPatch.Find(__instance)?.OnStagger();
            }
        }
    }

    /// <summary>A held ship, on the machine that sails it: no sail before the game moves it, kept in place after (<see cref="ShipHold"/>).</summary>
    [HarmonyPatch(typeof(Ship), nameof(Ship.CustomFixedUpdate))]
    public static class ShipHoldPatch
    {
        private static void Prefix(Ship __instance)
        {
            if (KrakenBrain.Loaded.Count > 0)
            {
                SafeCall.Run("Ship.CustomFixedUpdate kraken hold", static ship => ShipHold.Furl(ship), __instance);
            }
        }

        private static void Postfix(Ship __instance)
        {
            if (KrakenBrain.Loaded.Count > 0)
            {
                SafeCall.Run("Ship.CustomFixedUpdate kraken hold", static ship => ShipHold.Keep(ship), __instance);
            }
        }
    }

    /// <summary>
    /// A hit on a ship, where the game applies it (the ship's owner): while a kraken holds the ship, the players' own blows
    /// do it no harm (<see cref="ShipHold.Spares"/>), so swinging at the kraken over the rail never breaks the ship.
    /// </summary>
    [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
    public static class KrakenShipGuardPatch
    {
        private static bool Prefix(WearNTear __instance, HitData hit)
        {
            if (KrakenBrain.Loaded.Count == 0 || hit == null)
            {
                return true;
            }
            return !SafeCall.Run("WearNTear.RPC_Damage kraken", static (part, blow) => ShipHold.Spares(part, blow), __instance, hit, false);
        }
    }

    /// <summary>
    /// A kraken dying, on its owner, before the game makes its corpse and drops its loot: moved to where its head is seen
    /// (<see cref="KrakenDeath"/>), so the corpse comes up there, and its loot aimed at the held ship's deck. First of all
    /// the mods' parts of a death, so they see it where it is seen; from then until after the last of them (Elite
    /// Crafting drops its runes and gear in its own finalizer) what they drop goes to the deck too (<see cref="KrakenDropSpot"/>).
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
    public static class KrakenDeathPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Character __instance)
        {
            KrakenBrain? brain = KrakenBrain.Loaded.Count > 0 ? KrakenAIPatch.Find(__instance) : null;
            if (brain != null && __instance.m_nview != null && __instance.m_nview.IsOwner())
            {
                SafeCall.Run("Character.OnDeath kraken", static dying =>
                {
                    KrakenDeath.Prepare(dying);
                    KrakenDropSpot.Open(KrakenDeath.LootSpot(dying));
                }, brain);
            }
        }

        [HarmonyPriority(Priority.Last)]
        private static void Finalizer() => KrakenDropSpot.Close();
    }

    /// <summary>An item dropped while a kraken dies goes where its loot does (<see cref="KrakenDropSpot"/>); any other passes untouched.</summary>
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.DropItem))]
    public static class KrakenDropSpotPatch
    {
        private static void Prefix(ref Vector3 position) => KrakenDropSpot.Redirect(ref position);
    }

    /// <summary>A kraken's loot lands on the deck of the ship it held (or floats up where it died), not on the sea floor.</summary>
    [HarmonyPatch(typeof(CharacterDrop), "OnDeath")]
    public static class KrakenLootPatch
    {
        private static void Prefix(CharacterDrop __instance)
        {
            KrakenBrain? brain = KrakenBrain.Loaded.Count > 0 ? KrakenAIPatch.Find(__instance) : null;
            if (brain != null)
            {
                SafeCall.Run("CharacterDrop.OnDeath kraken", static (dying, drop) => KrakenDeath.AimLoot(dying, drop), brain, __instance);
            }
        }
    }

    /// <summary>Each zone's spawn system takes the kraken's spawn entry as it wakes (<see cref="KrakenSpawns"/>).</summary>
    [HarmonyPatch(typeof(SpawnSystem), "Awake")]
    public static class KrakenSpawnPatch
    {
        private static void Postfix(SpawnSystem __instance) =>
            SafeCall.Run("SpawnSystem.Awake kraken", static system => KrakenSpawns.Join(system), __instance);
    }

    /// <summary>
    /// A kraken about to spawn from the wild spawn system: only near a ship with a crew, and at the surface rather than on
    /// the sea floor where the game would put it (<see cref="KrakenSpawns.Allow"/>). Every other spawn passes untouched.
    /// </summary>
    [HarmonyPatch(typeof(SpawnSystem), "Spawn")]
    public static class KrakenSpawnGatePatch
    {
        private static bool Prefix(SpawnSystem.SpawnData critter, ref Vector3 spawnPoint)
        {
            if (critter?.m_prefab == null || critter.m_prefab != KrakenPrefabs.Prefab || SpawnSystem.m_nospawn)
            {
                return true;
            }
            (bool allowed, Vector3 point) = SafeCall.Run("SpawnSystem.Spawn kraken",
                static at => (KrakenSpawns.Allow(ref at), at), spawnPoint, (false, spawnPoint));
            spawnPoint = point;
            return allowed;
        }
    }
}
