// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Setting names and ranges follow design section 4; the ShipConfig and GrindstoneSkills names they
// mirror are recorded in the design's Source column (names only; descriptions are Keel's own).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Keel.Catalog;
using static ExtraPotions.Keel.Config.SettingScope;

namespace ExtraPotions.Keel.Config
{
    /// <summary>The catalog settings of one Keel ship type (design 4.3), in the section named after the ship. All ServerAuthoritative.</summary>
    public sealed class CatalogSettings
    {
        internal CatalogSettings(ShipType type)
        {
            Type = type;
            var section = type.Name;
            Enabled = KeelSetting.Toggle(section, "Enabled", true, ServerAuthoritative,
                "Off hides the " + type.Name + " from the hammer. Ships already built stay.");
            Recipe = KeelSetting.Text(section, "Recipe", type.Recipe, ServerAuthoritative,
                "Up to five Prefab:Amount pairs separated by commas. An invalid recipe or an unknown item keeps the default and logs why.", v => (v ?? "").Trim());
            Width = Shape(section, "Width", type.Width, "Hull width relative to the vanilla " + HullName(type) + ".");
            Length = Shape(section, "Length", type.Length, "Bow-to-stern length relative to the vanilla " + HullName(type) + ".");
            Scale = Shape(section, "Scale", type.Scale, "Size of the whole ship in all three dimensions; Width and Length multiply on top.");
            CargoColumns = KeelSetting.Integer(section, "Cargo Columns", type.CargoColumns, 1, 12, ServerAuthoritative,
                "Columns of the ship's hold. A smaller hold never deletes items: a non-empty hold is not shrunk.");
            CargoRows = KeelSetting.Integer(section, "Cargo Rows", type.CargoRows, 1, 10, ServerAuthoritative,
                "Number of storage rows in this ship's hold.");
            SailColor = KeelSetting.Text(section, "Sail Color", type.SailColor, ServerAuthoritative,
                "Hex RRGGBB multiplied into the sail; FFFFFF leaves it as it is. Loaded ships change at once.", v => HexColor.Normalize(v, type.SailColor, false));
            HullColor = KeelSetting.Text(section, "Hull Color", type.HullColor, ServerAuthoritative,
                "Hex RRGGBB paint multiplied into the hull planks (not the mast or rudder); empty keeps the vanilla planks.", v => HexColor.Normalize(v, type.HullColor, true));
            HullStripes = KeelSetting.Integer(section, "Hull Stripes", type.HullStripes, 0, 8, ServerAuthoritative,
                "Painted bands on the hull; 0 paints it solid.");
            if (type.HasPen)
                PenCapacity = KeelSetting.Integer(section, "Pen Capacity", type.PenCapacity, 0, 40, ServerAuthoritative,
                    "Animals the deck pen is meant for (about one per 2 square meters); Shepherd uses it as the herd limit. 0: no limit.");
        }

        public ShipType Type { get; }
        public KeelSetting<bool> Enabled { get; }
        public KeelSetting<string> Recipe { get; }
        public KeelSetting<float> Width { get; }
        public KeelSetting<float> Length { get; }
        public KeelSetting<float> Scale { get; }
        public KeelSetting<int> CargoColumns { get; }
        public KeelSetting<int> CargoRows { get; }
        public KeelSetting<string> SailColor { get; }
        public KeelSetting<string> HullColor { get; }
        public KeelSetting<int> HullStripes { get; }

        /// <summary>Only the Byrdings have a deck pen.</summary>
        public KeelSetting<int>? PenCapacity { get; }

        public IEnumerable<KeelSetting> Entries()
        {
            yield return Enabled;
            yield return Recipe;
            yield return Width;
            yield return Length;
            yield return Scale;
            yield return CargoColumns;
            yield return CargoRows;
            yield return SailColor;
            yield return HullColor;
            yield return HullStripes;
            if (PenCapacity != null) yield return PenCapacity;
        }

        private static string HullName(ShipType type) => type.BaseHull == ShipCatalog.Karve ? "Karve" : "Longship";

        private static KeelSetting<float> Shape(string section, string key, float value, string description) =>
            KeelSetting.Number(section, key, value, 0.5f, 2f, ServerAuthoritative, description);
    }

    /// <summary>How the ship panel shows speed (design 4.5).</summary>
    public enum SpeedUnit { MetersPerSecond, Knots, KilometersPerHour }

