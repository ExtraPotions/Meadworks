using ExtraPotions.Burrow.Embedded.PatchGuard;
using ExtraPotions.Burrow.Embedded.SyncedConfig;
using ExtraPotions.Burrow.Embedded.YamlConfig;

namespace Burrow.Capacity
{
    /// <summary>
    /// The Capacity module's second YAML set, Burrow.Stations*.yml: how much each station holds. Synced from the
    /// server, hot reloaded, editable in game, and gated by Capacity's Enabled like the container sizes.
    /// </summary>
    public static class StationsFile
    {
        public const string DefaultResource = "Burrow.config.Burrow.Stations.yml";

        /// <summary>The Burrow.Stations*.yml set; its Current model is the last one that parsed without errors.</summary>
        public static YamlFileSet Set { get; private set; }

        public static void Initialize(SyncedConfiguration synced)
        {
            Set = synced.AddYaml(new YamlFileSet("Burrow.Stations*.yml", "burrow_stations", () => new StationsModel(), Apply)
            {
                DefaultContent = SyncedConfiguration.EmbeddedResource(typeof(StationsFile).Assembly, DefaultResource),
                EditorLabel = () => "Edit station capacities",
            });
            CapacitySettings.Enabled.SettingChanged += Guard.Wrap("station capacity switch", (_, _) => StationCapacities.ApplyAll());
        }

        private static void Apply(YamlModel model)
        {
            StationCapacities.ApplyAll();
            Plugin.Log.LogInfo($"Burrow: station capacities applied, {((StationsModel)model).Caps.Count} prefabs.");
        }
    }
}
