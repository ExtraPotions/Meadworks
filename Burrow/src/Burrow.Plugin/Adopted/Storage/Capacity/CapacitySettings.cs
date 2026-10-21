using BepInEx.Configuration;
using ExtraPotions.Burrow.Embedded.SyncedConfig;

namespace Burrow.Capacity
{
    /// <summary>Section "5. Capacity": the switch is synced, the hover display is per player.</summary>
    public static class CapacitySettings
    {
        public const string Section = "5. Capacity";

        public static ConfigEntry<bool> Enabled { get; private set; }
        public static ConfigEntry<bool> HoverContents { get; private set; }
        public static ConfigEntry<int> HoverLines { get; private set; }
        public static ConfigEntry<HoverFill> HoverFill { get; private set; }
        public static ConfigEntry<int> StationMinimum { get; private set; }

        public static void Bind(SyncedConfiguration synced)
        {
            Enabled = synced.Bind(Section, "Enabled", true,
                "Master switch of the Capacity module: container sizes from Burrow.Containers.yml, station capacities from Burrow.Stations.yml and the hover contents. Off restores the vanilla sizes where no item is in the way and the vanilla station capacities.");
            StationMinimum = synced.Bind(Section, "Station Minimum Capacity", 50,
                "Smelters, kilns, furnaces, the spinning wheel, windmill, eitr refinery and other processing stations, including stations from other mods, hold at least this many items and this much fuel. A station with its own line in Burrow.Stations.yml uses that line instead (set exact values there); a station the game already gives more keeps its own value, and a slot the station does not use stays unused. 0 keeps the game's capacities. Host setting, applies to everyone. Default: 50.",
                acceptableValues: new AcceptableValueRange<int>(0, 1000));
            StationMinimum.SettingChanged += (_, _) => StationCapacities.ApplyAll();
            HoverContents = synced.Bind(Section, "Hover Contents", false,
                "Hovering a container adds its fill and contents to the hover text: the Hover Fill line, up to Hover Lines lines of 'name x count', then 'and n more'. Only for containers you could open, and only while Chest Preview is off: the preview panel shows the contents otherwise. Off by default: the hover shows the action prompt and the preview panel only.", synced: false);
            HoverLines = synced.Bind(Section, "Hover Lines", 8,
                "How many content lines the hover text shows.", synced: false, acceptableValues: new AcceptableValueRange<int>(0, 40));
            HoverFill = synced.Bind(Section, "Hover Fill", Capacity.HoverFill.Fraction,
                "The hover text's first extra line: Fraction '12 / 24 slots', Percent '50% full', Off.", synced: false);
        }
    }
}
