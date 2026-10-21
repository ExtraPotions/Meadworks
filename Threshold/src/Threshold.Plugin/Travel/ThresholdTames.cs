// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Bring Tames (T18). Idea seen in OdinPlus TeleportEverything (no license found): clean-room, from its
// one-line feature description only. Vanilla members read with ILSpy (not copied): MonsterAI.GetFollowTarget, the
// follow command saved as the player's name in the creature's ZDO (Tameable, ZDOVars.s_follow), persistent ZDOs kept by
// ZNetScene.RemoveObjects when an instance unloads, ZDO.SetOwner/SetPosition, Player.UpdateTeleport (a distant jump,
// which every Threshold trip is, never sends the player back: with no floor under the exit it lifts the player onto
// ZoneSystem.GetSolidHeight at the exit), ZoneSystem.FindFloor (ray down from 1 m above the point).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Portals;
using ExtraPotions.Threshold.Routing;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.Travel
{
    /// <summary>
    /// Followers of the local player within Tame Radius are chosen when a Threshold trip starts. When the jump has landed
    /// (the player is no longer teleporting and stands over the exit, at the height the game finally put them) each chosen
    /// creature is placed in front of the player's landing on the floor found under its spot (<see cref="TameRules.Settle"/>)
    /// and moved by its ZDO: this client takes ownership and writes the new position, so an unloaded creature is rebuilt at
    /// the destination by the game's own object streaming, and one still loaded (a short jump) is moved with its rigidbody
    /// stopped. Nothing moves if the player ended elsewhere (the game never sends a distant jump back; this only guards
    /// against something else moving the player), died or two minutes passed. The follow command stays the game's.
    /// </summary>
    public static class ThresholdTames
    {
        const float GiveUpSeconds = 120f;

        static readonly List<ZDOID> pending = new List<ZDOID>();
        static MapPoint exit;
        static float yaw;
        static float startedAt;

        /// <summary>Bring Tames in effect now (server value, TeleportEverything absent, Threshold routing).</summary>
        public static bool Enabled(ThresholdConfig settings) =>
            settings != null && ThresholdModules.ExtrasActive() && settings.Get(ThresholdSettings.Tames.BringTames);

        /// <summary>The followers to bring, chosen before <c>Player.TeleportTo</c>.</summary>
        public static List<ZDOID> Choose(Player player, ThresholdConfig settings)
        {
            var chosen = new List<ZDOID>();
            if (player == null || !Enabled(settings)) return chosen;
            var views = new List<ZNetView>();
            var candidates = new List<TameCandidate>();
            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || character == player || character.IsPlayer()) continue;
                var view = character.GetComponent<ZNetView>();
                if (view == null || !view.IsValid()) continue;
                var ai = character.GetComponent<MonsterAI>();
                bool follows = ai != null && ai.GetFollowTarget() == player.gameObject;
                if (!follows) continue;
                var at = character.transform.position;
                candidates.Add(new TameCandidate(views.Count, new MapPoint(at.x, at.y, at.z), character.IsTamed(), true, character.IsDead()));
                views.Add(view);
            }
            var here = player.transform.position;
            var picked = TameRules.Select(candidates, new MapPoint(here.x, here.y, here.z),
                settings.Get(ThresholdSettings.Tames.TameRadius), settings.Get(ThresholdSettings.Tames.MaxTames));
            foreach (int index in picked) chosen.Add(views[index].GetZDO().m_uid);
            Plugin.Log?.Debug("Threshold tames: " + candidates.Count + " following, " + chosen.Count + " chosen within "
                + settings.Get(ThresholdSettings.Tames.TameRadius).ToString("0.#") + " m"
                + (chosen.Count > 0 ? ": " + string.Join(", ", picked.Select(i => Utils.GetPrefabName(views[i].gameObject) + " " + views[i].GetZDO().m_uid)) : ""));
            return chosen;
        }

        /// <summary>The game accepted the teleport: remember the followers and the exit they land around.</summary>
        public static void Begin(List<ZDOID> tames, MapPoint exitPoint, float exitYaw)
        {
            pending.Clear();
            if (tames == null || tames.Count == 0) return;
            pending.AddRange(tames);
            exit = exitPoint;
            yaw = exitYaw;
            startedAt = Time.time;
        }

        /// <summary>Each frame on a client: moves the followers once the local player's jump has landed.</summary>
        public static void Tick()
        {
            ThresholdCarts.Tick();
            if (pending.Count == 0) return;
            var player = Player.m_localPlayer;
            if (player == null || player.IsDead() || Time.time - startedAt > GiveUpSeconds)
            {
                Plugin.Log?.Debug("Threshold tames: " + pending.Count + " left behind (player gone, dead or the jump timed out).");
                pending.Clear();
                return;
            }
            if (player.IsTeleporting()) return;
            var at = player.transform.position;
            if (!TameRules.Landed(new MapPoint(at.x, at.y, at.z), exit))
            {
                Plugin.Log?.Debug("Threshold tames: the player is not over the exit after the jump; " + pending.Count + " stay where they are.");
                pending.Clear();
                return;
            }
            try { Move(new MapPoint(at.x, at.y, at.z)); }
            catch (Exception e) { Plugin.Log?.Warning("Threshold tames: move failed: " + e.GetType().Name + ": " + e.Message); }
            pending.Clear();
        }

        static void Move(MapPoint landed)
        {
            var zdos = ZDOMan.instance;
            var scene = ZNetScene.instance;
            if (zdos == null || scene == null) return;
            long self = ZDOMan.GetSessionID();
            int moved = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                var zdo = zdos.GetZDO(pending[i]);
                if (zdo == null || !zdo.IsValid())
                {
                    Plugin.Log?.Debug("Threshold tames: " + pending[i] + " no longer exists.");
                    continue;
                }
                var spot = TameRules.Place(landed, yaw, i, pending.Count);
                float? floor = ZoneSystem.instance != null
                    && ZoneSystem.instance.FindFloor(new Vector3(spot.X, TameRules.ProbeHeight(landed), spot.Z), out var height) ? height : (float?)null;
                var settled = TameRules.Settle(spot, landed, floor);
                var to = new Vector3(settled.X, settled.Y, settled.Z);
                zdo.SetOwner(self);
                zdo.SetPosition(to);
                var view = scene.FindInstance(zdo);
                if (view != null)
                {
                    view.transform.position = to;
                    var body = view.GetComponent<Rigidbody>();
                    if (body != null)
                    {
                        body.position = to;
                        body.linearVelocity = Vector3.zero;
                    }
                }
                moved++;
                Plugin.Log?.Debug("Threshold tames: " + zdo.m_uid + " moved to " + to.ToString("F1") + (view != null ? " (loaded)" : " (rebuilt by streaming)")
                    + (floor.HasValue && Mathf.Approximately(settled.Y, floor.Value) ? ", on the floor" : ", at the player (no floor at its spot)"));
            }
            Plugin.Log?.Debug("Threshold tames: " + moved + " of " + pending.Count + " brought.");
        }

        public static void Reset() => pending.Clear();
    }
}
