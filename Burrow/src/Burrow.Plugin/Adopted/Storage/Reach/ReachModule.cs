using Burrow.Core;
using ExtraPotions.Burrow.Embedded.SyncedConfig;
using ExtraPotions.Burrow.Embedded.YamlConfig;

namespace Burrow.Reach
{
    /// <summary>Entry point of the Reach module: binds its settings, registers its YAML files and language words.</summary>
    public static class ReachModule
    {
        public const string Resource = "Burrow.config.Burrow.Reach.yml";

        public static YamlFileSet Files { get; private set; }

        public static void Initialize(SyncedConfiguration synced)
        {
            ReachSettings.Initialize(synced);
            Files = synced.AddYaml(new YamlFileSet("Burrow.Reach*.yml", "burrow_reach", () => new ReachModel(), ReachRules.Apply)
            {
                DefaultContent = SyncedConfiguration.EmbeddedResource(typeof(ReachModule).Assembly, Resource),
                EditorLabel = () => "Edit reach rules",
            });
            Words();
        }

        private static void Words()
        {
            Language.Add("burrow_fromstorage", "From storage");
            Language.Add("burrow_reach", "Burrow reach");
            Language.Add("burrow_on", "on");
            Language.Add("burrow_off", "off");
            Language.Add("burrow_pulled", "Pulled from storage:");
            Language.Add("burrow_nothingtopull", "Nothing to pull from storage");
            Language.Add("burrow_nofit", "The materials from storage do not fit in the inventory");
        }
    }
}
