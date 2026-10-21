// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Bring Carts. Idea seen in OdinPlus TeleportEverything / TeleportCarts (no license found): clean-room,
// from its one-line feature description only. Vanilla members read by metadata only (not copied): Vagon.m_instances,
// IsAttached, Detach, m_container, m_nview, RPC_RequestOwn (the "RPC_RequestDenied" answer); Container.GetInventory, IsInUse,
// RPC_RequestOpen (the "RPC_OpenResponse" answer); persistent ZDOs kept by ZNetScene when an instance unloads.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Localization;
using ExtraPotions.Threshold.Portals;
using ExtraPotions.Threshold.Routing;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.Travel
{
    /// <summary>
    /// The cart the local player pulls on a routed trip. Its load lives in the cart's own ZDO and is never copied: the cart
    /// is moved, not rebuilt from its items, so nothing can be lost or doubled. When the game accepts the teleport the cart
    /// is detached and its ZDO (owned by the puller, which the game requires for pulling) is moved at once in front of the
    /// destination exit, so nobody left at the source area can take it over; a still-loaded instance is moved with it and
    /// held still. Until the player lands, this client refuses every request to open the cart's box or take the cart. On
    /// landing the cart is set down on the floor in front of the player, if this client still owns it and its box is closed;
    /// otherwise it is left untouched where it already is, beside the destination portal.
    /// </summary>
    public static class ThresholdCarts
    {
        const float GiveUpSeconds = 120f;

        static ZDOID pending = ZDOID.None;
        static readonly List<(Rigidbody Body, bool Kinematic)> held = new List<(Rigidbody, bool)>();
        static MapPoint exit;
        static float yaw;
        static float startedAt;

        /// <summary>The cart being carried on the current trip, or <see cref="ZDOID.None"/>.</summary>
        public static ZDOID Pending => pending;

        /// <summary>Bring Carts in effect now (server value, TeleportEverything absent, Threshold routing).</summary>
        public static bool Enabled(ThresholdConfig settings) =>
            settings != null && ThresholdModules.ExtrasActive() && settings.Get(ThresholdSettings.Tames.BringCarts);

        public static void Patch(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Container), nameof(Container.RPC_RequestOpen), new[] { typeof(long), typeof(long) }),
                prefix: new HarmonyMethod(typeof(ThresholdCarts), nameof(BeforeRequestOpen)));
            harmony.Patch(AccessTools.Method(typeof(Vagon), nameof(Vagon.RPC_RequestOwn), new[] { typeof(long) }),
                prefix: new HarmonyMethod(typeof(ThresholdCarts), nameof(BeforeRequestOwn)));
        }

        /// <summary>The cart the player pulls, or null.</summary>
        public static Vagon? Pulled(Player player)
        {
            if (player == null) return null;
            foreach (var cart in Vagon.m_instances)
                if (cart != null && cart.IsAttached(player)) return cart;
            return null;
        }

        /// <summary>The trip check for the pulled cart. <paramref name="teleportable"/> is the traveler's inventory rule
        /// (the game's check with the world's rules and Teleport Listed Items) applied to the cart's load.</summary>
        public static CartOutcome Decide(Vagon? cart, ThresholdConfig settings, bool enteredAllowsAllItems, bool destinationAllowsAllItems,
            Func<Inventory, bool> teleportable)
        {
            var view = cart != null ? cart.m_nview : null;
            var box = cart != null ? cart.m_container : null;
            var load = box != null ? box.GetInventory() : null;
            bool attached = cart != null;
            bool free = view != null && view.IsValid() && view.IsOwner() && box != null && load != null && !box.IsInUse();
            var outcome = CartRules.Decide(new CartFacts(attached, Enabled(settings), free,
                free && !load!.IsTeleportable(true), free && teleportable(load!),
                enteredAllowsAllItems, destinationAllowsAllItems, settings.Get(ThresholdSettings.General.OreTransport), settings.IgnoreTeleportRestrictions));
            if (attached) Plugin.Log?.Debug("Threshold carts: " + outcome + (load != null ? " (" + load.NrOfItems() + " stacks)" : ""));
            return outcome;
        }

        /// <summary>The game accepted the teleport: detach the cart and move it, with its load, to the destination.</summary>
        public static void Begin(Vagon cart, MapPoint exitPoint, float exitYaw)
        {
            Reset();
            var view = cart != null ? cart.m_nview : null;
            if (cart == null || view == null || !view.IsValid() || !view.IsOwner()) return;
            exit = exitPoint;
            yaw = exitYaw;
            startedAt = Time.time;
            cart.Detach();
            var spot = CartRules.Place(exitPoint, exitYaw);
            var to = new Vector3(spot.X, spot.Y, spot.Z);
            var turn = Quaternion.Euler(0f, exitYaw, 0f);
            Move(cart, to, turn, hold: true);
            var zdo = view.GetZDO();
            zdo.SetPosition(to);
            zdo.SetRotation(turn);
            pending = zdo.m_uid;
            Plugin.Log?.Debug("Threshold carts: " + pending + " sent to " + to.ToString("F1"));
        }

        /// <summary>Each frame on a client (from <see cref="ThresholdTames.Tick"/>): sets the cart down once the jump landed.</summary>
        public static void Tick()
        {
            if (pending == ZDOID.None) return;
            var player = Player.m_localPlayer;
            if (player == null || player.IsDead() || Time.time - startedAt > GiveUpSeconds)
            {
                Plugin.Log?.Debug("Threshold carts: " + pending + " left at the destination portal (player gone, dead or the jump timed out).");
                Reset();
                return;
            }
            if (player.IsTeleporting()) return;
            try
            {
                var at = player.transform.position;
                var landed = new MapPoint(at.x, at.y, at.z);
                if (TameRules.Landed(landed, exit)) SetDown(landed);
                else Plugin.Log?.Debug("Threshold carts: the player is not over the exit; " + pending + " stays beside the destination portal.");
            }
            catch (Exception e) { Plugin.Log?.Warning("Threshold carts: set-down failed: " + e.GetType().Name + ": " + e.Message); }
            Reset();
        }

        static void SetDown(MapPoint landed)
        {
            var zdos = ZDOMan.instance;
            var zdo = zdos != null ? zdos.GetZDO(pending) : null;
            if (zdo == null || !zdo.IsValid())
            {
                Plugin.Log?.Debug("Threshold carts: " + pending + " no longer exists.");
                return;
            }
            long self = ZDOMan.GetSessionID();
            var view = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(zdo) : null;
            var cart = view != null ? view.GetComponent<Vagon>() : null;
            bool open = cart != null && cart.m_container != null && cart.m_container.IsInUse();
            if (zdo.HasOwner() && zdo.GetOwner() != self || open)
            {
                Plugin.Log?.Debug("Threshold carts: " + pending + " is another player's now or open; left where it is.");
                return;
            }
            var spot = CartRules.Place(landed, yaw);
            float? floor = ZoneSystem.instance != null
                && ZoneSystem.instance.FindFloor(new Vector3(spot.X, TameRules.ProbeHeight(landed), spot.Z), out var height) ? height : (float?)null;
            var settled = CartRules.Settle(spot, landed, floor);
            var to = new Vector3(settled.X, settled.Y, settled.Z);
            var turn = Quaternion.Euler(0f, yaw, 0f);
            zdo.SetOwner(self);
            zdo.SetPosition(to);
            zdo.SetRotation(turn);
            if (cart != null) Move(cart, to, turn, hold: false);
            Plugin.Log?.Debug("Threshold carts: " + pending + " set down at " + to.ToString("F1") + (cart != null ? " (loaded)" : " (rebuilt by streaming)"));
        }

        /// <summary>Moves the cart and every rigidbody of it (wheels included) keeping their layout; <paramref name="hold"/>
        /// keeps them still (kinematic) until the set-down, which restores what they were.</summary>
        static void Move(Vagon cart, Vector3 to, Quaternion turn, bool hold)
        {
            var root = cart.transform;
            var from = root.position;
            var undo = Quaternion.Inverse(root.rotation);
            var bodies = cart.GetComponentsInChildren<Rigidbody>(true).Where(b => b != null).ToList();
            var layout = bodies.Select(b => (Body: b, Offset: undo * (b.position - from), Turn: undo * b.rotation)).ToList();
            root.SetPositionAndRotation(to, turn);
            foreach (var (body, offset, rotation) in layout)
            {
                if (hold && !held.Any(h => h.Body == body)) held.Add((body, body.isKinematic));
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.transform.SetPositionAndRotation(to + turn * offset, turn * rotation);
                body.position = body.transform.position;
                body.rotation = body.transform.rotation;
                if (hold) body.isKinematic = true;
            }
            if (!hold) Release();
        }

        static void Release()
        {
            foreach (var (body, kinematic) in held)
                if (body != null) body.isKinematic = kinematic;
            held.Clear();
        }

        static bool Carried(ZNetView? view) =>
            pending != ZDOID.None && view != null && view.IsValid() && view.GetZDO().m_uid == pending;

        /// <summary>While the cart travels nobody opens its box (this client owns it, so it answers the request).</summary>
        static bool BeforeRequestOpen(Container __instance, long __0)
        {
            if (__instance == null || !Carried(__instance.m_nview)) return true;
            __instance.m_nview.InvokeRPC(__0, "RPC_OpenResponse", false);
            return false;
        }

        /// <summary>While the cart travels nobody takes it over.</summary>
        static bool BeforeRequestOwn(Vagon __instance, long __0)
        {
            if (__instance == null || !Carried(__instance.m_nview)) return true;
            __instance.m_nview.InvokeRPC(__0, "RPC_RequestDenied");
            return false;
        }

        /// <summary>The message when the cart stops a trip or stays.</summary>
        public static string Message(CartOutcome outcome) =>
            outcome == CartOutcome.Refuse ? ThresholdText.Text(ThresholdTextKeys.CartBlocked)
            : outcome == CartOutcome.StayBusy ? ThresholdText.Text(ThresholdTextKeys.CartStays) : "";

        public static void Reset()
        {
            Release();
            pending = ZDOID.None;
        }
    }
}
