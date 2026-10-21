// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: HUD badge on the Crucible.UI kit HUD panel (KX07) and the warning sound (design 4.3, 8, W8).
using System;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Tempest.Config;
using ExtraPotions.Tempest.Events;
using ExtraPotions.Tempest.Integration;
using ExtraPotions.Tempest.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>
    /// TP07b. A display-only kit HUD panel with the event kind, phase and countdown on a KX07 panel and a kit meter for
    /// the part of the phase left; automatic position top center, movable through the Crucible HUD layout; hidden with the
    /// HUD and while a player window is open (<see cref="TempestWindows"/>). A game sound plays once when an event's warning reaches the local player (Warning Sound).
    /// Never built on a headless process or without Crucible.UI. Main thread only.
    /// </summary>
    public static class TempestBadge
    {
        const float Width = 240, MaxWidth = 440, Pad = 12, Glyph = 22, Gap = 6;
        const float Height = 82;
        const float DefaultTop = 96;

        static readonly WarningCue Cue = new WarningCue();
        static KitHudPanel? panel;
        static Transform? owner;
        static TMP_Text? label, detail;
        static KitMeter? meter;
        static Image? icon;
        static bool warnedDisplay;
        static string? shownTitle, shownLine;
        static EventPhase shownPhase;
        static long shownWhole = long.MinValue;
        static bool warnedSound;
        static MeadworksLog? log;

        public static void Initialize(MeadworksLog logger) => log = logger;

        /// <summary>Called from Update.</summary>
        public static void Tick()
        {
            if (!Plugin.AllowsUi) return;
            var settings = Plugin.Settings;
            var player = Player.m_localPlayer;
            if (settings == null || player == null || !TempestModules.Active(TempestModule.Events))
            {
                Hide();
                return;
            }
            var e = TempestWorld.Current;
            var now = TempestWorld.Now;
            var biome = TempestWorld.LocalBiome;
            var forced = EnvMan.instance != null && !string.IsNullOrEmpty(EnvMan.instance.m_forceEnv);
            var notices = TempestSettings.Notices;
            if (Cue.Observe(e, now, biome, forced) && settings.Get(notices.WarningSound) && e != null) PlaySound(e.Kind, player.transform.position);
            var state = Badge.For(e, now, biome, forced, settings.Get(notices.HudBadge), CairnDisplaysWeather());
            var root = Hud.instance != null && Hud.instance.m_rootObject != null ? Hud.instance.m_rootObject.transform : null;
            var visible = state.Visible && CrucibleUiPlugin.Active && root != null && KitCanvas.Font != null
                && !Hud.IsUserHidden() && !TempestWindows.Open();
            if (!visible)
            {
                Hide();
                return;
            }
            if (panel != null && (panel.Root == null || owner != root)) Reset();
            if (panel == null && !Build(root!)) return;
            // Two centered lines: the glyph and event name, then the phase countdown ("Time remaining 29:37").
            var title = Badge.Title(state, TempestText.Text);
            var whole = TempestTextKeys.WholeSeconds(state.SecondsLeft);
            if (shownTitle == null || title != shownTitle || state.Phase != shownPhase || whole != shownWhole)
            {
                var line = Badge.Detail(state, (key, value) => TempestText.Format(key, value));
                if (title != shownTitle || line != shownLine)
                {
                    label!.text = title;
                    detail!.text = line;
                    Fit();
                }
                shownTitle = title;
                shownLine = line;
                shownPhase = state.Phase;
                shownWhole = whole;
            }
            meter!.Set((float)state.Fraction, 1f);
            icon!.sprite = KitGlyphSprites.Of(GlyphOf(state.Kind));
            var free = ZCursor.LockState != CursorLockMode.Locked && ZCursor.IsVisible;
            panel!.Update(true, free, 1f, 1f);
        }

        public static void Shutdown()
        {
            Reset();
            log = null;
            warnedDisplay = false;
        }

        static bool CairnDisplaysWeather()
        {
            try
            {
                Func<bool>? display = null;
                return CruciblePlugin.Services.Capabilities.TryResolve("meadworks.cairn.weather_display", new System.Version(1, 0), out display)
                    && display != null && display();
            }
            catch (Exception error)
            {
                if (!warnedDisplay) log?.Warning("Cairn weather display was unavailable; Tempest keeps its badge: " + error.GetType().Name);
                warnedDisplay = true;
                return false;
            }
        }

        // Grows the badge to fit its text (keeping it centered where it stands), up to MaxWidth; past that the labels
        // shrink their font instead, so the text never runs past the frame or under the event glyph. Both lines are
        // centered; the glyph sits just left of the centered name.
        static void Fit()
        {
            if (panel?.Root == null || label == null || detail == null || meter?.Root == null || icon == null) return;
            float title = label.GetPreferredValues(label.text, 10000f, 0f).x;
            float need = Mathf.Max(title + Glyph + Gap + Pad * 2, detail.GetPreferredValues(detail.text, 10000f, 0f).x + Pad * 2);
            float want = Mathf.Clamp(need, Width, MaxWidth);
            title = Mathf.Min(title, want - Pad * 2 - Glyph - Gap);
            ((RectTransform)icon.transform).anchoredPosition = new Vector2((want - Glyph - Gap - title) * .5f, -10);
            var root = panel.Root;
            float grow = want - root.sizeDelta.x;
            if (Mathf.Abs(grow) < 0.5f) return;
            // Re-center the automatic position for the new width; a position the player placed in the HUD editor still wins.
            float canvas = root.parent is RectTransform parent ? parent.rect.width : Screen.width;
            panel.SetPosition(new Vector2(Mathf.Max(0, canvas * .5f - want * .5f), DefaultTop));
            root.sizeDelta = new Vector2(want, Height);
            meter.Root.sizeDelta = new Vector2(want - Pad * 2, meter.Root.sizeDelta.y);
        }

        static bool Build(Transform root)
        {
            var width = root is RectTransform canvas ? canvas.rect.width : Screen.width;
            var at = new Vector2(Mathf.Max(0, width * .5f - Width * .5f), DefaultTop);
            var made = KitHudPanel.Create(root, "Tempest_Badge", new Vector2(Width, Height), at, global::ExtraPotions.Tempest.Runtime.Plugin.Guid, "Tempest_Badge");
            if (made == null) return false;
            panel = made;
            owner = root;
            var graphic = new GameObject("Tempest_BadgeGlyph", typeof(RectTransform), typeof(Image));
            var graphicRect = (RectTransform)graphic.transform;
            graphicRect.SetParent(made.Content, false);
            graphicRect.anchorMin = graphicRect.anchorMax = graphicRect.pivot = new Vector2(0, 1);
            graphicRect.anchoredPosition = new Vector2(Pad, -10); graphicRect.sizeDelta = new Vector2(Glyph, Glyph);
            icon = graphic.GetComponent<Image>(); icon.preserveAspect = true; icon.raycastTarget = false;
            icon.color = KitUnity.Colour(KitPalette.TabLitText);
            label = Line(made.Content, "Tempest_BadgeText", 19f, Pad + Glyph + Gap, -8, -34);
            detail = Line(made.Content, "Tempest_BadgeTime", 16f, Pad, -36, -56);
            meter = KitMeter.Create(made.Content, "Tempest_BadgeMeter", new Vector2(Pad, -62), new Vector2(Width - Pad * 2, 12));
            if (meter == null)
            {
                Reset();
                return false;
            }
            shownTitle = shownLine = null;
            shownWhole = long.MinValue;
            return true;
        }

        // A centered single line stretched across the badge; very long text shrinks rather than leave the frame.
        static TMP_Text Line(RectTransform parent, string name, float size, float left, float top, float bottom)
        {
            var text = KitCanvas.Text(parent, name, "", size, KitAppearance.Color(KitColorRole.OnPrimary), TextAlignmentOptions.Midline, false);
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-Pad, top);
            text.enableAutoSizing = true; text.fontSizeMin = 12f; text.fontSizeMax = size;
            return text;
        }

        /// <summary>The kit glyph of each kind: snowflake, bolt for both storms, cloud for fog, star for the ash and meteor events.</summary>
        internal static KitGlyph GlyphOf(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Blizzard: return KitGlyph.Snowflake;
                case EventKind.DenseFog: return KitGlyph.Cloud;
                case EventKind.AshStorm:
                case EventKind.MeteorShower: return KitGlyph.Star;
                default: return KitGlyph.Bolt;
            }
        }

        static void PlaySound(EventKind kind, Vector3 at)
        {
            var prefab = FindSound(WarningSound.For(kind)) ?? FindSound(WarningSound.Storm);
            if (prefab == null)
            {
                if (!warnedSound) log?.Warning("The game's warning sound was not found; the warning stays silent.");
                warnedSound = true;
                return;
            }
            try { TempestEffects.Track(UnityEngine.Object.Instantiate(prefab, at, Quaternion.identity), activeOnly: false); }
            catch (Exception error) { log?.Warning("Warning sound failed: " + error.GetType().Name); }
        }

        /// <summary>
        /// The game's own effect prefab by name: a ZNetScene prefab, else an effect of a status effect (sfx_Frost_Start is
        /// the Freezing start sound), else an effect of the weather Thunder component on an environment (sfx_thunder).
        /// </summary>
        static GameObject? FindSound(string name)
        {
            var found = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (found != null) return found;
            if (ObjectDB.instance != null)
                foreach (var effect in ObjectDB.instance.m_StatusEffects)
                    if (effect != null && (found = InEffects(effect.m_startEffects, name)) != null) return found;
            if (EnvMan.instance != null)
                foreach (var env in EnvMan.instance.m_environments)
                {
                    if (env == null) continue;
                    if ((found = InThunder(env.m_envObject, name)) != null) return found;
                    if (env.m_psystems == null) continue;
                    foreach (var system in env.m_psystems)
                        if ((found = InThunder(system, name)) != null) return found;
                }
            return null;
        }

        static GameObject? InThunder(GameObject? root, string name)
        {
            if (root == null) return null;
            foreach (var thunder in root.GetComponentsInChildren<Thunder>(true))
            {
                var found = InEffects(thunder.m_thunderEffect, name);
                if (found != null) return found;
            }
            return null;
        }

        static GameObject? InEffects(EffectList? effects, string name)
        {
            if (effects?.m_effectPrefabs == null) return null;
            foreach (var data in effects.m_effectPrefabs)
                if (data?.m_prefab != null && WarningSound.Matches(data.m_prefab.name, name)) return data.m_prefab;
            return null;
        }

        static void Hide() => panel?.Update(false, false, 1f, 1f);

        static void Reset()
        {
            meter?.Dispose();
            panel?.Dispose();
            meter = null;
            panel = null;
            owner = null;
            label = null;
            detail = null;
            icon = null;
            shownTitle = shownLine = null;
            shownWhole = long.MinValue;
        }
    }
}
