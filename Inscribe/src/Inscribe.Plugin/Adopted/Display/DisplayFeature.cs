// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// Entry point of the Display area, called once from plugin Awake after the rules, settings and words are loaded.
    /// Owns: rarity-colored names, the tooltip affix block, display preferences and the ground glow (display.md).
    /// Harmony patches of this area are ordinary [HarmonyPatch] classes in this folder; the plugin's PatchAll finds them.
    /// <para>
    /// Multiplayer: everything here is drawn on the viewing client from the item's replicated custom data and the
    /// synced rarity definitions, plus this player's own unsynced preferences. No netcode, no ZDO key; on a dedicated
    /// server the glow manager is never created and the name and tooltip hooks are simply never called.
    /// </para>
    /// </summary>
    public static class DisplayFeature
    {
        public static void Init()
        {
            RarityPalette.Rebuild();
            ActiveRules.RulesChanged += OnRulesChanged;
            ItemStateCache.Written += OnWritten;
            Words.Changed += DisplayCache.Invalidate;   // a language setup or a data-driven word: built blocks are stale
            WatchDisplaySettings();
            GlowManager.Create();
            WatchGlowSettings();
        }

        internal static void Shutdown()
        {
            ActiveRules.RulesChanged -= OnRulesChanged;
            ItemStateCache.Written -= OnWritten;
            Words.Changed -= DisplayCache.Invalidate;
            ModSettings.ColoredItemNames.SettingChanged -= OnDisplaySettingChanged;
            ModSettings.TooltipDetailLevel.SettingChanged -= OnDisplaySettingChanged;
            ModSettings.ShowDormantAffixes.SettingChanged -= OnDisplaySettingChanged;
            ModSettings.GroundGlow.SettingChanged -= OnGlowSettingChanged;
            ModSettings.GlowIntensity.SettingChanged -= OnGlowSettingChanged;
            ModSettings.GlowRange.SettingChanged -= OnGlowSettingChanged;
            ModSettings.GlowMaxLights.SettingChanged -= OnGlowSettingChanged;
            ModSettings.GlowRefreshSeconds.SettingChanged -= OnGlowSettingChanged;
            ModSettings.GlowStones.SettingChanged -= OnGlowSettingChanged;
            ModSettings.LootBeam.SettingChanged -= OnGlowSettingChanged;
            GlowManager.Instance?.Shutdown();
            Backdrops.IconBackdrop.Shutdown();
            Tooltips.TooltipScroll.Shutdown();
        }

        private static void OnRulesChanged()
        {
            RarityPalette.Rebuild();
            DisplayCache.Invalidate();
            GlowManager.Instance?.RequestTick();
        }

        private static void OnWritten(ItemDrop.ItemData item)
        {
            DisplayCache.Invalidate();
            GlowManager.Instance?.RequestTick();
        }

        // Detail and dormant switches are part of the cache key; the name switch is read per draw. Invalidating on
        // any display change keeps a hot-reloaded .cfg from showing a stale block.
        private static void WatchDisplaySettings()
        {
            ModSettings.ColoredItemNames.SettingChanged += OnDisplaySettingChanged;
            ModSettings.TooltipDetailLevel.SettingChanged += OnDisplaySettingChanged;
            ModSettings.ShowDormantAffixes.SettingChanged += OnDisplaySettingChanged;
        }

        private static void OnDisplaySettingChanged(object sender, EventArgs e)
        {
            DisplayCache.Invalidate();
            if (!Runtime.InscribeModules.Active(Integration.InscribeModule.Display))
                Backdrops.IconBackdrop.HideAll();
            else
                Backdrops.IconBackdrop.RefreshAll();
        }

        /// <summary>Glow preferences apply live: the next tick (moved up to now) reads them all.</summary>
        private static void WatchGlowSettings()
        {
            ModSettings.GroundGlow.SettingChanged += OnGlowSettingChanged;
            ModSettings.GlowIntensity.SettingChanged += OnGlowSettingChanged;
            ModSettings.GlowRange.SettingChanged += OnGlowSettingChanged;
            ModSettings.GlowMaxLights.SettingChanged += OnGlowSettingChanged;
            ModSettings.GlowRefreshSeconds.SettingChanged += OnGlowSettingChanged;
            ModSettings.GlowStones.SettingChanged += OnGlowSettingChanged;
            ModSettings.LootBeam.SettingChanged += OnGlowSettingChanged;
        }

        private static void OnGlowSettingChanged(object sender, EventArgs e) => GlowManager.Instance?.RequestTick();
    }
}
