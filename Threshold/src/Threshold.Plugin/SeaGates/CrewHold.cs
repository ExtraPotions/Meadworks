// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/CrewHold.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>Holds the local player inside the game's teleport until the ship has settled, then lands them on its
    /// deck (or ashore by the destination gate after the wait, <see cref="CrewFallback"/>), and ends the teleport
    /// itself. Vanilla <c>Player.UpdateTeleport</c> (verified in the decompiled assembly) knows nothing of ships: from
    /// 2 s it pins the player at the target with zero velocity, then ends after <c>ZNetScene.IsAreaReady</c> and 8 s
    /// wherever <c>ZoneSystem.FindFloor</c> hits (the seabed at sea; ships are not on its layers), or at
    /// <c>GetSolidHeight + 0.5</c> after 15 s. So for a sea gate jump the prefix replaces it entirely. While
    /// <c>m_teleporting</c> is set the game itself blocks every hit (<c>RPC_Damage</c> and <c>ApplyDamage</c> return),
    /// reads no water depth (<c>CalculateLiquidDepth</c>) and takes no input. An active hold runs to its end even if
    /// the settings turn sea gates off meanwhile: handing a held player back to vanilla would drop them at sea.</summary>
    public static class CrewHold
    {
        private const float FadeSeconds = 2f;      // vanilla's fade: it starts pinning the player at 2 s
        private const float LandTolerance = 1f;    // the local ship copy must be this close to its ZDO position
        private const float AngleTolerance = 5f;   // and turned this close to its ZDO rotation
        private const float DeckLift = 0.05f;      // land a few cm above the measured spot
        private const float MinWaitSeconds = 5f;
        private const float DeckCutoffSeconds = 2f; // no landing on deck this close to the ship's own timeout
        private const float SteadySeconds = 1f;    // everything ready this long before the loading screen lifts

        private static Player player;
        private static JumpOrder order;
        private static CrewSpot spot;
        private static bool hadHelm;
        private static float timer;                // the game's teleport timer: the fade, physics steps
        private static long startedAt;             // network ticks: the wait, the clock the ship's timeout uses
        private static bool failed;
        private static bool fadePinned;
        private static float landReadySince = -1f;
        private static float fallbackReadySince = -1f;
        private static Vector3 fadePos;
        private static Quaternion fadeRot;

        /// <summary>True while the local player is in a sea gate jump.</summary>
        public static bool Holding { get; private set; }

        /// <summary>The jump being held, 0 when none.</summary>
        public static int JumpId => Holding && order != null ? order.JumpId : 0;

        internal static void Begin(Player p, JumpOrder o, CrewSpot s, bool helm)
        {
            player = p;
            order = o;
            spot = s;
            hadHelm = helm;
            timer = 0f;
            startedAt = SeaGateFields.Now;
            failed = false;
            fadePinned = false;
            landReadySince = -1f;
            fallbackReadySince = -1f;
            Holding = true;
            CrewHelm.Clear();
            CrewFallback.Reset();
        }

        internal static void Clear()
        {
            Holding = false;
            player = null;
            order = null;
        }

        /// <summary>Where a held player should come back after a logout: beside the destination gate, where the fallback
        /// would set them ashore, rather than over the water they are held above.</summary>
        internal static bool TryLogoutPoint(out Vector3 point)
        {
            point = Vector3.zero;
            if (!Holding || order == null || player == null)
                return false;
            CrewFallback.HoldPoint(order, player, out point, out Quaternion _);
            return true;
        }

        /// <summary>Death, logout or world unload of the local player: drop the hold, the helm request, the pending
        /// report and the protection with it.</summary>
        internal static void Forget(Player p)
        {
            if (p == null || (p != player && p != Player.m_localPlayer))
                return;
            Clear();
            CrewHelm.Clear();
            CrewReport.Clear();
            JumpProtection.Clear();
        }

        /// <summary>The prefix body. True when this step was handled (vanilla skipped). On an exception the hold ends
        /// and vanilla finishes the teleport: a player left at the target beats a player stuck in the loading screen.</summary>
        internal static bool Step(Player p, float dt)
        {
            try
            {
                return Advance(p, dt);
            }
            catch (Exception e)
            {
                if (!failed)
                    SeaGateLog.LogError($"Sea gate crew hold failed, the game finishes the teleport: {e}");
                failed = true;
                Clear();
                return false;
            }
        }

        /// <summary>Every physics step of the local player while no hold runs (from <c>TeleportStepPatch</c>): sends a
        /// pending landing report again (whatever the settings say: it finishes a jump) and runs the helm request after
        /// a landing.</summary>
        internal static void Between(Player p)
        {
            CrewReport.Tick();
            if (SeaGateConfig.On)
                CrewHelm.Tick(p);
        }

        private static bool Advance(Player p, float dt)
        {
            if (p != player || order == null || !p.m_teleporting)
            {
                Clear();
                return false;
            }
            timer += dt;
            p.m_teleportTimer = timer;
            p.m_teleportCooldown = 0f;
            if (timer > FadeSeconds && TryLand(p))
                return true;
            if (WaitOver && TryFallback(p))
                return true;
            Hold(p);
            return true;
        }

        private static float WaitSeconds => Mathf.Max(order.WaitSeconds, MinWaitSeconds);

        /// <summary>The crew's wait, on the network clock rather than summed physics steps: those fall behind real time
        /// while a new area loads in hitches, and the ship counts its own timeout (<see cref="SeaGateFields.UntilKey"/>,
        /// a few seconds longer) on the network clock.</summary>
        private static bool WaitOver => SeaGateFields.Now - startedAt > (long)(WaitSeconds * TimeSpan.TicksPerSecond);

        /// <summary>Lands on the ship once the ZDO shows this jump settled, the local copy stands within 1 m of its ZDO
        /// position and the area around the deck spot is loaded, unless the ship is about to stop waiting.</summary>
        private static bool TryLand(Player p)
        {
            ZDO zdo = CrewShip.Zdo(order.Ship);
            Ship ship = CrewShip.Settled(zdo, order.JumpId) && !ShipLeavingSoon(zdo) ? CrewShip.Find(zdo) : null;
            if (ship == null || !Arrived(ship.transform, zdo))
                return Steady(false, ref landReadySince);
            Transform frame = ship.transform;
            Vector3 pos = spot.PositionOn(frame.position, frame.rotation) + Vector3.up * DeckLift;
            if (!Steady(ZNetScene.instance != null && ZNetScene.instance.IsAreaReady(pos), ref landReadySince))
                return false;
            ZDOID shipId = order.Ship;
            bool retakeHelm = hadHelm;
            Land(p, pos, spot.FacingOn(frame.rotation));
            if (retakeHelm)
                CrewHelm.Request(shipId);
            return true;
        }

        /// <summary>True once <paramref name="ready"/> has held for <see cref="SteadySeconds"/>: the loading screen stays
        /// up a second after everything is in place, so the area around the player has finished streaming in.</summary>
        private static bool Steady(bool ready, ref float since)
        {
            if (!ready)
            {
                since = -1f;
                return false;
            }
            if (since < 0f)
                since = Time.time;
            return Time.time - since >= SteadySeconds;
        }

        /// <summary>The ship stops waiting for its crew at its own timeout and gets its speed back. A landing this close
        /// to it might reach the owner too late for the second it waits after a landing, and a ship gaining speed under
        /// a player still a few cm above the deck can leave them behind; from here on only the fallback ashore.</summary>
        private static bool ShipLeavingSoon(ZDO zdo) =>
            SeaGateFields.Now > zdo.GetLong(SeaGateFields.UntilKey, 0L) - (long)(DeckCutoffSeconds * TimeSpan.TicksPerSecond);

        /// <summary>This machine's copy of the ship has caught up with its ZDO pose (<c>ZSyncTransform</c> moves a copy
        /// it does not own toward the ZDO each frame, snapping over 5 m or 45 degrees).</summary>
        private static bool Arrived(Transform frame, ZDO zdo) =>
            Vector3.Distance(frame.position, zdo.GetPosition()) <= LandTolerance &&
            Quaternion.Angle(frame.rotation, zdo.GetRotation()) <= AngleTolerance;

        private static bool TryFallback(Player p)
        {
            bool found = CrewFallback.TryFind(order, p, out Vector3 pos, out Quaternion rot);
            if (!Steady(found, ref fallbackReadySince))
                return false;
            Land(p, pos, rot);
            p.Message(MessageHud.MessageType.Center, SeaGateWords.Fallback);
            return true;
        }

        /// <summary>Ends the teleport the way vanilla's success path does (<c>m_teleporting</c> off, timer reset,
        /// <c>ResetCloth</c>), with the protection started first and the ship's owner told.</summary>
        private static void Land(Player p, Vector3 pos, Quaternion rot)
        {
            JumpProtection.Start(SeaGateConfig.ProtectionSeconds.Value);
            Pin(p, pos, rot);
            p.SetLookDir(rot * Vector3.forward);
            p.m_teleportTimer = 0f;
            p.m_teleporting = false;
            p.ResetCloth();
            if (EnvMan.instance != null)
                EnvMan.instance.ForceInstantEnvironmentSwitch();
            CrewReport.Landed(order.Ship, order.JumpId, p.GetPlayerID());
            Clear();
        }

        private static void Hold(Player p)
        {
            if (timer <= FadeSeconds)
            {
                HoldDuringFade(p);
                return;
            }
            Vector3 pos;
            Quaternion rot;
            if (WaitOver)
                CrewFallback.HoldPoint(order, p, out pos, out rot);
            else
                DeckTarget(out pos, out rot);
            p.m_teleportTargetPos = pos;  // the latest target, should vanilla ever take over (see Step)
            p.m_teleportTargetRot = rot;
            Pin(p, pos, rot);
            p.SetLookDir(rot * Vector3.forward);
            if (EnvMan.instance != null)
                EnvMan.instance.ForceInstantEnvironmentSwitch();
        }

        /// <summary>The game's fade (vanilla leaves the player alone until 2 s): while they stand on the frozen ship they
        /// stay on it; from the moment they don't (the ship moved on, or they were in the air or the water) they are
        /// pinned where they were, so nobody falls or sinks while the screen goes dark.</summary>
        private static void HoldDuringFade(Player p)
        {
            if (!fadePinned && StandingOnFrozenShip(p))
                return;
            if (!fadePinned)
            {
                fadePinned = true;
                fadePos = p.transform.position;
                fadeRot = p.transform.rotation;
            }
            Pin(p, fadePos, fadeRot);
        }

        private static bool StandingOnFrozenShip(Player p)
        {
            ZDO zdo = CrewShip.Zdo(order.Ship);
            Ship ship = CrewShip.Find(zdo);
            return ship != null && !CrewShip.Moved(zdo, order.JumpId) && p.GetStandingOnShip() == ship;
        }

        /// <summary>The deck spot on the ship's pose as its ZDO shows it once moved, else on the order's destination
        /// pose: the player waits where the ship is (or will be), so the game streams that area.</summary>
        private static void DeckTarget(out Vector3 pos, out Quaternion rot)
        {
            ZDO zdo = CrewShip.Zdo(order.Ship);
            bool moved = CrewShip.Moved(zdo, order.JumpId);
            Vector3 shipPos = moved ? zdo.GetPosition() : order.DestPos;
            Quaternion shipRot = moved ? zdo.GetRotation() : order.DestRot;
            pos = spot.PositionOn(shipPos, shipRot) + Vector3.up * DeckLift;
            rot = spot.FacingOn(shipRot);
        }

        /// <summary>As vanilla pins a teleporting player: position, rotation, zero velocity, fall height reset.</summary>
        private static void Pin(Player p, Vector3 pos, Quaternion rot)
        {
            p.transform.position = pos;
            p.transform.rotation = rot;
            p.m_body.linearVelocity = Vector3.zero;
            p.m_maxAirAltitude = pos.y;
        }
    }

    /// <summary>Death ends a hold and drops the pending helm request and report (a dead player's <c>FixedUpdate</c> no
    /// longer calls <c>UpdateTeleport</c>).</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
    public static class CrewHoldDeathPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Player __instance) => CrewHold.Forget(__instance);
    }

    /// <summary>Logout and world unload destroy the local player: the hold ends with it.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnDestroy))]
    public static class CrewHoldDestroyPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Player __instance) => CrewHold.Forget(__instance);
    }
}
