// Keel - ExtraPotions Meadworks ships and sailing.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Portions Copyright (C) 2026 Gerald Glasgow, GrindstoneSkills 0.15.0, GPL-3.0-or-later.
// Adapted from ValheimMods at 14d151ed8a16f7264a21efd35e19f8ac346276c1:
// GrindstoneSkills/GrindstoneSkills/Sailing/WindCall/WindCallInput.cs, WindCallReceive.cs, WindCallState.cs,
// WindCallWind.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: one Keel class; the key through Crucible C07 (MeadworksInput) plus Keel's gamepad chord, and
// the settings, levels (CS1), unlock, refusal, cooldown and compass from Keel.Core (AbilityRules, AbilityCooldown,
// WindCallState); Keel's RPC and ZDO keys (keel_WindCall, keel.wind_dir, keel.wind_until); the announcements are Keel's
// C05 texts chosen by WindCallState.Announce; PatchGuard replaced by try/catch with a log line; everything acts only
// while Keel's Sailing module is active.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Localization;
using ExtraPotions.Keel.Sailing;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// Wind Call (design B6). The local player's key while steering sends the call on that ship to everybody who has the
    /// ship loaded, with the flat direction the camera looks and the caller's ID; the cooldown is the caller's own, for
    /// the session. The ship's owner stores direction and end time in the ship's ZDO, every client aboard is told who
    /// turned the wind, and while it blows the game's target wind is that direction on every client aboard that ship (the
    /// owner's sail force and everyone's visuals agree). The debug wind and the world-edge wind stay the game's.
    /// </summary>
    internal static class KeelWindCall
    {
        private static readonly int DirectionHash = SailingKeys.WindCallDirection.GetStableHashCode();
        private static readonly int UntilHash = SailingKeys.WindCallUntil.GetStableHashCode();

        public static readonly AbilityCooldown Cooldown = new AbilityCooldown();

        public static bool Unlockable => SailingPerks.Active && AbilityRules.Unlockable(true, Level);

        public static float Level => Plugin.Settings!.Get(KeelSettings.WindCall.Level);

        public static void Pressed(Player player)
        {
            var ship = player.GetControlledShip();
            if (!Unlockable || ship == null || ship.m_nview == null || !ship.m_nview.IsValid()) return;
            var settings = Plugin.Settings!;
            var name = KeelText.Text(KeelTextKeys.AbilityWindCall);
            switch (AbilityRules.Refuse(KeelSailing.Skill!.Local(), Level, Cooldown, Time.time, out var seconds))
            {
                case AbilityRefusal.NeedsLevel:
                    player.Message(MessageHud.MessageType.Center, KeelText.Format(KeelTextKeys.AbilityNeedsLevel, name, Mathf.CeilToInt(Level)));
                    return;
                case AbilityRefusal.CoolingDown:
                    player.Message(MessageHud.MessageType.Center, KeelText.Format(KeelTextKeys.AbilityCooldown, name, seconds));
                    return;
            }
            Cooldown.Start(Time.time, settings.Get(KeelSettings.WindCall.Cooldown));
            ship.m_nview.InvokeRPC(ZNetView.Everybody, SailingKeys.RpcWindCall, LookDirection(player), player.GetPlayerID());
        }

        /// <summary>The way the camera looks, flat: its yaw alone.</summary>
        private static Vector3 LookDirection(Player player)
        {
            var camera = Utils.GetMainCamera();
            var yaw = camera != null ? camera.transform.eulerAngles.y : player.transform.eulerAngles.y;
            return Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        }

        internal static void Receive(Ship ship, Vector3 direction, long caller)
        {
            direction.y = 0f;
            if (ship == null || ship.m_nview == null || !ship.m_nview.IsValid() || !SailingPerks.Active || direction.sqrMagnitude < 0.01f) return;
            direction.Normalize();
            var seconds = Plugin.Settings!.Get(KeelSettings.WindCall.Duration);
            if (ship.m_nview.IsOwner() && ZNet.instance != null)
            {
                var zdo = ship.m_nview.GetZDO();
                zdo.Set(DirectionHash, direction);
                zdo.Set(UntilHash, WindCallState.Until(ZNet.instance.GetTime().Ticks, seconds));
            }
            var player = Player.m_localPlayer;
            if (player == null || Ship.GetLocalShip() != ship) return;
            var point = KeelText.Text(KeelTextKeys.Compass(WindCallState.Compass(direction.x, direction.z)));
            var whole = Mathf.RoundToInt(seconds);
            var who = ship.m_players.Find(p => p != null && p.GetPlayerID() == caller);
            var announcement = WindCallState.Announce(caller, player.GetPlayerID(), who != null);
            var key = WindCallState.KeyOf(announcement);
            var text = announcement == WindAnnouncement.ByName ? KeelText.Format(key, who!.GetPlayerName(), point, whole) : KeelText.Format(key, point, whole);
            player.Message(MessageHud.MessageType.Center, text);
        }

        /// <summary>The direction the local player's ship's called wind blows toward, when one blows now.</summary>
        internal static bool Called(EnvMan env, out Vector3 direction)
        {
            direction = Vector3.zero;
            var player = Player.m_localPlayer;
            if (player == null || !SailingPerks.Active || ZNet.instance == null) return false;
            if (Utils.LengthXZ(player.transform.position) > 10500f - env.m_edgeOfWorldWidth) return false;
            var ship = Ship.GetLocalShip();
            var zdo = ship != null && ship.m_nview != null && ship.m_nview.IsValid() ? ship.m_nview.GetZDO() : null;
            if (zdo == null) return false;
            direction = zdo.GetVec3(DirectionHash, Vector3.zero);
            return WindCallState.Blows(zdo.GetLong(UntilHash), ZNet.instance.GetTime().Ticks, direction.x, direction.z);
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.Awake))]
        private static class Register
        {
            [HarmonyPostfix]
            private static void Postfix(Ship __instance)
            {
                var nview = __instance.m_nview;
                if (nview == null || nview.GetZDO() == null) return;
                nview.Register<Vector3, long>(SailingKeys.RpcWindCall, (sender, direction, caller) =>
                {
                    try { Receive(__instance, direction, caller); }
                    catch (Exception error) { KeelShips.Warn("wind call", error); }
                });
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SetTargetWind))]
        private static class Target
        {
            [HarmonyPrefix]
            private static void Prefix(EnvMan __instance, ref Vector3 dir)
            {
                try
                {
                    if (__instance.m_debugWind || !Called(__instance, out var called)) return;
                    dir = called;
                    // A native change already under way ignores this target until it ends; turn that change instead.
                    var target = __instance.m_windDir2;
                    if (WindCallState.Retarget(__instance.m_windTransitionTimer >= 0f, target.x, target.z, called.x, called.z))
                        __instance.m_windDir2 = new Vector4(called.x, called.y, called.z, target.w);
                }
                catch (Exception error) { KeelShips.Warn("called wind", error); }
            }
        }
    }
}
