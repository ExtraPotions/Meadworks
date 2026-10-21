using System;
using System.Collections.Generic;
using Mantle.Crafting;
using ExtraPotions.Mantle.Embedded.YamlConfig;

namespace Mantle.Tackle
{
    /// <summary>
    /// The parsed Mantle.Tackleboxes*.yml files: under <c>tackleboxes:</c>, one entry per tacklebox prefab
    /// (<see cref="TackleboxCatalog"/>), each key optional: <c>station</c>, <c>level</c> and <c>cost</c> as every crafted
    /// item has them (<see cref="RecipeYaml"/>), and <c>cells</c> (0 to 20). A key left out keeps the built-in default. An
    /// unknown tacklebox is a warning; a value out of range an error, which rejects the file set so the previous values
    /// stay. Whether a station or cost item exists is checked when the recipes are made (<see cref="CraftRecipes"/>).
    /// </summary>
    public sealed class TackleboxesModel : YamlModel
    {
        public const int MaxCells = 20;

        public Dictionary<string, TackleboxStats> Stats { get; } = new Dictionary<string, TackleboxStats>(StringComparer.OrdinalIgnoreCase);

        protected override void Read(YamlNode root)
        {
            YamlNode boxes = root.Get("tackleboxes");
            if (boxes.Kind == YamlNodeKind.Map)
            {
                foreach (KeyValuePair<string, YamlNode> entry in boxes.Entries)
                    ReadBox(entry.Key, entry.Value);
            }
            else if (boxes.Kind != YamlNodeKind.Missing && boxes.Kind != YamlNodeKind.Null)
            {
                boxes.Error("expected one entry per tacklebox prefab, as in 'Mantle_FinewoodTacklebox: { cells: 3 }'");
            }
        }

        private void ReadBox(string id, YamlNode node)
        {
            TackleboxKind kind = TackleboxCatalog.ById(id);
            if (kind == null)
            {
                node.Warn("no tacklebox of that name; Mantle's are Mantle_DriftwoodTacklebox, Mantle_FinewoodTacklebox, Mantle_CarapaceTacklebox, Mantle_FlametalTacklebox and Mantle_TimberwoodTacklebox");
                return;
            }
            if (node.Kind != YamlNodeKind.Map)
            {
                if (node.Kind != YamlNodeKind.Null)
                    node.Error("a tacklebox takes station:, level:, cost: and cells:, as in { cells: 3 }");
                return;
            }
            TackleboxStats defaults = kind.Defaults;
            int cells = RecipeYaml.Whole(node.Get("cells"), 0, MaxCells, defaults.Cells);
            Stats[kind.Id] = new TackleboxStats(RecipeYaml.Station(node, defaults), RecipeYaml.Level(node, defaults), RecipeYaml.Cost(node, defaults), cells);
        }
    }
}