    /// <summary>
    /// Every fixed Keel setting from design section 4, in file order. Per-ship tuning entries (4.2) are bound per
    /// discovered prefab from the ship field table; per-ship-type catalog entries (4.3) come from the catalog.
    /// The plugin binds exactly <see cref="All"/>; the portable tests compare it with the spec tables.
    /// </summary>
    public static class KeelSettings
    {
        public static class Multipliers
        {
            public const string Section = "Multipliers";

            public static readonly KeelSetting<float> Health = Factor("Health Multiplier", 0.1f, "the health of every ship");
            public static readonly KeelSetting<float> SailForce = Factor("Sail Force Multiplier", 0.1f, "the sail force of every ship");
            public static readonly KeelSetting<float> PaddleForce = Factor("Paddle Force Multiplier", 0.1f, "the paddle force of every ship");
            public static readonly KeelSetting<float> Turning = Factor("Turning Multiplier", 0.1f, "the turning force of every ship, under sail and at the oars");
            public static readonly KeelSetting<float> DamageTaken = Factor("Damage Taken Multiplier", 0f, "the damage every ship takes; 0 means no damage");
            public static readonly KeelSetting<float> BuildCost = Factor("Build Cost Multiplier", 0.1f,
                "the materials every ship costs (each material rounded, at least 1; free ships come from Timber's Free Build cheat)");

            private static KeelSetting<float> Factor(string key, float min, string subject) =>
                KeelSetting.Number(Section, key, 1f, min, 10f, ServerAuthoritative, "Multiplier on " + subject + ", on top of the per-ship value. 1 is vanilla.");
        }

        public static class Pens
        {
            public const string Section = "Pens";

            public static readonly KeelSetting<float> ContainmentSpeed = KeelSetting.Number(Section, "Containment Speed", 0.5f, 0f, 5f, ServerAuthoritative,
                "Ship speed in m/s above which the invisible lip along a deck pen's rails keeps jostled animals aboard. At rest the lip is gone so animals can leave.");
            public static readonly KeelSetting<bool> GatesOpenAtRestOnly = KeelSetting.Toggle(Section, "Gates Open At Rest Only", false, ServerAuthoritative,
                "On: a pen gate refuses to open while the ship moves faster than Containment Speed. Closing always works.");
        }

        public static class Sailing
        {
            public const string Section = "Sailing";

            public static readonly KeelSetting<bool> Enabled = KeelSetting.Toggle(Section, "Sailing Enabled", true, ServerAuthoritative,
                "Every Sailing perk, Wind Call, Lookout and Sailing experience. Off keeps everyone's Sailing level.");
            public static readonly KeelSetting<float> ShipHealthAt100 = KeelSetting.Number(Section, "Ship Health At 100", 50f, 0f, 500f, ServerAuthoritative,
                "Percent more health for a ship built by a level 100 sailor, fixed by the builder's level when the ship is placed.");
            public static readonly KeelSetting<float> ShipSpeedAt100 = KeelSetting.Number(Section, "Ship Speed At 100", 20f, 0f, 200f, ServerAuthoritative,
                "Percent more top speed, under sail and at the oars, for a ship a level 100 sailor steers.");
            public static readonly KeelSetting<float> ExplorationRadiusAt100 = KeelSetting.Number(Section, "Exploration Radius At 100", 100f, 0f, 500f, ServerAuthoritative,
                "Percent larger map exploration radius while a level 100 sailor is aboard a ship (the game's radius is 100 m).");
            public static readonly KeelSetting<float> HelmExperiencePerKilometre = KeelSetting.Number(Section, "Helm Experience Per Kilometer", 40f, 0f, 1000f, ServerAuthoritative,
                "Sailing experience per kilometer a ship travels while you steer it. 0 turns it off.");
            public static readonly KeelSetting<float> CrewExperienceShare = KeelSetting.Number(Section, "Crew Experience Share", 25f, 0f, 100f, ServerAuthoritative,
                "Percent of the helmsman's experience everyone else aboard earns while someone steers. 0 turns it off.");
        }

        public static class WindCall
        {
            public const string Section = "Wind Call";

