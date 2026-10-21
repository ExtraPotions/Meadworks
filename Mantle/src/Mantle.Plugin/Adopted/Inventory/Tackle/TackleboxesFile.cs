using Mantle.Crafting;
using ExtraPotions.Mantle.Embedded.SyncedConfig;
using ExtraPotions.Mantle.Embedded.YamlConfig;

namespace Mantle.Tackle
{
    /// <summary>
    /// Mantle.Tackleboxes*.yml: what each tacklebox gives and costs. Synced from the server (Charter article
    /// <c>mantle_tackleboxes</c>), hot reloaded and editable in game like the other YAML files. Applying it sets every
    /// kind's stats (the file over the built-in defaults), makes the recipes and the words follow, and the next frame the
    /// box's cells (<see cref="TackleboxWear"/>).
    /// </summary>
    public static class TackleboxesFile
    {
        public const string DefaultResource = "Mantle.config.Mantle.Tackleboxes.yml";

        public static YamlFileSet Set { get; private set; }

        public static void Initialize(SyncedConfiguration synced)
        {
            Set = synced.AddYaml(new YamlFileSet("Mantle.Tackleboxes*.yml", "mantle_tackleboxes", () => new TackleboxesModel(), Apply)
            {
                DefaultContent = SyncedConfiguration.EmbeddedResource(typeof(TackleboxesFile).Assembly, DefaultResource),
                EditorLabel = () => "Edit tackleboxes",
            });
        }

        private static void Apply(YamlModel model)
        {
            TackleboxesModel read = (TackleboxesModel)model;
            foreach (TackleboxKind kind in TackleboxCatalog.All)
                kind.Stats = read.Stats.TryGetValue(kind.Id, out TackleboxStats stats) ? stats : kind.Defaults;
            CraftRecipes.RefreshAll();
            TackleboxWords.DescribeAll();
            Plugin.Log.LogInfo($"Mantle: tackleboxes applied, {read.Stats.Count} set in the file.");
        }
    }
}
