using Burrow.Core;
using ExtraPotions.Burrow.Embedded.PatchGuard;
using ExtraPotions.Burrow.Embedded.SyncedConfig;
using ExtraPotions.Burrow.Embedded.YamlConfig;

namespace Burrow.Capacity
{
    /// <summary>Entry point of the Capacity module: binds its settings, registers its YAML files and language words.</summary>
    public static class CapacityModule
    {
        public const string DefaultResource = "Burrow.config.Burrow.Containers.yml";

        public static SyncedConfiguration Synced { get; private set; }

        /// <summary>The Burrow.Containers*.yml set; its Current model is the last one that parsed without errors.</summary>
        public static YamlFileSet Set { get; private set; }

        public static void Initialize(SyncedConfiguration synced)
        {
            Synced = synced;
            CapacitySettings.Bind(synced);
            Set = synced.AddYaml(new YamlFileSet("Burrow.Containers*.yml", "burrow_containers", () => new ContainersModel(), ApplyYaml)
            {
                DefaultContent = SyncedConfiguration.EmbeddedResource(typeof(CapacityModule).Assembly, DefaultResource),
                EditorLabel = () => "Edit container sizes",
            });
            CapacitySettings.Enabled.SettingChanged += Guard.Wrap("capacity switch", (_, _) => ContainerSizes.ApplyAll());
            StationsFile.Initialize(synced);
            Language.Add("burrow_slots", "slots");
            Language.Add("burrow_full", "full");
            Language.Add("burrow_and", "and");
            Language.Add("burrow_more", "more");
        }

        private static void ApplyYaml(YamlModel model)
        {
            ContainerSizes.ApplyAll();
            Plugin.Log.LogInfo($"Burrow: container sizes applied, {((ContainersModel)model).Specs.Count} prefabs listed, chests {(((ContainersModel)model).Chests?.ToString() ?? "unchanged")}.");
        }
    }
}
