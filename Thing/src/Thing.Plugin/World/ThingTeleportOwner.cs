// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-owned reuse: Wayfarer's landing/capsule checks and bounded departure recovery; existing kit consent window.
using System;
using System.Collections.Generic;
using System.Globalization;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Thing.Integration;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingTeleportOwner
    {
        static KitWindow? prompt;
        static string token = "", character = "";
        static long generation = -1;
        static float promptExpires, moveExpires;
        static ZNetPeer? server;
        static Player? actor;
        static Vector3 destination, departure;
        static bool moving, recovering;
        static readonly System.Reflection.FieldInfo cooldown = AccessTools.Field(typeof(Player), "m_teleportCooldown");
        static bool Active => Plugin.Ready && Plugin.AllowsUi && CruciblePlugin.IsReady && CruciblePlugin.Session.AuthorityReady && ThingModules.Active(ThingModule.WorldTools);
        static bool SameSession => Active && generation == CruciblePlugin.Session.Generation
            && (CruciblePlugin.Session.IsAuthority || ReferenceEquals(server, ZNet.instance?.GetServerPeer()) && server?.m_rpc?.IsConnected() == true);
        static bool Local(string expected, out Player? player)
        {
            player = Player.m_localPlayer;
            if (!player || player.IsDead()) return false;
            var view = player.GetComponent<ZNetView>();
            return view && view.IsValid() && view.IsOwner() && view.GetZDO()?.m_uid.ToString() == expected;
        }
        internal static OperationExecution<IReadOnlyList<string>> Apply(PeerCommandContext context, IReadOnlyList<string> request)
        {
            if (!Active || (!context.IsLocal && !context.ServerConnectionVerified) || context.Session.Generation != CruciblePlugin.Session.Generation || request.Count < 2)
                return Reply("unavailable");
            if (request[0] == "cancel" && request.Count == 2)
            { if (SameSession && token == request[1]) ClosePrompt(false); return Reply("canceled"); }
            if (request[0] == "prompt" && request.Count == 5)
            {
                if (prompt != null || moving || recovering || request[1].Length != 32 || (request[2] != "goto" && request[2] != "bring") || request[3].Length > 200
                    || !Local(request[4], out _)) return Reply("unavailable");
                var window = KitWindow.Create("Thing_TeleportConsent", new Vector2(620, 270));
                if (window == null) return Reply("unavailable");
                prompt = window; generation = context.Session.Generation; server = ZNet.instance?.GetServerPeer();
                token = request[1]; character = request[4]; promptExpires = Time.realtimeSinceStartup + 30;
                var heading = KitAlmanacDialog.Label(window.Panel, "Title", ThingText.Text("teleport_prompt_title"), new Vector2(20, -16), new Vector2(580, 35), 22);
                if (KitCanvas.Heading != null) heading.font = KitCanvas.Heading; KitCanvas.Fit(heading);
                KitAlmanacDialog.Label(window.Panel, "Request", ThingText.Format(request[2] == "goto" ? "teleport_prompt_goto" : "teleport_prompt_bring", request[3]), new Vector2(20, -65), new Vector2(580, 100), 17).richText = false;
                var yes = KitButton.Create(window.Panel, "Accept", ThingText.Text("teleport_accept"), () => ClosePrompt(true), new Vector2(20, -210), new Vector2(270, 40));
                var no = KitButton.Create(window.Panel, "Decline", ThingText.Text("teleport_decline"), () => ClosePrompt(false, true), new Vector2(330, -210), new Vector2(270, 40));
                window.Focusable(yes, 0, 0); window.Focusable(no, 1, 0); window.Closed += Closed; window.Open();
                return Reply("shown");
            }
            if (request[0] != "move" || request.Count != 5 || moving || recovering || !Local(request[1], out var player) || player!.IsTeleporting() || !Cooldown(player)
                || !float.TryParse(request[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(request[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                || !float.TryParse(request[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var z)) return Reply("unavailable");
            var target = new Vector3(x, y, z);
            if (!ThingTeleportCommands.Valid(target) || !ThingTeleportCommands.Valid(player.transform.position)) return Reply("unavailable");
            ClosePrompt(false); generation = context.Session.Generation; server = ZNet.instance?.GetServerPeer(); character = request[1]; actor = player;
            departure = player.transform.position; destination = target;
            context.MarkDecisionMade();
            if (!player.TeleportTo(target, player.transform.rotation, true)) { actor = null; return Reply("unavailable"); }
            moving = true; moveExpires = Time.realtimeSinceStartup + 30;
            return Reply("accepted");
        }
        static OperationExecution<IReadOnlyList<string>> Reply(string value) => OperationExecution<IReadOnlyList<string>>.Success(new[] { value });
        static bool Cooldown(Player player) => (float)(cooldown?.GetValue(player) ?? 0f) >= 2;
        static void Closed() => ClosePrompt(false, true);
        static void ClosePrompt(bool accepted, bool decline = false)
        {
            var old = prompt; var answer = token; var send = old != null && SameSession && Local(character, out _) && Time.realtimeSinceStartup < promptExpires;
            prompt = null; token = "";
            if (old != null) { old.Closed -= Closed; old.Close(); old.Destroy(); }
            if (send && (accepted || decline)) ThingTeleportCommands.Submit(new[] { "respond", answer, accepted ? "yes" : "no" });
        }
        internal static void Tick()
        {
            if (!SameSession) { Reset(); return; }
            if (prompt != null && (Time.realtimeSinceStartup >= promptExpires || !Local(character, out _))) ClosePrompt(false);
            if (!moving && !recovering) return;
            if (!Local(character, out var player) || !ReferenceEquals(player, actor)) { moving = recovering = false; actor = null; return; }
            if (Time.realtimeSinceStartup >= moveExpires) { moving = recovering = false; actor = null; Report("teleport_unknown"); return; }
            if (player!.IsTeleporting()) return;
            if (recovering)
            {
                if (!Cooldown(player)) return;
                recovering = false; actor = null;
                // Exactly one attempt to return to the captured departure; an uncertain result is never retried.
                player.TeleportTo(departure, player.transform.rotation, true); Report("teleport_unsafe"); return;
            }
            if (Vector2.Distance(new Vector2(destination.x, destination.z), new Vector2(player.transform.position.x, player.transform.position.z)) > 2)
            { Recover(); return; }
            if (!ZNetScene.instance || !ZNetScene.instance.IsAreaReady(destination)) return;
            var target = destination; var mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            if ((!Physics.Raycast(target + Vector3.up * 2, Vector3.down, out var hit, 2000, mask, QueryTriggerInteraction.Ignore)
                && !Physics.Raycast(target + Vector3.up * 1000, Vector3.down, out hit, 2000, mask, QueryTriggerInteraction.Ignore))) { Recover(); return; }
            target.y = Mathf.Max(hit.point.y, ZoneSystem.c_WaterLevel) + .6f;
            if (!Clear(target, hit.normal.y) && !TryResolve(target, out target)) { Recover(); return; }
            moving = false; actor = null; player.transform.position = target;
            var body = player.GetComponent<Rigidbody>(); if (body) { body.position = target; body.linearVelocity = Vector3.zero; }
            AccessTools.Field(typeof(Character), "m_maxAirAltitude")?.SetValue(player, target.y);
            Report("teleport_arrived");
        }
        static void Recover() { moving = false; recovering = true; moveExpires = Time.realtimeSinceStartup + 30; Report("teleport_recovering"); }
        static bool Clear(Vector3 position, float normalY)
        {
            var player = Player.m_localPlayer; var collider = player ? player.GetComponent<CapsuleCollider>() : null;
            var scale = player ? player.transform.lossyScale : Vector3.one;
            var radius = collider ? collider.radius * Mathf.Max(scale.x, scale.z) : .4f;
            var height = collider ? Mathf.Max(collider.height * scale.y, radius * 2) : 1.8f;
            var center = position + (collider ? Vector3.Scale(collider.center, scale) : Vector3.up * .9f);
            var offset = Vector3.up * (height * .5f - radius);
            var mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "character", "character_net", "character_ghost");
            foreach (var hit in Physics.OverlapCapsule(center - offset, center + offset, radius, mask, QueryTriggerInteraction.Ignore))
                if (!player || !hit.transform.IsChildOf(player.transform)) return false;
            return ZNetScene.instance && ZNetScene.instance.IsAreaReady(position) && (normalY >= .65f || position.y <= ZoneSystem.c_WaterLevel + .61f);
        }
        static bool TryResolve(Vector3 target, out Vector3 landing)
        {
            landing = target;
            var solid = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            foreach (var radius in new[] { 5f, 8f }) for (var i = 0; i < 8; i++)
            {
                var angle = i * Mathf.PI / 4; var probe = target + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                if (!Physics.Raycast(probe + Vector3.up * 80, Vector3.down, out var hit, 180, solid, QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<Ship>()) continue;
                probe.y = Mathf.Max(hit.point.y, ZoneSystem.c_WaterLevel) + .6f;
                if (!Clear(probe, hit.normal.y)) continue;
                landing = probe; return true;
            }
            return false;
        }
        static void Report(string key) => MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ThingText.Text(key));
        internal static void Reset() { ClosePrompt(false); moving = recovering = false; actor = null; character = ""; server = null; generation = -1; }
    }
}