            public static readonly KeelSetting<float> Level = KeelSetting.Number(Section, "Wind Call Level", 25f, 0f, 101f, ServerAuthoritative,
                "Sailing level that unlocks Wind Call. 0 gives it to everyone; above 100 turns it off.");
            public static readonly KeelSetting<float> Duration = KeelSetting.Number(Section, "Wind Call Duration", 60f, 5f, 600f, ServerAuthoritative,
                "Seconds the called wind blows for everyone aboard that ship.");
            public static readonly KeelSetting<float> Cooldown = KeelSetting.Number(Section, "Wind Call Cooldown", 180f, 0f, 3600f, ServerAuthoritative,
                "Seconds before the same player can call the wind again.");
            public static readonly KeelSetting<string> Key = Shortcut(Section, "Wind Call Key", "H",
                "Turns the wind to blow the way you look while you steer a ship. Your own key.");
            public static readonly KeelSetting<string> Pad = Shortcut(Section, "Gamepad Wind Call", "",
                "Gamepad chord for Wind Call (Crucible pad buttons joined by +). Empty: none, so nothing collides with steering.");
        }

        public static class Lookout
        {
            public const string Section = "Lookout";

            public static readonly KeelSetting<float> Level = KeelSetting.Number(Section, "Lookout Level", 50f, 0f, 101f, ServerAuthoritative,
                "Sailing level that unlocks Lookout. 0 gives it to everyone; above 100 turns it off.");
            public static readonly KeelSetting<float> Radius = KeelSetting.Number(Section, "Lookout Radius", 100f, 10f, 300f, ServerAuthoritative,
                "How far from the ship the Lookout pulse reaches, in meters.");
            public static readonly KeelSetting<float> Duration = KeelSetting.Number(Section, "Lookout Duration", 30f, 1f, 300f, ServerAuthoritative,
                "Seconds the enemies the pulse reached keep their name tags for everyone aboard.");
            public static readonly KeelSetting<float> Cooldown = KeelSetting.Number(Section, "Lookout Cooldown", 60f, 0f, 3600f, ServerAuthoritative,
                "Seconds before the same player can send the next pulse.");
            public static readonly KeelSetting<string> Key = Shortcut(Section, "Lookout Key", "O",
                "Sends the Lookout pulse while you are aboard a ship. Your own key.");
            public static readonly KeelSetting<string> Pad = Shortcut(Section, "Gamepad Lookout", "",
                "Gamepad chord for Lookout (Crucible pad buttons joined by +). Empty: none.");
        }

        public static class Display
        {
            public const string Section = "Display";

            public static readonly KeelSetting<bool> ShipPanel = KeelSetting.Toggle(Section, "Ship Panel", true, Local,
                "A panel under the minimap while you are aboard: speed, speed against vanilla, exploration radius and your Sailing abilities.");
            public static readonly KeelSetting<SpeedUnit> SpeedUnit = KeelSetting.Choice(Section, "Speed Unit", Config.SpeedUnit.KilometersPerHour, Local,
                "How the ship panel shows speed: meters per second (MetersPerSecond), knots (Knots) or kilometers per hour (KilometersPerHour).");
            public static readonly KeelSetting<float> BoatCameraMaxDistance = KeelSetting.Number(Section, "Boat Camera Max Distance", 16f, 8f, 40f, Local,
                "How far the camera may zoom out while you steer, in meters. 16 is the game's own limit; try 24 for the biggest ships.");
            public static readonly KeelSetting<float> SailOpacity = KeelSetting.Number(Section, "Sail Opacity", 1f, 0f, 1f, Local,
                "Sail opacity of the ship you steer: 1 as the game draws it, lower is see-through, 0 hides it. Other ships are unchanged.");
            public static readonly KeelSetting<bool> SailOpacityBothSides = KeelSetting.Toggle(Section, "Sail Opacity Both Sides", false, Local,
                "Off: only the side of the sail you see from the rudder is see-through. On: both sides.");
            public static readonly KeelSetting<bool> SailOpacityOnDeck = KeelSetting.Toggle(Section, "Sail Opacity On Deck", false, Local,
                "Sail Opacity always applies at the rudder. On: also anywhere on the deck.");
            public static readonly KeelSetting<bool> NameVanillaShips = KeelSetting.Toggle(Section, "Name Vanilla Ships", true, Local,
                "Vanilla rudders and holds name their ship too: \"Use rudder (Karve)\", \"Karve Storage\".");
        }

        public static class Cheats
        {
            public const string Section = "Cheats";

