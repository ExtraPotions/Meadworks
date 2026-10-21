using Burrow.Core;
using ExtraPotions.Burrow.Embedded.SyncedConfig;
using ExtraPotions.Burrow.Embedded.YamlConfig;

namespace Burrow.Stow
{
    /// <summary>Entry point of the Stow module: binds its settings, registers its YAML files and language words.</summary>
    public static class StowModule
    {
        public const string Resource = "Burrow.config.Burrow.Stow.yml";

        public static YamlFileSet Files { get; private set; }

        public static void Initialize(SyncedConfiguration synced)
        {
            StowSettings.Bind(synced);
            Files = synced.AddYaml(new YamlFileSet("Burrow.Stow*.yml", "burrow_stow", () => new StowModel(), Apply)
            {
                DefaultContent = SyncedConfiguration.EmbeddedResource(typeof(StowModule).Assembly, Resource),
                EditorLabel = () => Language.Localize(StowWords.Edit),
            });
            StowWords.Register();
        }

        private static void Apply(YamlModel model)
        {
            StowModel rules = (StowModel)model;
            StowRules.Apply(rules);
            Plugin.Log.LogInfo($"Stow rules applied: {rules.Containers.Count} container rules.");
        }
    }
}
