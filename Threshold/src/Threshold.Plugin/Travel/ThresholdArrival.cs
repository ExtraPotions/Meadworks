// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: arrival protection after a portal trip (7 - Jumps / Arrival Protection Seconds). The no-damage part
// reuses the sea gate JumpProtection; the time stamp uses the sea gate clock (SeaGateFields.Now, server time).
using System;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Runtime.SeaGates;
using HarmonyLib;

namespace ExtraPotions.Threshold.Runtime.Travel
{
    /// <summary>
    /// After the local player lands from a portal trip (routed or the game's own), for Arrival Protection Seconds: no
    /// damage of any kind (<see cref="JumpProtection"/>, which stops every hit on the owner's client) and creatures drop
    /// the player as their target. Creature AI runs on whichever machine owns the creature, so the end time is written to
    /// the player's own ZDO and every Threshold peer reads it in <c>MonsterAI.UpdateTarget</c>. A peer without Threshold
    /// (a vanilla client owning nearby creatures) still lets them target the player; damage stays blocked either way.
    /// </summary>
    public static class ThresholdArrival
    {
        public const string SafeKeyName = "threshold.arrival.safe";

        /// <summary>Player ZDO key: long server ticks, creatures leave this player alone until then.</summary>
        public static readonly int SafeKey = SafeKeyName.GetStableHashCode();

        static readonly AccessTools.FieldRef<Character, ZNetView> View = AccessTools.FieldRefAccess<Character, ZNetView>("m_nview");
        static readonly AccessTools.FieldRef<MonsterAI, Character> TargetCreature = AccessTools.FieldRefAccess<MonsterAI, Character>("m_targetCreature");

        static bool pending;

        /// <summary>The local player is on a portal trip whose landing starts the protection.</summary>
        public static bool Pending => pending;

        public static void Patch(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(TeleportWorld), nameof(TeleportWorld.Teleport), new[] { typeof(Player) }),
                prefix: new HarmonyMethod(typeof(ThresholdArrival), nameof(BeforePortal)),
                postfix: new HarmonyMethod(typeof(ThresholdArrival), nameof(AfterPortal)));
            harmony.Patch(AccessTools.Method(typeof(Player), "UpdateTeleport", new[] { typeof(float) }),
                prefix: new HarmonyMethod(typeof(ThresholdArrival), nameof(BeforeStep)),
                postfix: new HarmonyMethod(typeof(ThresholdArrival), nameof(AfterStep)));
            harmony.Patch(AccessTools.Method(typeof(MonsterAI), "UpdateTarget",
                    new[] { typeof(Humanoid), typeof(float), typeof(bool).MakeByRefType(), typeof(bool).MakeByRefType() }),
                postfix: new HarmonyMethod(typeof(ThresholdArrival), nameof(AfterUpdateTarget)));
        }

        /// <summary>A portal trip of the local player started (after <c>Player.TeleportTo</c> answered true).</summary>
        public static void Begin(Player player)
        {
            if (player != null && player == Player.m_localPlayer) pending = true;
        }

        static void BeforePortal(Player player, out bool __state) =>
            __state = player != null && player == Player.m_localPlayer && !player.IsTeleporting();

        static void AfterPortal(Player player, bool __state)
        {
            if (__state && player != null && player.IsTeleporting()) Begin(player);
        }

        static void BeforeStep(Player __instance, out bool __state)
        {
            __state = false;
            if (!pending || __instance == null || __instance != Player.m_localPlayer) return;
            if (__instance.IsTeleporting()) __state = true;
            else Reset(); // the trip ended some other way (death, logout)
        }

        static void AfterStep(Player __instance, bool __state)
        {
            if (!__state || __instance == null || __instance.IsTeleporting()) return;
            pending = false;
            try { Arrived(__instance); }
            catch (Exception e) { Plugin.Log?.Warning("Threshold: arrival protection failed: " + e.GetType().Name + ": " + e.Message); }
        }

        static void Arrived(Player player)
        {
            var settings = Plugin.Settings;
            if (settings == null || !ThresholdModules.Active()) return;
            float seconds = settings.Get(ThresholdSettings.Jumps.ArrivalProtectionSeconds);
            if (seconds <= 0f) return;
            JumpProtection.Start(seconds);
            var view = View(player);
            if (view != null && view.IsValid() && view.IsOwner()) view.GetZDO().Set(SafeKey, SeaGateFields.After(seconds));
            Plugin.Log?.Verbose("Threshold: arrival protection for " + seconds.ToString("0.#") + " s");
        }

        /// <summary>Whether creatures leave this player alone now (read on the creature owner's machine).</summary>
        public static bool Protected(Character? character)
        {
            if (!(character is Player)) return false;
            var view = View(character);
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            return zdo != null && zdo.GetLong(SafeKey, 0L) > SeaGateFields.Now;
        }

        static void AfterUpdateTarget(MonsterAI __instance)
        {
            if (__instance == null) return;
            ref var target = ref TargetCreature(__instance);
            if (target != null && Protected(target)) target = null!;
        }

        public static void Reset() => pending = false;
    }
}
