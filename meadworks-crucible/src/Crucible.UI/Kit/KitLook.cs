using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// The kit's look settings, each player's own (section "Look" of Crucible.UI's config), with PackPanel 0.12.0's
    /// defaults, ranges and wording: <see cref="Styled"/> is PackPanel's Brown Style switch, then the Timber theme and its
    /// border, jaggedness and night shade. <see cref="GamePanels"/> decides whether the game's own wood panels are dressed
    /// too: PackPanel always does with Timber on; in Meadworks the product that owns the inventory asks for it
    /// (<see cref="RequestGamePanels"/>), so a suite without it leaves the game's menus alone.
    /// </summary>
    public static class KitLook
    {
        public const string Section = "Look";

        private static readonly HashSet<object> gamePanelRequests = new HashSet<object>();

        public static ConfigEntry<bool>? Styled { get; private set; }
        public static ConfigEntry<KitThemeKind>? PanelTheme { get; private set; }
        public static ConfigEntry<float>? FrameWidth { get; private set; }
        public static ConfigEntry<float>? FrameJaggedness { get; private set; }
        public static ConfigEntry<float>? NightShadeSetting { get; private set; }
        public static ConfigEntry<KitGamePanels>? GamePanels { get; private set; }
        public static ConfigEntry<bool>? MapFrames { get; private set; }
        public static ConfigEntry<bool>? ScreenLayout { get; private set; }

        /// <summary>The inventory screen follows the Crucible region layout (default on; off leaves the game's own placement of the crafting panel and header).</summary>
        public static bool ScreenLayoutOn => ScreenLayout?.Value ?? true;

        /// <summary>Meadworks frames show on the game's minimap and large map (default off since 2026-10-12; off leaves the game's maps untouched).</summary>
        public static bool MapFramesOn => MapFrames?.Value ?? KitMapFrameRules.DefaultOn;

        /// <summary>Raised when any look setting or game-panel request changes (themes rescan, frames rebuild).</summary>
        public static event Action? Changed;

        /// <summary>The kit's brown or Timber look is on (PackPanel's <c>GridSkin.On</c>).</summary>
        public static bool On => Styled?.Value ?? true;

        /// <summary>Timber shows (PackPanel's <c>SkinArt.Timber</c>).</summary>
        public static bool Timber => KitThemeRules.Timber(On, PanelTheme?.Value ?? KitThemeKind.Timber);

        public static float Width => FrameWidth?.Value ?? TimberGeometry.DefaultWidth;

        public static float Jaggedness => FrameJaggedness?.Value ?? TimberGeometry.DefaultJaggedness;

        public static float Night => NightShadeSetting?.Value ?? NightShadeCurve.Default;

        /// <summary>The game's own wood panels are dressed as well (only ever with Timber showing).</summary>
        public static bool ThemeGamePanels => KitThemeRules.ThemeGamePanels(GamePanels?.Value ?? KitGamePanels.Auto, gamePanelRequests.Count > 0);

        /// <summary>A product (the inventory owner) asks for the game's wood panels in the kit's look.</summary>
        public static void RequestGamePanels(object owner)
        {
            if (owner != null && gamePanelRequests.Add(owner))
                Changed?.Invoke();
        }

        public static void ReleaseGamePanels(object owner)
        {
            if (owner != null && gamePanelRequests.Remove(owner))
                Changed?.Invoke();
        }

        internal static void Bind(ConfigFile config)
        {
            Styled = MeadworksConfig.Bind(config, Section, "Styled Panels", true,
                "The brown framed look of MeadWorks panels, their cells and buttons. Off: the game's wood. Crucible.UI owns this local player setting.");
            PanelTheme = MeadworksConfig.Bind(config, Section, "Panel Theme", KitThemeKind.Timber,
                "Timber: shared warm wallpaper with softly chopped, beveled wood edges. Brown: the original brown artwork. Styled Panels off restores the game's panels. Cosmetic, local to this player. Crucible.UI owns this local player setting.");
            FrameWidth = MeadworksConfig.Bind(config, Section, "Timber Border Width", TimberGeometry.DefaultWidth,
                new ConfigDescription("How thick the wooden edge of MeadWorks panels is. Crucible.UI owns this local player setting.",
                    new AcceptableValueRange<float>(TimberGeometry.MinWidth, TimberGeometry.MaxWidth), new ConfigurationManagerAttributes()));
            FrameJaggedness = MeadworksConfig.Bind(config, Section, "Timber Border Jaggedness", TimberGeometry.DefaultJaggedness,
                new ConfigDescription("How roughly the wooden edge of MeadWorks panels is cut. Crucible.UI owns this local player setting.",
                    new AcceptableValueRange<float>(TimberGeometry.MinJaggedness, TimberGeometry.MaxJaggedness), new ConfigurationManagerAttributes()));
            NightShadeSetting = MeadworksConfig.Bind(config, Section, "Night Shade", NightShadeCurve.Default,
                new ConfigDescription("How dark MeadWorks panels get at night, as the game's own wooden panels darken with the light around you (dark weather shades them too). 1 keeps them as bright as day; 0.65 at midnight in clear weather. Gray only, never the weather's color. Cells never change. Crucible.UI owns this local player setting.",
                    new AcceptableValueRange<float>(NightShadeCurve.Min, NightShadeCurve.Max)));
            GamePanels = MeadworksConfig.Bind(config, Section, "Theme Game Panels", KitGamePanels.Auto,
                "Dress the game's own wood panels (crafting, character dialogs, settings) in the Timber look. Auto: when a MeadWorks product that lays out the inventory asks for it. Always or Never override. Crucible.UI owns this local player setting.");
            MapFrames = MeadworksConfig.Bind(config, Section, KitMapFrameRules.ConfigName, KitMapFrameRules.DefaultOn,
                "Draw MeadWorks wood-and-bronze frames over the game's minimap and large map. The map itself (terrain, pins, fog) is the game's own and stays untouched. Off: the game's maps exactly as they were. Cosmetic, local to this player. Crucible.UI owns this local player setting.");
            ScreenLayout = MeadworksConfig.Bind(config, Section, "Meadworks Screen Layout", true,
                "Lay the inventory screen out in named regions: the grid at the top left, the equipment panel beside it, the character header and the crafting panel in a column at the right, and a dock for product panels under the equipment panel. Off: the game's own placement. Local to this player. Crucible.UI owns this local player setting.");
            Styled.SettingChanged += (sender, args) => Changed?.Invoke();
            PanelTheme.SettingChanged += (sender, args) => Changed?.Invoke();
            GamePanels.SettingChanged += (sender, args) => Changed?.Invoke();
            FrameWidth.SettingChanged += (sender, args) => TimberFrame.Invalidate();
            FrameJaggedness.SettingChanged += (sender, args) => TimberFrame.Invalidate();
        }

        // Read by name by configuration managers (Almanac included): advanced settings are hidden by default.
        sealed class ConfigurationManagerAttributes { public bool? IsAdvanced = true; }
    }
}
