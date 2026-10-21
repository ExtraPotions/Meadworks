// OpenKeep. Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Adapted from Homestead BedChoice/BedChoiceMap and their input/screen patches, and MapClicks/IconClick at 684671f98de54db6ea6652092ec041f44016c37d.
// Changed by expDARE, 2026: verified request lease, local scoped UI ownership and native input coexistence.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Ember.Beds;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Ember.Runtime
{
    internal static class OwnedBedChoice
    {
        static readonly List<BedPoint> beds = new List<BedPoint>();
        static Game? game;
        static Player? actor;
        static Minimap? map;
        static Hud? hud;
        static bool open, oldBedFilter, oldPingFilter, oldLoadingActive, screenOwned;
        static float deadline, oldZoom, oldLastZoom, oldLoadingAlpha, pendingDue;
        static Vector3 oldOffset, deathPoint;
        static BedPoint? pending;
        static int closeFrame = -1;

        internal static bool Active => open && game && ReferenceEquals(game, Game.instance) && actor
            && actor == Player.m_localPlayer && actor.IsDead() && map && ReferenceEquals(map, Minimap.instance)
            && !Game.m_noMap && OwnedBedRespawn.Active && QuickRespawn.ChoiceCurrent && !ForeignPicker(map);
        internal static IReadOnlyList<BedPoint> Beds => beds;
        internal static Minimap? Map => map;
        internal static int SecondsLeft => Mathf.CeilToInt(Mathf.Max(0f, deadline - Time.time));

        static bool ForeignPicker(Minimap owner)
        {
            if (!owner || !owner.m_mapImageLarge) return false;
            var parent = owner.m_mapImageLarge.transform.parent;
            var picker = parent ? parent.Find("Threshold.Destinations") : null;
            return picker && picker.gameObject.activeInHierarchy;
        }

        internal static bool TryOpen(Player dead, IReadOnlyList<BedPoint> candidates, Vector3 at)
        {
            var owner = Minimap.instance;
            if (open || !dead || dead != Player.m_localPlayer || !dead.IsDead() || !owner || !owner.m_hasGenerated
                || !owner.m_mapImageLarge || !owner.m_biomeNameLarge || owner.m_visibleIconTypes == null
                || owner.m_visibleIconTypes.Length <= (int)Minimap.PinType.Ping || Game.m_noMap || ZInput.VirtualKeyboardOpen
                || !Utils.GetMainCamera() || ForeignPicker(owner) || candidates.Count < 2) return false;
            var unique = new List<BedPoint>();
            foreach (var point in candidates) if (BedPoints.Finite(point) && !BedPoints.Contains(unique, point)) unique.Add(point);
            if (unique.Count < 2 || !QuickRespawn.TryBeginChoice(out var until)) return false;
            try
            {
                game = Game.instance; actor = dead; map = owner; deadline = until; deathPoint = at;
                oldBedFilter = owner.m_visibleIconTypes[(int)Minimap.PinType.Bed];
                oldPingFilter = owner.m_visibleIconTypes[(int)Minimap.PinType.Ping];
                oldZoom = owner.LargeZoom; oldLastZoom = owner.m_lastLargeZoom; oldOffset = owner.m_mapOffset;
                beds.Clear(); beds.AddRange(unique); open = true; pending = null; closeFrame = -1;
                owner.m_visibleIconTypes[(int)Minimap.PinType.Bed] = true;
                owner.m_visibleIconTypes[(int)Minimap.PinType.Ping] = true;
                owner.SetMapMode(Minimap.MapMode.Large);
                owner.m_mapOffset = Vector3.zero;
                double farthest = 0;
                var origin = new BedPoint(at.x, at.y, at.z);
                foreach (var point in beds) farthest = Math.Max(farthest, BedPoints.MapDistance(origin, point));
                double height = (double)owner.m_textureSize * owner.m_pixelSize;
                if (height > 0) owner.LargeZoom = Math.Max(oldZoom, (float)(2d * farthest * 1.25d / height));
                OwnedBedPins.Refresh(owner);
                OwnedBedChoicePresentation.Open(owner, beds[0]);
                return true;
            }
            catch
            {
                Cleanup();
                if (QuickRespawn.ChoiceCurrent) CompleteWithCover();
                else QuickRespawn.AbandonChoice();
                return true;
            }
        }

        internal static void Tick()
        {
            if (!open) return;
            if (!Active) { Close(); return; }
            if (Time.time >= deadline || closeFrame >= 0 && Time.frameCount >= closeFrame) { Confirm(); return; }
            if (pending.HasValue && Time.time >= pendingDue)
            {
                var point = pending.Value; pending = null;
                if (Active && OwnedBedRespawn.Prefer(point)) Confirm();
            }
        }

        static bool InputFocus() => Active && !ZInput.VirtualKeyboardOpen
            && (!Chat.instance || !Chat.instance.HasFocus()) && !global::Console.IsVisible() && !TextInput.IsVisible()
            && !Menu.IsActive();
        internal static bool TakesInput() => InputFocus() && !Minimap.InTextInput();

        static void Queue(Vector3 point)
        {
            if (!TakesInput() || !map) return;
            double radius = OwnedBedChoicePresentation.WorldRadius(map);
            if (radius <= 0) radius = map.PinInteractRadius;
            int nearest = -1; double distance = radius;
            var at = new BedPoint(point.x, point.y, point.z);
            for (int i = 0; i < beds.Count; i++)
            {
                double next = BedPoints.MapDistance(beds[i], at);
                if (next < distance) { distance = next; nearest = i; }
            }
            if (nearest < 0) return;
            pending = beds[nearest]; pendingDue = Time.time + .3f;
        }

        internal static bool LeftClick(Minimap owner)
        {
            if (!Active || !ReferenceEquals(map, owner)) return true;
            if (TakesInput()) Queue(owner.ScreenToWorldPoint(ZInput.pointerPosition));
            return false;
        }
        internal static void DoubleClick(Minimap owner) { if (open && ReferenceEquals(map, owner)) pending = null; }

        internal static bool UpdateMap(Minimap owner)
        {
            if (!open) return true;
            if (!Active) { Close(); return true; }
            if (!ReferenceEquals(map, owner)) return true;
            if (closeFrame >= 0 && Time.frameCount >= closeFrame || Time.time >= deadline) { Confirm(); return false; }
            if (ZInput.VirtualKeyboardOpen || !Utils.GetMainCamera()) return false;
            owner.SetMapMode(Minimap.MapMode.Large); owner.m_hiddenFrames = 0; owner.m_shownFrames++;
            owner.inputDelay = Mathf.Max(0f, owner.inputDelay - Time.deltaTime);
            owner.UpdateNameInput();
            if (InputFocus() && Minimap.InTextInput() && owner.m_namePin != null
                && (ZInput.GetKeyDown(KeyCode.Escape) || ZInput.GetButton("JoyButtonB")))
            {
                owner.m_nameInput.text = ""; owner.OnPinTextEntered("");
            }
            bool input = TakesInput();
            if (input && owner.m_shownFrames > 1 && (ZInput.GetButtonDown("Map") || ZInput.GetKeyDown(KeyCode.Escape)
                || ZInput.GetButtonDown("JoyMap") && (!ZInput.GetButton("JoyLTrigger") || !ZInput.GetButton("JoyLBumper"))
                || ZInput.GetButtonDown("JoyButtonB"))) closeFrame = Time.frameCount + 1;
            bool choosePad = input && !ZInput.IsMouseActive() && ZInput.GetButtonUp("JoyButtonA") && owner.inputDelay <= 0f;
            if (choosePad) Queue(owner.ScreenToWorldPoint(new Vector3(Screen.width * .5f, Screen.height * .5f)));
            // Native A opens a pin-name field; choice A instead selects at the native center reticle.
            owner.UpdateMap(actor!, Time.deltaTime, input && !choosePad);
            owner.UpdateDynamicPins(Time.deltaTime);
            OwnedBedChoicePresentation.BeforePins(owner);
            owner.m_pinUpdateRequired = false;
            owner.UpdatePins();
            OwnedBedChoicePresentation.AfterPins(owner);
            owner.UpdateBiome(actor!);
            OwnedBedChoicePresentation.Label(owner, SecondsLeft);
            return false;
        }

        static void Confirm()
        {
            if (!Active) { Close(); return; }
            Cleanup();
            CompleteWithCover();
        }
        static void CompleteWithCover()
        {
            bool quick = QuickRespawn.Active;
            if (!QuickRespawn.CompleteChoice()) { QuickRespawn.AbandonChoice(); return; }
            var profile = Game.instance ? Game.instance.GetPlayerProfile() : null;
            var screen = Hud.instance ? Hud.instance.m_loadingScreen : null;
            if (quick && profile != null && profile.HaveCustomSpawnPoint() && ZNetScene.instance
                && !ZNetScene.instance.IsAreaReady(profile.GetCustomSpawnPoint()) && screen)
            {
                screen.gameObject.SetActive(true); screen.alpha = 1f;
            }
        }
        internal static void Close() { Cleanup(); QuickRespawn.AbandonChoice(); }

        static void Cleanup()
        {
            if (!open && !map && !screenOwned) return;
            open = false; pending = null; closeFrame = -1;
            var owner = map;
            try { OwnedBedChoicePresentation.Close(owner); }
            finally
            {
                if (owner)
                {
                    if (owner.m_visibleIconTypes[(int)Minimap.PinType.Bed]) owner.m_visibleIconTypes[(int)Minimap.PinType.Bed] = oldBedFilter;
                    if (owner.m_visibleIconTypes[(int)Minimap.PinType.Ping]) owner.m_visibleIconTypes[(int)Minimap.PinType.Ping] = oldPingFilter;
                    if (ReferenceEquals(owner, Minimap.instance) && !ForeignPicker(owner))
                    {
                        if (owner.m_namePin != null && owner.m_nameInput) owner.OnPinTextEntered(owner.m_nameInput.text);
                        owner.LargeZoom = oldZoom; owner.m_lastLargeZoom = oldLastZoom; owner.m_mapOffset = oldOffset;
                        if (owner.m_mode == Minimap.MapMode.Large) owner.SetMapMode(Minimap.MapMode.None);
                        owner.m_hiddenFrames = 3;
                    }
                }
                if (screenOwned && hud && hud.m_loadingScreen)
                {
                    if (hud.m_loadingScreen.alpha == 0f) hud.m_loadingScreen.alpha = oldLoadingAlpha;
                    if (!hud.m_loadingScreen.gameObject.activeSelf) hud.m_loadingScreen.gameObject.SetActive(oldLoadingActive);
                }
                screenOwned = false; hud = null; map = null; actor = null; game = null; beds.Clear();
                if (owner) OwnedBedPins.Refresh(owner);
            }
        }

        internal static bool BlackScreen(Hud owner)
        {
            if (!Active) return true;
            if (!owner.m_loadingScreen) return true;
            if (!screenOwned || !ReferenceEquals(owner, hud))
            {
                hud = owner; oldLoadingAlpha = owner.m_loadingScreen.alpha;
                oldLoadingActive = owner.m_loadingScreen.gameObject.activeSelf; screenOwned = true;
            }
            owner.m_loadingScreen.alpha = 0f; owner.m_loadingScreen.gameObject.SetActive(false);
            return false;
        }

        internal static object Status()
        {
            var points = new List<object>(); foreach (var point in beds) points.Add(new { x = point.X, y = point.Y, z = point.Z });
            var chosen = Game.instance?.GetPlayerProfile()?.GetCustomSpawnPoint() ?? Vector3.zero;
            return new { active = Active, open, deadline, secondsLeft = SecondsLeft, candidates = points,
                selected = new { x = chosen.x, y = chosen.y, z = chosen.z }, deathPoint = new { x = deathPoint.x, y = deathPoint.y, z = deathPoint.z },
                pendingClick = pending.HasValue, pendingDue, mapInstance = map ? map.GetInstanceID() : 0,
                choiceMapMode = map ? map.m_mode.ToString() : "None",
                labelFontSize = OwnedBedChoicePresentation.FontSize, labelScreenFontSize = OwnedBedChoicePresentation.ScreenFontSize };
        }
    }

    [HarmonyPatch(typeof(Minimap), nameof(Minimap.Update))]
    internal static class OwnedBedChoiceMapPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)] static bool Before(Minimap __instance) => OwnedBedChoice.UpdateMap(__instance);
        [HarmonyPostfix] static void After() => OwnedBedChoice.Tick();
    }
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnMapLeftClick))]
    internal static class OwnedBedChoiceClickPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyBefore("com.meadworks.wayfarer", "com.meadworks.threshold")]
        static bool Before(Minimap __instance) => OwnedBedChoice.LeftClick(__instance);
    }
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnMapDblClick))]
    internal static class OwnedBedChoiceDoubleClickPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)] static void Before(Minimap __instance) => OwnedBedChoice.DoubleClick(__instance);
    }
    [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateBlackScreen))]
    internal static class OwnedBedChoiceScreenPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)] static bool Before(Hud __instance) => OwnedBedChoice.BlackScreen(__instance);
    }
}
