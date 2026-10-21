// OpenKeep. Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Adapted from Homestead BedPins/BedPinsPatch and static tint in BedPinLook/BedPinLookPatch at 684671f98de54db6ea6652092ec041f44016c37d.
// Changed by expDARE, 2026: Ember scope gates, reference-owned unsaved pins, filter preservation and conditional tint restoration.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Ember.Beds;
using ExtraPotions.Ember.Config;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Ember.Runtime
{
    internal static class OwnedBedPins
    {
        static readonly Color Yellow = new Color(1f, .85f, .1f);
        static readonly List<Minimap.PinData> pins = new List<Minimap.PinData>();
        static readonly Dictionary<Image, Color> colors = new Dictionary<Image, Color>();
        static readonly HashSet<Image> desiredImages = new HashSet<Image>();
        static readonly List<Image> staleImages = new List<Image>();
        static Minimap? owner;
        static string? scope;
        static long generation = -1;
        static float nextCheck;
        static Vector3? shownSpawn;
        static bool showingChoice;

        internal static bool Active => OwnedBedChoice.Active || OwnedBedRespawn.Active && Plugin.Settings?.Get(EmberSettings.Sleep.OwnedBedMapPins) == true;
        internal static IReadOnlyList<Minimap.PinData> Drawn => pins;
        static BedPoint Point(Vector3 p) => new BedPoint(p.x, p.y, p.z);
        static Vector3 Position(BedPoint p) => new Vector3(p.X, p.Y, p.Z);
        static bool SameSpawn(Vector3? a, Vector3? b) => a.HasValue == b.HasValue && (!a.HasValue
            || a.Value.x == b!.Value.x && a.Value.y == b.Value.y && a.Value.z == b.Value.z);
        static Vector3? Spawn()
        {
            var profile = Game.instance ? Game.instance.GetPlayerProfile() : null;
            return profile != null && profile.HaveCustomSpawnPoint() ? profile.GetCustomSpawnPoint() : (Vector3?)null;
        }
        internal static void Tick() => Refresh(Minimap.instance);
        internal static void Refresh(Minimap map)
        {
            if (map && !ReferenceEquals(map, Minimap.instance)) { if (ReferenceEquals(owner, map)) Clear(); return; }
            var current = OwnedBedRespawn.Scope();
            if (!map || !Active || current == null || !Player.m_localPlayer) { Clear(); return; }
            if (!ReferenceEquals(map, owner) || scope != current || generation != CruciblePlugin.Session.Generation)
            {
                Clear(); owner = map; scope = current; generation = CruciblePlugin.Session.Generation;
            }
            var spawn = Spawn();
            bool choice = OwnedBedChoice.Active;
            if (Time.time < nextCheck && SameSpawn(spawn, shownSpawn) && choice == showingChoice) return;
            nextCheck = Time.time + 1f; shownSpawn = spawn; showingChoice = choice;
            var wanted = new List<BedPoint>();
            foreach (var point in OwnedBedChoice.Active ? OwnedBedChoice.Beds : OwnedBedRespawn.Known())
                if ((!spawn.HasValue || !BedPoints.Same(point, Point(spawn.Value))) && !BedPoints.Contains(wanted, point)) wanted.Add(point);
            bool differs = wanted.Count != pins.Count;
            for (int i = 0; !differs && i < pins.Count; i++)
                differs = !map.m_pins.Contains(pins[i]) || !BedPoints.Same(Point(pins[i].m_pos), wanted[i]);
            if (!differs) return;
            RemovePins();
            foreach (var point in wanted)
            {
                var pin = new Minimap.PinData { m_type = Minimap.PinType.Bed, m_name = "", m_pos = Position(point),
                    m_icon = map.GetSprite(Minimap.PinType.Bed), m_save = false };
                // AddPin changes the user's Bed filter; native unsaved insertion leaves that preference intact.
                map.m_pins.Add(pin); pins.Add(pin); map.m_pinUpdateRequired = true;
            }
        }
        static void RemovePins()
        {
            if (owner) foreach (var pin in pins) if (owner.m_pins.Contains(pin)) owner.RemovePin(pin);
            pins.Clear();
        }
        static void Restore(Image image)
        {
            if (image && colors.TryGetValue(image, out var before) && image.color.Equals(Yellow)) image.color = before;
        }
        internal static void Tint(Minimap map)
        {
            Refresh(map);
            if (!owner || !ReferenceEquals(owner, map)) return;
            desiredImages.Clear();
            foreach (var pin in pins) if (pin.m_iconElement) desiredImages.Add(pin.m_iconElement);
            if (map.m_spawnPointPin != null && map.m_spawnPointPin.m_iconElement) desiredImages.Add(map.m_spawnPointPin.m_iconElement);
            staleImages.Clear();
            foreach (var pair in colors) if (!pair.Key || !desiredImages.Contains(pair.Key)) staleImages.Add(pair.Key);
            foreach (var image in staleImages) { Restore(image); colors.Remove(image); }
            foreach (var image in desiredImages)
            {
                var current = image.color;
                if (current.Equals(Color.white))
                {
                    if (!colors.ContainsKey(image)) colors.Add(image, current);
                    image.color = Yellow;
                }
                else if (colors.ContainsKey(image) && !current.Equals(Yellow)) colors.Remove(image);
            }
        }
        internal static void Clear()
        {
            foreach (var pair in colors) Restore(pair.Key);
            colors.Clear(); desiredImages.Clear(); staleImages.Clear(); RemovePins();
            owner = null; scope = null; generation = -1; nextCheck = 0; shownSpawn = null; showingChoice = false;
        }
        internal static object Status()
        {
            var positions = new List<Vector3>(); foreach (var pin in pins) positions.Add(pin.m_pos);
            return new { active = Active, scope, ownedCount = pins.Count, mapInstance = owner ? owner.GetInstanceID() : 0,
                ownedPositions = positions.ToArray(), tintedImages = colors.Count };
        }
    }
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.UpdateProfilePins))]
    internal static class OwnedBedPinsPatch
    {
        [HarmonyPostfix] static void After(Minimap __instance, bool __runOriginal) { if (__runOriginal) OwnedBedPins.Refresh(__instance); }
    }
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.UpdatePins))]
    internal static class OwnedBedPinTintPatch
    {
        [HarmonyPostfix] static void After(Minimap __instance, bool __runOriginal) { if (__runOriginal) OwnedBedPins.Tint(__instance); }
    }
}
