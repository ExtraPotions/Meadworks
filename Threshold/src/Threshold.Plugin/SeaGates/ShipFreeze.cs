// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/ShipFreeze.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>A ship whose ZDO says it is stopped by a gate (choosing or jumping) stays where it is on every machine
    /// and every instance, including one recreated after its zone unloads or after an ownership change; a ship inside
    /// its safe time takes no damage.
    /// How the game moves a ship (verified in the decompiled assembly): only the ZDO owner simulates it
    /// (<c>Ship.CustomFixedUpdate</c> returns before buoyancy on any other machine) and its <c>ZSyncTransform</c> writes
    /// the body's pose and velocity to the ZDO each LateUpdate; every other machine places the ship from the ZDO
    /// (snapping moves over 5 m) with gravity off and the body asleep. <c>ZSyncTransform</c> reads <c>isKinematic</c> once
    /// in its Awake and never writes it, and a ship's body is not kinematic. So the owner's body is made kinematic with
    /// zero velocity while frozen and set back to non-kinematic on release; another machine's body is left as the game
    /// keeps it. Nothing is changed in <c>Ship.Awake</c>: a body made kinematic before <c>ZSyncTransform.Awake</c> ran would
    /// be synced as a kinematic body for the rest of its life. No physics step runs between a ship's creation (in
    /// <c>ZNetScene.Update</c>) and its first <c>CustomFixedUpdate</c>, where the freeze takes hold.</summary>
    public static class ShipFreeze
    {
        // Bodies this mod made kinematic, so a release touches only those and sets back exactly what it changed.
        private static readonly HashSet<Rigidbody> frozen = new HashSet<Rigidbody>();

        // The prefabs of every ship this machine has loaded (each joins in Ship.Awake, see ShipJumpAwakePatch): a hit on
        // any other piece is let through after one set lookup, without reading its ZDO.
        private static readonly HashSet<int> shipPrefabs = new HashSet<int>();

        public static bool IsFrozen(ZDO ship) => SeaGateFields.IsStopped(ship);

        /// <summary>The ZDO of a ship prefab. A ship's WearNTear reads the same ZDO as its Ship (both take the ZNetView on
        /// their own object), so of all the pieces only ships' hulls match.</summary>
        public static bool IsShip(ZDO zdo) => shipPrefabs.Contains(zdo.GetPrefab());

        internal static void KnowShip(ZDO zdo) => shipPrefabs.Add(zdo.GetPrefab());

        public static bool IsSafe(ZDO ship) => ship != null && ship.GetLong(SeaGateFields.SafeKey, 0L) > SeaGateFields.Now;

        /// <summary>A stopped ship, every fixed step and wherever the jump changes its pose: the owner's body stops dead
        /// and turns kinematic; on any other machine (an owner a moment ago) it goes back to the game's own handling.</summary>
        internal static void Hold(Ship ship)
        {
            if (ship.m_nview != null && ship.m_nview.IsOwner())
                Freeze(ship.m_body);
            else
                Restore(ship.m_body);
        }

        /// <summary>Velocity first: Unity refuses (and warns about) a velocity set on a kinematic body.</summary>
        internal static void Freeze(Rigidbody body)
        {
            if (body == null || body.isKinematic)
                return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            frozen.Add(body);
        }

        /// <summary>Called every physics step of every ship that is not stopped: nothing to look up while no body is held.</summary>
        internal static void Restore(Rigidbody body)
        {
            if (frozen.Count == 0 || body == null || !frozen.Remove(body))
                return;
            body.isKinematic = false;
        }

        /// <summary>Owner, the end of a jump or a stop: back to physics with the stored speed along the ship's new heading, and the
        /// sail and rudder as they were. Only the owner writes those to the ZDO (<c>Ship.UpdateControlls</c>, skipped
        /// while frozen), so the ZDO still holds them from before the jump, while an owner whose copy of the ship was
        /// recreated at the destination starts with the sail down.</summary>
        internal static void Release(Ship ship, float speed)
        {
            ZDO zdo = ship.m_nview.GetZDO();
            ship.m_speed = (Ship.Speed)zdo.GetInt(ZDOVars.s_forward, (int)ship.m_speed);
            ship.m_rudderValue = zdo.GetFloat(ZDOVars.s_rudder, ship.m_rudderValue);
            Rigidbody body = ship.m_body;
            Restore(body);
            if (body == null || body.isKinematic)
                return;
            body.linearVelocity = ship.transform.forward * speed;
            body.angularVelocity = Vector3.zero;
        }

        /// <summary>Owner: the ship at a pose at once - ZDO, transform and body together, so the owner's
        /// <c>ZSyncTransform</c> (which writes <c>Rigidbody.position</c>/<c>rotation</c> back to the ZDO each LateUpdate)
        /// writes the new pose rather than the old one.</summary>
        internal static void Place(Ship ship, Vector3 pos, Quaternion rot)
        {
            ZDO zdo = ship.m_nview.GetZDO();
            zdo.SetPosition(pos);
            zdo.SetRotation(rot);
            ship.transform.SetPositionAndRotation(pos, rot);
            if (ship.m_body != null)
            {
                ship.m_body.position = pos;
                ship.m_body.rotation = rot;
            }
            Physics.SyncTransforms();
        }

        internal static void Prune() => frozen.RemoveWhere(body => body == null);
    }

    /// <summary>A stopped ship gets no buoyancy, sail force, upside-down or Ashlands damage and no speed changes: its
    /// whole fixed update is skipped, on every machine. Not gated on the settings: these keys are written only by a stop or
    /// jump that has begun, and letting go of a ship mid-jump could drop it into water that is not loaded.</summary>
    [HarmonyPatch(typeof(Ship), nameof(Ship.CustomFixedUpdate))]
    public static class ShipFreezeFixedUpdatePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Ship __instance)
        {
            ZNetView view = __instance != null ? __instance.m_nview : null;
            ZDO zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo == null)
                return true;
            if (!ShipFreeze.IsFrozen(zdo))
            {
                ShipFreeze.Restore(__instance.m_body);
                return true;
            }
            ShipFreeze.Hold(__instance);
            return false;
        }
    }

    /// <summary>A frozen or safe ship takes no damage. <c>WearNTear.ApplyDamage</c> is where every kind ends: hits and
    /// <c>ImpactEffect</c> collisions (both through <c>Damage</c> and the owner's <c>RPC_Damage</c>), the ship's own
    /// water-impact, upside-down and Ashlands damage, and rain/ash/lava wear. Not gated on the settings, for the same
    /// reason as the freeze; the keys are read from the ship's ZDO, so any ship prefab is covered. Every other piece's
    /// hit (this runs for every piece in the world that takes damage or wear) leaves after one set lookup.</summary>
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.ApplyDamage))]
    public static class ShipFreezeDamagePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(WearNTear __instance, ref bool __result)
        {
            ZNetView view = __instance != null ? __instance.m_nview : null;
            ZDO zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo == null || !ShipFreeze.IsShip(zdo) || (!ShipFreeze.IsFrozen(zdo) && !ShipFreeze.IsSafe(zdo)))
                return true;
            __result = false;
            return false;
        }
    }
}