            public static readonly KeelSetting<bool> AshlandsImmuneShips = KeelSetting.Toggle(Section, "Ashlands Immune Ships", false, Cheat,
                "Cheat. The ships on Ashlands Immune List sail the burning Ashlands ocean unharmed. This removes the progression gate that makes the Drakkar necessary, with the ocean effects and world key it triggers.",
                safe: false);
            public static readonly KeelSetting<string> AshlandsImmuneList = KeelSetting.Text(Section, "Ashlands Immune List", "*", ServerAuthoritative,
                "Ship prefab names separated by commas, or * for every ship. Acts only while Ashlands Immune Ships is on.", ShipList.Normalize);
            public static readonly KeelSetting<bool> InvulnerableShips = KeelSetting.Toggle(Section, "Invulnerable Ships", false, Cheat,
                "Cheat. The ships on Invulnerable List take no damage of any kind: hits, collisions, weather or the Ashlands ocean.",
                safe: false);
            public static readonly KeelSetting<string> InvulnerableList = KeelSetting.Text(Section, "Invulnerable List", "*", ServerAuthoritative,
                "Ship prefab names separated by commas, or * for every ship. Acts only while Invulnerable Ships is on.", ShipList.Normalize);
            public static readonly KeelSetting<bool> Tailwind = KeelSetting.Toggle(Section, "Tailwind", false, Cheat,
                "Cheat. The wind always blows from behind the ship you steer, for you alone: your sail fills whichever way you head. Other players' wind is unchanged.",
                safe: false);
            public static readonly KeelSetting<bool> FastSailing = KeelSetting.Toggle(Section, "Fast Sailing", false, Cheat,
                "Cheat. The ship you steer sails at up to twice its top speed. Acts on the machine that moves the ship, which is usually the helmsman's.",
                safe: false);
        }

        private static KeelSetting<string> Shortcut(string section, string key, string defaultValue, string description) =>
            KeelSetting.Text(section, key, defaultValue, Local, description, v => (v ?? "").Trim());

        private static readonly Lazy<KeelSetting[]> general = new Lazy<KeelSetting[]>(() => new KeelSetting[]
        {
            Multipliers.Health, Multipliers.SailForce, Multipliers.PaddleForce, Multipliers.Turning, Multipliers.DamageTaken, Multipliers.BuildCost,
            Pens.ContainmentSpeed, Pens.GatesOpenAtRestOnly,
            Sailing.Enabled, Sailing.ShipHealthAt100, Sailing.ShipSpeedAt100, Sailing.ExplorationRadiusAt100, Sailing.HelmExperiencePerKilometre, Sailing.CrewExperienceShare,
            WindCall.Level, WindCall.Duration, WindCall.Cooldown, WindCall.Key, WindCall.Pad,
            Lookout.Level, Lookout.Radius, Lookout.Duration, Lookout.Cooldown, Lookout.Key, Lookout.Pad,
            Display.ShipPanel, Display.SpeedUnit, Display.BoatCameraMaxDistance,
            Display.SailOpacity, Display.SailOpacityBothSides, Display.SailOpacityOnDeck, Display.NameVanillaShips,
            Cheats.AshlandsImmuneShips, Cheats.AshlandsImmuneList, Cheats.InvulnerableShips, Cheats.InvulnerableList,
            Cheats.Tailwind, Cheats.FastSailing,
        });

        private static readonly Lazy<CatalogSettings[]> catalog = new Lazy<CatalogSettings[]>(
            () => ShipCatalog.All.Select(t => new CatalogSettings(t)).ToArray());

        private static readonly Lazy<KeelSetting[]> all = new Lazy<KeelSetting[]>(
            () => general.Value.Concat(catalog.Value.SelectMany(c => c.Entries())).ToArray());

        /// <summary>The catalog settings of one ship type (design 4.3).</summary>
        public static CatalogSettings Catalog(KeelShip ship) => catalog.Value[(int)ship];

        private static readonly Lazy<Dictionary<string, KeelSetting>> byId = new Lazy<Dictionary<string, KeelSetting>>(
            () => all.Value.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase));

        /// <summary>The fixed settings of design 4.1, 4.4, 4.5 and 4.6 and the pen settings, in file order.</summary>
        public static IReadOnlyList<KeelSetting> General => general.Value;

        /// <summary>Every fixed setting the plugin binds: the general settings, then each ship type's catalog section.</summary>
        public static IReadOnlyList<KeelSetting> All => all.Value;

        public static KeelSetting? Find(string? id) => id != null && byId.Value.TryGetValue(id.Trim(), out var setting) ? setting : null;

        /// <summary>The local keyboard shortcut settings (bound as <c>KeyboardShortcut</c> by the plugin).</summary>
        public static IReadOnlyList<KeelSetting<string>> KeyboardShortcuts { get; } = Array.AsReadOnly(new[] { WindCall.Key, Lookout.Key });
    }
}
