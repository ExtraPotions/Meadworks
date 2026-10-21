// Keel - ExtraPotions Meadworks ships and sailing.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Portions Copyright (C) 2026 Gerald Glasgow, GrindstoneSkills 0.15.0, GPL-3.0-or-later.
// Adapted from ValheimMods at 14d151ed8a16f7264a21efd35e19f8ac346276c1:
// GrindstoneSkills/GrindstoneSkills/Sailing/Lookout/LookoutInput.cs, LookoutPulse.cs, LookoutReveal.cs, LookoutRing.cs;
// GrindstoneSkills/GrindstoneSkills/Core/WishbonePing.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: one Keel class; the key through Crucible C07 plus Keel's gamepad chord; settings, levels
// (CS1), unlock, refusal, cooldown, target test and ring radius from Keel.Core (AbilityRules, AbilityCooldown,
// LookoutRules); Keel's RPC (keel_Lookout); the report is Keel's C05 text; pressed away from a ship the key does nothing
// (no message); PatchGuard replaced by try/catch; acts only while Keel's Sailing module is active and draws nothing on
// a headless process.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Localization;
using ExtraPotions.Keel.Sailing;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// Lookout (design B7). The local player's key aboard a ship sends a pulse on that ship to everybody who has it loaded;
    /// every client with a player draws the ring and plays the Wishbone's near ping at the ship; the players aboard that
    /// ship see the name tags of the enemies it reached (bosses left out) for the duration, at any distance, and a count.
    /// The cooldown is the sender's own, for the session.
    /// </summary>
    internal static class KeelLookout
    {
        public static readonly AbilityCooldown Cooldown = new AbilityCooldown();

        private static readonly Dictionary<Character, float> revealed = new Dictionary<Character, float>();
        private static float latestUntil;
        private static EffectList? ping;
        private static ObjectDB? pingFrom;

        public static bool Unlockable => SailingPerks.Active && AbilityRules.Unlockable(true, Level);

        public static float Level => Plugin.Settings!.Get(KeelSettings.Lookout.Level);

        public static void Pressed(Player player)
        {
            if (!Unlockable) return;
            var ship = Ship.GetLocalShip();
            if (ship == null || ship.m_nview == null || !ship.m_nview.IsValid()) return;
            var name = KeelText.Text(KeelTextKeys.AbilityLookout);
            switch (AbilityRules.Refuse(KeelSailing.Skill!.Local(), Level, Cooldown, Time.time, out var seconds))
            {
                case AbilityRefusal.NeedsLevel:
                    player.Message(MessageHud.MessageType.Center, KeelText.Format(KeelTextKeys.AbilityNeedsLevel, name, Mathf.CeilToInt(Level)));
                    return;
                case AbilityRefusal.CoolingDown:
                    player.Message(MessageHud.MessageType.Center, KeelText.Format(KeelTextKeys.AbilityCooldown, name, seconds));
                    return;
            }
            Cooldown.Start(Time.time, Plugin.Settings!.Get(KeelSettings.Lookout.Cooldown));
            ship.m_nview.InvokeRPC(ZNetView.Everybody, SailingKeys.RpcLookout);
        }

        internal static void Receive(Ship ship)
        {
            var player = Player.m_localPlayer;
            if (ship == null || player == null || !SailingPerks.Active || !Plugin.AllowsUi) return;
            var settings = Plugin.Settings!;
            var center = ship.transform.position;
            var radius = settings.Get(KeelSettings.Lookout.Radius);
            LookoutRing.Draw(center, radius);
            Ping(center);
            if (Ship.GetLocalShip() != ship) return;
            var found = Reveal(player, center, radius, settings.Get(KeelSettings.Lookout.Duration));
            player.Message(MessageHud.MessageType.TopLeft, KeelText.Format(KeelTextKeys.LookoutSent, found));
        }

        private static int Reveal(Player player, Vector3 center, float radius, float seconds)
        {
            Prune();
            var until = Time.time + Mathf.Max(0f, seconds);
            latestUntil = Mathf.Max(latestUntil, until);
            var count = 0;
            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null) continue;
                var offset = character.transform.position - center;
                if (!LookoutRules.IsTarget(character.IsPlayer(), character.IsDead(), character.IsBoss(), character.m_hideHud,
                        BaseAI.IsEnemy(player, character), offset.x, offset.z, radius))
                    continue;
                revealed[character] = Mathf.Max(until, revealed.TryGetValue(character, out var earlier) ? earlier : 0f);
                count++;
            }
            return count;
        }

        private static bool IsRevealed(Character character) =>
            character != null && revealed.TryGetValue(character, out var until) && Time.time < until;

        private static void Prune()
        {
            var ended = new List<Character>();
            foreach (var entry in revealed)
                if (entry.Key == null || Time.time >= entry.Value) ended.Add(entry.Key);
            foreach (var character in ended) revealed.Remove(character);
        }

        public static void Reset()
        {
            revealed.Clear();
            latestUntil = 0f;
            ping = null;
            pingFrom = null;
        }

        /// <summary>The Wishbone's near ping at a spot, as a local copy with network views disabled; silent without a Wishbone.</summary>
        private static void Ping(Vector3 position)
        {
            if (ObjectDB.instance != null && ObjectDB.instance != pingFrom)
            {
                pingFrom = ObjectDB.instance;
                var wishbone = ObjectDB.instance.GetItemPrefab("Wishbone");
                var item = wishbone != null ? wishbone.GetComponent<ItemDrop>() : null;
                ping = item != null && item.m_itemData.m_shared.m_equipStatusEffect is SE_Finder finder ? finder.m_pingEffectNear : null;
            }
            if (ping?.m_effectPrefabs == null) return;
            foreach (var data in ping.m_effectPrefabs)
            {
                if (data == null || !data.m_enabled || data.m_prefab == null) continue;
                var was = ZNetView.m_forceDisableInit;
                ZNetView.m_forceDisableInit = true;
                try { UnityEngine.Object.Destroy(UnityEngine.Object.Instantiate(data.m_prefab, position, Quaternion.identity), 8f); }
                finally { ZNetView.m_forceDisableInit = was; }
            }
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.Awake))]
        private static class Register
        {
            [HarmonyPostfix]
            private static void Postfix(Ship __instance)
            {
                var nview = __instance.m_nview;
                if (nview == null || nview.GetZDO() == null) return;
                nview.Register(SailingKeys.RpcLookout, sender =>
                {
                    try { Receive(__instance); }
                    catch (Exception error) { KeelShips.Warn("lookout pulse", error); }
                });
            }
        }

        /// <summary>A revealed creature's tag shows at any distance (the game shows tags within 10 m).</summary>
        [HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.TestShow))]
        private static class Show
        {
            [HarmonyPostfix]
            private static void Postfix(Character c, ref bool __result)
            {
                if (!__result && revealed.Count > 0 && IsRevealed(c)) __result = true;
            }
        }

        /// <summary>While revealed, the hud's hover timer is held at 0; after the last reveal the list is emptied.</summary>
        [HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.UpdateHuds))]
        private static class KeepShown
        {
            [HarmonyPrefix]
            private static void Prefix(EnemyHud __instance)
            {
                if (revealed.Count == 0) return;
                if (Time.time >= latestUntil)
                {
                    revealed.Clear();
                    return;
                }
                foreach (var hud in __instance.m_huds)
                    if (IsRevealed(hud.Key)) hud.Value.m_hoverTimer = 0f;
            }
        }
    }

    /// <summary>A pale ring laid on the sea at the pulse's start, racing out to the radius in two seconds and fading.</summary>
    internal sealed class LookoutRing : MonoBehaviour
    {
        private const int Segments = 96;
        private const float Width = 0.5f;
        private const float AboveSea = 0.6f;
        private static readonly Color Colour = new Color(0.62f, 0.88f, 1f, 0.95f);
        private static Material? material;

        private LineRenderer? line;
        private Vector3 center;
        private float radius;
        private float born;

        public static void Draw(Vector3 shipPosition, float radius)
        {
            var lineMaterial = LineMaterial();
            if (lineMaterial == null) return;
            var holder = new GameObject("keel_lookout_ring");
            var ring = holder.AddComponent<LookoutRing>();
            var sea = ZoneSystem.instance != null ? Mathf.Max(ZoneSystem.instance.m_waterLevel, shipPosition.y - 1f) : shipPosition.y;
            ring.center = new Vector3(shipPosition.x, sea + AboveSea, shipPosition.z);
            ring.radius = radius;
            ring.born = Time.time;
            var l = holder.AddComponent<LineRenderer>();
            l.sharedMaterial = lineMaterial;
            l.useWorldSpace = true;
            l.loop = true;
            l.positionCount = Segments;
            l.shadowCastingMode = ShadowCastingMode.Off;
            l.receiveShadows = false;
            ring.line = l;
            ring.Spread(0f);
        }

        private void Update()
        {
            try
            {
                var t = (Time.time - born) / LookoutRules.RingSeconds;
                if (t >= 1f) Destroy(gameObject);
                else Spread(t);
            }
            catch (Exception)
            {
                Destroy(gameObject);
            }
        }

        private void Spread(float t)
        {
            var current = LookoutRules.RingRadius(t, radius);
            var eased = 1f - (1f - t) * (1f - t);
            for (var i = 0; i < Segments; i++)
            {
                var angle = Mathf.PI * 2f * i / Segments;
                line!.SetPosition(i, center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * current);
            }
            var colour = Colour;
            colour.a *= 1f - t * t;
            line!.startColor = colour;
            line.endColor = colour;
            line.widthMultiplier = Width * (1f + 2f * eased);
        }

        private static Material? LineMaterial()
        {
            if (material != null) return material;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            return material = shader != null ? new Material(shader) : null;
        }
    }
}
