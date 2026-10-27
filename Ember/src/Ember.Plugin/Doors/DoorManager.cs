// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2023 Pete Navarra (aka Vapok), MIT. Adapted from DoorOpenerBruh 2.0.10 at
// dafb2af2418710dd1f87907e8b30d4efb1b68a20: DoorOpenerBruh/Components/DoorStatus.cs (Awake collider cache, ghost
// check, CheckPlayerPositionForDoors: open through Door.Interact, close through UseDoor, GetDistanceToPlayer input)
// and DoorOpenerBruh/Patches/Door.cs (Door.Awake registration, headless guard).
// The MIT notice is retained in THIRD_PARTY_NOTICES/DoorOpenerBruh-MIT.txt.
// Changed by expDARE, 2026: one manager instead of a component per door, checked at 20 Hz only for doors near the
// player (D13); decisions in Ember.Core (DoorTracker with D11 Keep Open While Others Near, DoorRules, DoorDistance);
// close only for doors the automation took, with the game's toggle; Crucible headless guard and module gate;
// Toggle Key; namespace Ember.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Ember.Config;
using ExtraPotions.Ember.Doors;
using ExtraPotions.Ember.Integration;
using ExtraPotions.Ember.Localization;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Ember.Runtime
{
    /// <summary>
    /// E08 door manager on a graphical client: acts only for the local player and only on doors near that player
    /// (design 3.3). Opening goes through Door.Interact, so key, ward and CanInteract apply as for a key press; closing
    /// sends the game's UseDoor toggle for doors this client took (D5) unless D11 keeps it open. Door.HaveKey is read afresh at
    /// every check of a near door, so a key that enters the inventory while the player stands at a keyed door opens it on the
    /// next check (the tracker does not adopt a shut door it was not allowed to open). Main thread only.
    /// </summary>
    public static class DoorManager
    {
        const float CheckSeconds = 0.05f;
        const float NearMargin = 2f;

        sealed class Tracked
        {
            public readonly DoorTracker Tracker = new DoorTracker();
            public Collider[] Colliders = Array.Empty<Collider>();
            public DoorKind Kind = DoorKinds.Other;
            public Piece? Piece;
        }

        static readonly Dictionary<Door, Tracked> doors = new Dictionary<Door, Tracked>();
        static readonly List<Door> deadDoors = new List<Door>();
        static readonly List<Player> othersPool = new List<Player>();
        static readonly List<KeyValuePair<Door, Tracked>> doorSnapshot = new List<KeyValuePair<Door, Tracked>>();
        static bool checking;
        static int generation;
        static MeadworksLog? log;
        static float timer;
        static Player? lastPlayer;
        static bool wasEnabled;

        public static int Count => doors.Count;

        public static void Initialize(MeadworksLog logger) => log = logger;

        public static void Shutdown()
        {
            generation++;
            doors.Clear();
            if (!checking)
            {
                deadDoors.Clear();
                othersPool.Clear();
                doorSnapshot.Clear();
            }
            timer = 0f;
            wasEnabled = false;
            lastPlayer = null;
        }

        /// <summary>D8, D10: registers a placed door (never a ghost, never on a headless process).</summary>
        internal static void Register(Door door)
        {
            if (door == null || !MeadworksHeadless.AllowsUi || door.gameObject.layer == Piece.s_ghostLayer
                || door.m_nview == null || door.m_nview.GetZDO() == null || doors.ContainsKey(door)) return;
            doors[door] = new Tracked
            {
                Colliders = door.GetComponentsInChildren<Collider>(true),
                Kind = DoorKinds.ForPrefab(Utils.GetPrefabName(door.gameObject)),
                Piece = door.GetComponent<Piece>(),
            };
        }

        static bool Enabled => EmberModules.Active(EmberModule.Doors) && Plugin.Settings!.Get(EmberSettings.Doors.EnableAutoDoor);

        public static void Tick(float dt)
        {
            if (checking || !Plugin.Ready || Plugin.Settings == null || !MeadworksHeadless.AllowsUi) return;
            ToggleKey();
            timer += dt;
            if (timer < CheckSeconds) return;
            timer = 0f;
            var player = Player.m_localPlayer;
            if (player != lastPlayer)
            {
                foreach (var t in doors.Values) t.Tracker.Reset();
                lastPlayer = player;
            }
            var enabled = Enabled;
            if (!enabled && wasEnabled)
                foreach (var t in doors.Values) t.Tracker.Reset();
            wasEnabled = enabled;
            if (player == null || player.IsDead() || !enabled) return;
            Check(player);
        }

        static void Check(Player player)
        {
            if (checking) return;
            checking = true;
            int observedGeneration = generation;
            try
            {
                doorSnapshot.Clear();
                doorSnapshot.AddRange(doors);
                var settings = Plugin.Settings!;
                var keep = settings.Get(EmberSettings.Doors.KeepOpenWhileOthersNear);
                var reach = DoorKinds.All.Max(k => settings.Door(k).Open) + NearMargin;
                var position = player.transform.position;
                othersPool.Clear();
                if (keep)
                {
                    foreach (var p in Player.GetAllPlayers())
                    {
                        if (p != null && p != player && !p.IsDead())
                            othersPool.Add(p);
                    }
                }

                deadDoors.Clear();
                foreach (var pair in doorSnapshot)
                {
                    if (observedGeneration != generation) return;
                    var door = pair.Key;
                    var t = pair.Value;
                    if (door == null)
                    {
                        deadDoors.Add(door!);
                        continue;
                    }
                    if (!t.Tracker.InRange && !t.Tracker.AutoOpened && (door.transform.position - position).sqrMagnitude > reach * reach) continue;
                    if (door.m_nview == null || !door.m_nview.IsValid()) continue;
                    var zdo = door.m_nview.GetZDO();
                    if (zdo == null) continue;
                    // A door the automation opened is left alone while it is still swinging (the game refuses UseDoor and
                    // CanInteract until the animation settles). Stepping now would read "automation not allowed" and drop the
                    // door without closing it, which a player walking off quickly (short Close Distance) hits at once.
                    if (t.Tracker.AutoOpened && !door.CanInteract()) continue;
                    var kind = settings.Door(t.Kind);
                    var facts = new DoorFacts(true, door.gameObject.layer == Piece.s_ghostLayer, door.CanInteract(),
                        t.Piece != null ? t.Piece.GetCreator() : 0L, player.GetPlayerID(), door.m_keyItem != null,
                        door.m_keyItem != null && door.HaveKey(player, false));
                    var allowed = DoorRules.AutomationEnabled(kind.Mechanic, t.Kind.HasCheckForKey, kind.CheckForKey, facts);
                    var otherNear = keep && othersPool.Any(o => Distance(door, t, o.transform.position) <= kind.Close);
                    var tick = new DoorTick(Distance(door, t, position), kind.Open, kind.Close, allowed,
                        zdo.GetInt(ZDOVars.s_state), door.m_invertedOpenClosedText, keep, otherNear);
                    switch (t.Tracker.Step(tick))
                    {
                        case DoorAction.Open:
                            try { door.Interact(player, false, false); }
                            catch (Exception error) { log?.Warning("Door interaction failed: " + error.GetType().Name + ": " + error.Message); }
                            break;
                        case DoorAction.Close:
                            // UseDoor toggles: from any open state it reaches the closed state of DoorState.ClosedState.
                            door.m_nview.InvokeRPC("UseDoor", true);
                            break;
                    }
                }
                if (deadDoors.Count > 0)
                {
                    foreach (var d in deadDoors) doors.Remove(d);
                    deadDoors.Clear();
                }
            }
            finally
            {
                deadDoors.Clear();
                othersPool.Clear();
                doorSnapshot.Clear();
                checking = false;
            }
        }

        /// <summary>D2: the smaller of the distance to the door's origin and to its enabled, solid colliders' bounds.</summary>
        static double Distance(Door door, Tracked t, Vector3 p)
        {
            var boxes = t.Colliders.Where(c => c != null && c.enabled && !c.isTrigger)
                .Select(c => new Box(new Vec3(c.bounds.min.x, c.bounds.min.y, c.bounds.min.z), new Vec3(c.bounds.max.x, c.bounds.max.y, c.bounds.max.z)));
            var o = door.transform.position;
            return DoorDistance.ToPlayer(new Vec3(o.x, o.y, o.z), boxes, new Vec3(p.x, p.y, p.z));
        }

        /// <summary>Doors.Toggle Key flips Enable Auto Door with a centre message (C07 gate: never while typing).</summary>
        static void ToggleKey()
        {
            var settings = Plugin.Settings!;
            if (!EmberModules.ProductActive(out _) || !MeadworksInput.Pressed(settings.Shortcut(EmberSettings.Doors.ToggleKey))) return;
            if (!settings.Get(EmberSettings.Doors.AutoDoorsAllowed))
            {
                EmberText.Centre(EmberText.Text(EmberTextKeys.DoorsNotAllowed), log!);
                return;
            }
            if (!settings.TryChange(EmberSettings.Doors.EnableAutoDoor)) return;
            var entry = (ConfigEntry<bool>)settings.Entry(EmberSettings.Doors.EnableAutoDoor);
            entry.Value = !entry.Value;
            EmberText.Centre(EmberText.Text(entry.Value ? EmberTextKeys.DoorsOn : EmberTextKeys.DoorsOff), log!);
        }
    }

    [HarmonyPatch]
    internal static class DoorPatches
    {
        [HarmonyPatch(typeof(Door), nameof(Door.Awake)), HarmonyPostfix]
        static void Awake(Door __instance) => DoorManager.Register(__instance);

        /// <summary>Compile-time check that the game's UseDoor handler still takes a bool (the close path sends one).</summary>
        static void UseDoorSignature(Door door) => door.RPC_UseDoor(0L, true);
    }
}
