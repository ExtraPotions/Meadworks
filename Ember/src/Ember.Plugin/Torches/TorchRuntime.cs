// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room (design 12.3). TorchesAreFires has no license grant: nothing of it is copied and its
// DLL is not decompiled. Fire area types come from the vanilla campfire prefab at runtime; the radius is TorchFire.AreaRadius.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Ember.Config;
using ExtraPotions.Ember.Integration;
using ExtraPotions.Ember.Torches;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Ember.Runtime
{
    /// <summary>
    /// E06 torch runtime on every peer (design 3.2): listed fire pieces carry a fire area while they burn, a carrier of a
    /// lit torch carries one too, and a held torch drains per <see cref="TorchBurn.Drain"/>. Endless Torches gives listed
    /// pieces infinite fuel on this instance only (never the prefab). Main thread only.
    /// </summary>
    public static class TorchRuntime
    {
        const string AreaName = "EmberFireArea";
        const string CampfirePrefab = "fire_pit";
        const string TorchPrefab = "Torch";
        const float RefreshSeconds = 0.5f;

        static readonly HashSet<Fireplace> fireplaces = new HashSet<Fireplace>();
        static readonly Dictionary<Fireplace, bool> originalInfiniteFuel = new Dictionary<Fireplace, bool>();
        static readonly HashSet<string> reportedUnknown = new HashSet<string>(StringComparer.Ordinal);
        static MeadworksLog? log;
        static float timer;
        static EffectArea.Type? areaType;
        static int areaLayer;
        static string? listText;
        static HashSet<string> listed = new HashSet<string>(StringComparer.Ordinal);

        internal static IEnumerable<Fireplace> Registered => fireplaces;

        public static void Initialize(ProductRegistration registration, MeadworksLog logger)
        {
            log = logger;
            TorchScheduleRuntime.Initialize(registration);
        }

        public static void Shutdown()
        {
            FireComfortRuntime.Shutdown();
            TorchScheduleRuntime.Shutdown();
            foreach (var fire in fireplaces.ToArray()) Detach(fire);
            fireplaces.Clear();
            originalInfiniteFuel.Clear();
            foreach (var player in Player.GetAllPlayers()) if (player) SetCarrierArea(player, false, CarriedHand.None);
            areaType = null;
        }

        static bool Active => EmberModules.Active(EmberModule.Torches);

        internal static void Register(Fireplace fire)
        {
            if (fire != null && fire.gameObject.layer != Piece.s_ghostLayer) fireplaces.Add(fire);
        }

        public static void Tick(float dt)
        {
            if (!Plugin.Ready || Plugin.Settings == null) return;
            TorchScheduleRuntime.Tick();
            timer += dt;
            if (timer < RefreshSeconds) return;
            timer = 0f;
            FireComfortRuntime.RefreshHeldSmoke();
            var active = Active;
            var warmthReady = !active || EnsureCampfire();
            ReadList();
            // Destroyed pieces compare equal to null in Unity; drop them and their saved fuel flag.
            fireplaces.RemoveWhere(f => f == null);
            foreach (var dead in originalInfiniteFuel.Keys.Where(f => f == null).ToList()) originalInfiniteFuel.Remove(dead);
            foreach (var fire in fireplaces)
            {
                var isListed = listed.Contains(Utils.GetPrefabName(fire.gameObject));
                SetArea(fire.gameObject, TorchFire.PieceIsFire(active && warmthReady,
                    EmberSeasons.Heat(isListed) && Utils.GetPrefabName(fire.gameObject) != "fire_pit", fire.IsBurning()));
                RefreshFire(fire);
                TorchScheduleRuntime.Apply(fire);
            }
            var warms = EmberSeasons.Heat(Plugin.Settings.Get(EmberSettings.Torches.HeldTorchWarms));
            foreach (var player in Player.GetAllPlayers())
            {
                if (player == null) continue;
                var torch = HeldTorch(player);
                SetCarrierArea(player, TorchFire.HeldWarms(active && warmthReady, warms, torch != null, torch?.m_durability ?? 0f),
                    TorchFire.HandFor(torch != null && player.m_rightItem == torch, torch != null && player.m_leftItem == torch));
            }
        }

        internal static void RefreshFire(Fireplace fire)
        {
            if (!fire || Plugin.Settings == null || fire.gameObject.layer == Piece.s_ghostLayer) return;
            var view = fire.GetComponent<ZNetView>();
            if (!view || !view.IsValid()) return;
            ReadList();
            bool active = Active;
            bool comfort = FireComfortRuntime.Target(fire) && Plugin.Settings.Get(EmberSettings.Torches.FuelFreeFires);
            bool endless = listed.Contains(Utils.GetPrefabName(fire.gameObject)) && Plugin.Settings.EndlessTorches;
            SetEndless(fire, active && (comfort || endless));
        }

        /// <summary>A carrier's area hangs at the item attach of the hand holding the torch (as TorchesAreFires' held torch
        /// area does); the root is the fallback. Areas left at the other anchors are switched off.</summary>
        static void SetCarrierArea(Player player, bool on, CarriedHand hand)
        {
            var vis = player.GetComponent<VisEquipment>();
            Transform? right = vis != null ? vis.m_rightHand : null, left = vis != null ? vis.m_leftHand : null;
            var anchor = (hand == CarriedHand.Left ? left : hand == CarriedHand.Right ? right : null) ?? player.transform;
            foreach (var other in new[] { player.transform, right, left })
                if (other != null && other != anchor && other.Find(AreaName) is Transform stale) stale.gameObject.SetActive(false);
            SetArea(player.gameObject, on, anchor);
        }

        /// <summary>Reads the campfire's fire area once (heat and fire types, layer); the radius is <see cref="TorchFire.AreaRadius"/>.</summary>
        static bool EnsureCampfire()
        {
            if (areaType.HasValue) return true;
            var prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab(CampfirePrefab) : null;
            if (prefab == null) return false;
            var area = prefab.GetComponentsInChildren<EffectArea>(true).FirstOrDefault(a => (a.m_type & EffectArea.Type.Heat) != 0);
            if (area == null)
            {
                log?.Warning("The campfire has no heat area; torches stay vanilla.");
                areaType = 0;
                return false;
            }
            areaType = area.m_type & (EffectArea.Type.Heat | EffectArea.Type.Fire);
            // EffectArea.IsPointInsideArea only overlaps the campfire area's layer (character_trigger); a child on Default is never found.
            areaLayer = area.gameObject.layer;
            log?.Verbose("Torch fire area: " + areaType + ", radius " + TorchFire.AreaRadius + " m.");
            return true;
        }

        static void ReadList()
        {
            var text = Plugin.Settings!.Get(EmberSettings.Torches.FirePieces);
            if (text == listText) return;
            listText = text;
            var names = FirePieceList.Parse(text);
            listed = new HashSet<string>(names, StringComparer.Ordinal);
            if (ZNetScene.instance == null) return;
            foreach (var name in FirePieceList.Unknown(names, n => ZNetScene.instance.GetPrefab(n) != null))
                if (reportedUnknown.Add(name)) log?.Info("Torches.Fire Pieces: unknown piece " + name + " is ignored.");
        }

        static ItemDrop.ItemData? HeldTorch(Humanoid humanoid)
        {
            foreach (var item in new[] { humanoid.m_rightItem, humanoid.m_leftItem })
                if (item != null && item.m_dropPrefab != null && item.m_dropPrefab.name == TorchPrefab) return item;
            return null;
        }

        static void SetArea(GameObject? owner, bool on, Transform? anchor = null)
        {
            if (owner == null) return;
            if (anchor == null) anchor = owner.transform;
            var child = anchor.Find(AreaName);
            if (!on || !areaType.HasValue || areaType.Value == 0)
            {
                if (child != null) child.gameObject.SetActive(false);
                return;
            }
            if (child == null)
            {
                var go = new GameObject(AreaName) { layer = areaLayer };
                go.SetActive(false);
                go.transform.SetParent(anchor, false);
                // On a piece, center it on the piece's own vanilla fire area (at the flame), as TorchesAreFires' warmth is.
                var flame = owner.GetComponent<Rigidbody>() == null
                    ? owner.GetComponentsInChildren<EffectArea>(true).FirstOrDefault(a => a.gameObject != go && (a.m_type & EffectArea.Type.Fire) != 0) : null;
                if (flame != null) go.transform.position = flame.transform.position;
                var sphere = go.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = owner.GetComponent<Rigidbody>() != null ? TorchFire.CarriedAreaRadius : TorchFire.AreaRadius;
                // Warmth reaches a character only through EffectArea.OnTriggerEnter (then CustomFixedUpdate calls OnNearFire).
                // Unity reports no trigger contact between colliders of the same rigidbody, so an area on a carrier (whose
                // root has the player's rigidbody) never warmed that carrier. Its own kinematic body, like the vanilla torch's
                // equipped area, makes it a separate body. Pieces have no rigidbody and need none.
                if (owner.GetComponent<Rigidbody>() != null)
                {
                    var body = go.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                }
                var area = go.AddComponent<EffectArea>();
                area.m_type = areaType.Value;
                child = go.transform;
            }
            if (!child.gameObject.activeSelf) child.gameObject.SetActive(true);
        }

        static void Detach(Fireplace fire)
        {
            if (fire == null) return;
            SetArea(fire.gameObject, false);
            SetEndless(fire, false);
        }

        static void SetEndless(Fireplace fire, bool endless)
        {
            if (endless)
            {
                if (!originalInfiniteFuel.ContainsKey(fire)) originalInfiniteFuel[fire] = fire.m_infiniteFuel;
                fire.m_infiniteFuel = true;
            }
            else if (originalInfiniteFuel.TryGetValue(fire, out var original))
            {
                if (fire.m_infiniteFuel) fire.m_infiniteFuel = original;
                originalInfiniteFuel.Remove(fire);
            }
        }

        /// <summary>T3 to T5, Endless Torches: scales the drain step of a held torch; the item's shared data is untouched.</summary>
        internal static void ScaleDrain(Humanoid humanoid, ItemDrop.ItemData item, ref float dt)
        {
            if (!Active || item == null || item.m_dropPrefab == null || item.m_dropPrefab.name != TorchPrefab || Plugin.Settings == null) return;
            var s = Plugin.Settings;
            if (s.Get(EmberSettings.Torches.FuelFreeFires)) { dt = 0f; return; }
            var vanilla = item.m_shared.m_durabilityDrain;
            var settings = new TorchBurnSettings(s.Get(EmberSettings.Torches.EnvironmentAffectsTorch), s.Get(EmberSettings.Torches.ColdBiomeAffectsTorch),
                s.Get(EmberSettings.Torches.BurnRate));
            var environment = EnvMan.instance != null ? EnvMan.instance.GetCurrentEnvironment()?.m_name : null;
            var biome = humanoid is Player player ? player.GetCurrentBiome().ToString() : null;
            var drain = EmberSeasons.Drain(TorchBurn.Drain(vanilla, settings, environment, biome, s.EndlessTorches), s.EndlessTorches);
            if (drain <= 0f) dt = 0f;
            else if (vanilla > 0f) dt *= drain / vanilla;
        }
    }

    [HarmonyPatch]
    internal static class TorchPatches
    {
        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Awake)), HarmonyPostfix]
        static void Awake(Fireplace __instance) => TorchRuntime.Register(__instance);

        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.UpdateFireplace)), HarmonyPrefix]
        static void UpdateFire(Fireplace __instance)
        {
            TorchRuntime.RefreshFire(__instance);
            TorchScheduleRuntime.Apply(__instance);
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DrainEquipedItemDurability)), HarmonyPrefix]
        static void Drain(Humanoid __instance, ItemDrop.ItemData item, ref float dt) => TorchRuntime.ScaleDrain(__instance, item, ref dt);
    }
}
