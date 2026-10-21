using System;
using System.Collections.Generic;
using Mantle.Crafting;
using ExtraPotions.Mantle.Embedded.YamlConfig;

namespace Mantle.Backpacks
{
    /// <summary>
    /// The parsed Mantle.Backpacks*.yml files: under <c>backpacks:</c>, one entry per backpack prefab
    /// (<see cref="BackpackCatalog"/>), each key optional: <c>station</c> (a crafting station's prefab name), <c>level</c>
    /// (1 to 10), <c>cost</c> (prefab:amount pairs), <c>slots</c> (0 to 40), <c>carry</c> (0 to 1000) and <c>portal</c>.
    /// A key left out keeps the built-in default. An unknown backpack is a warning; a value out of range an error, which
    /// rejects the file set so the previous values stay. Whether a station or cost item exists is only known once the
    /// game's items are loaded, so that is checked when the recipes are made (<see cref="Crafting.CraftRecipes"/>).
    /// </summary>
    public sealed class BackpacksModel : YamlModel
    {
        public const int MaxSlots = 40;
        public const float MaxCarry = 1000f;

        public Dictionary<string, BackpackStats> Stats { get; } = new Dictionary<string, BackpackStats>(StringComparer.OrdinalIgnoreCase);

        protected override void Read(YamlNode root)
        {
            YamlNode backpacks = root.Get("backpacks");
            if (backpacks.Kind == YamlNodeKind.Map)
            {
                foreach (KeyValuePair<string, YamlNode> entry in backpacks.Entries)
                    ReadPack(entry.Key, entry.Value);
            }
            else if (backpacks.Kind != YamlNodeKind.Missing && backpacks.Kind != YamlNodeKind.Null)
            {
                backpacks.Error("expected one entry per backpack prefab, as in 'Mantle_TrollhideBackpack: { slots: 8 }'");
            }
        }

        private void ReadPack(string id, YamlNode node)
        {
            BackpackKind kind = BackpackCatalog.ById(id);
            if (kind == null)
            {
                node.Warn("no backpack of that name; Mantle's are Mantle_DeerhideSatchel, Mantle_TrollhideBackpack, Mantle_RootboundPack, Mantle_WolfpeltPack, Mantle_LoxHauler, Mantle_CarapacePack, Mantle_AsksvinPack and Mantle_MoosehidePack");
                return;
            }
            if (node.Kind != YamlNodeKind.Map)
            {
                if (node.Kind != YamlNodeKind.Null)
                    node.Error("a backpack takes station:, level:, cost:, slots:, carry: and portal:, as in { slots: 8, carry: 50 }");
                return;
            }
            Stats[kind.Id] = Merge(kind.Defaults, node);
        }

        private static BackpackStats Merge(BackpackStats defaults, YamlNode node)
        {
            int slots = RecipeYaml.Whole(node.Get("slots"), 0, MaxSlots, defaults.Slots);
            float carry = RecipeYaml.Number(node.Get("carry"), 0f, MaxCarry, defaults.Carry);
            bool portal = node.Get("portal").TryBool(out bool p) ? p : defaults.Portal;
            return new BackpackStats(RecipeYaml.Station(node, defaults), RecipeYaml.Level(node, defaults), RecipeYaml.Cost(node, defaults),
                slots, carry, portal);
        }
    }
}
