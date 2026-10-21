// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System;
using System.Collections.Generic;
using System.Text;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// <c>inscribe dump items</c> (console-commands.md section 3, DECISIONS.md RC-10): the in-game survey that base
    /// prefabs, item classes (classes-and-tiers.md section 9), item levels and the material map are verified with. One tab-separated line per
    /// <c>ObjectDB</c> item, sorted by prefab name (ordinal), with a header row and nothing else, so two dumps diff
    /// cleanly and the file pastes into a spreadsheet. Written to <c>ExtraPotions.Inscribe.Adopted_items.txt</c> in the config folder.
    /// Reads this machine's object database and rules; any peer can run it (the server's console included).
    /// </summary>
    internal static class ItemSurvey
    {
        public const string FileName = "ExtraPotions.Inscribe.Adopted_items.txt";

        private static readonly string[] Columns =
        {
            "prefab", "display_name", "name_token", "item_type", "skill", "max_stack", "weight", "teleportable", "value",
            "recipes", "stations", "class", "magic_base", "level", "level_source", "material_tier", "visuals",
        };

        public static void Write(CommandCall call)
        {
            ObjectDB? db = ObjectDB.instance;
            if (db == null || db.m_items == null)
            {
                call.Reply("the object database is not loaded yet.");
                return;
            }
            Recipes.Refresh();
            List<string> rows = new List<string>();
            foreach (GameObject go in db.m_items)
            {
                if (go != null)
                {
                    rows.Add(Row(go));
                }
            }
            rows.Sort(StringComparer.Ordinal);
            rows.Insert(0, string.Join("\t", Columns));
            string path = ConfigOutput.Write(FileName, string.Join("\n", rows) + "\n");
            call.Reply($"wrote {rows.Count - 1} items to {path}");
        }

        private static string Row(GameObject go)
        {
            ItemDrop? drop = go.GetComponent<ItemDrop>();
            ItemDrop.ItemData.SharedData? shared = drop?.m_itemData?.m_shared;
            if (drop == null || shared == null)
            {
                return Cell(go.name) + "\t(no ItemDrop)";
            }
            List<string> cells = new List<string>
            {
                go.name, Words.Localize(shared.m_name), shared.m_name, shared.m_itemType.ToString(), shared.m_skillType.ToString(),
                Numbers.Format(shared.m_maxStackSize), Numbers.Format(shared.m_weight), shared.m_teleportable ? "yes" : "no",
                Numbers.Format(shared.m_value),
            };
            SurveyRecipes.Add(cells, go.name);
            AddClassification(cells, go.name, drop.m_itemData);
            cells.Add(SurveyVisuals.Describe(go));
            return string.Join("\t", cells.ConvertAll(Cell));
        }

        private static void AddClassification(List<string> cells, string prefab, ItemDrop.ItemData item)
        {
            string? itemClass = ItemClasses.Classify(item).ClassId;
            bool magicBase = ItemClasses.IsMagicBase(item);
            TierResult level = itemClass != null ? ItemTier.Explain(prefab) : default;
            IReadOnlyDictionary<string, int> materials = ActiveRules.Current.Economy.ItemTiers.Materials;
            cells.Add(itemClass ?? "-");
            cells.Add(magicBase ? "yes" : ItemClasses.IsStone(item) ? "rune" : "no");
            cells.Add(itemClass != null ? Numbers.Format(level.Tier) : "-");
            cells.Add(itemClass != null ? level.Source : "-");
            cells.Add(materials.TryGetValue(prefab, out int materialTier) ? Numbers.Format(materialTier) : "-");
        }

        /// <summary>One cell: no tabs or line breaks, never empty.</summary>
        private static string Cell(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "-";
            }
            StringBuilder sb = new StringBuilder(text!.Length);
            foreach (char c in text)
            {
                sb.Append(c == '\t' || c == '\r' || c == '\n' ? ' ' : c);
            }
            return sb.ToString();
        }
    }

    /// <summary>The recipe and station columns of the survey: every enabled recipe producing the item.</summary>
    internal static class SurveyRecipes
    {
        /// <summary>Recipes are separated by " | " in both columns, in the same order.</summary>
        public static void Add(List<string> cells, string prefab)
        {
            IReadOnlyList<Recipe> recipes = Recipes.For(prefab);
            List<string> materials = new List<string>();
            List<string> stations = new List<string>();
            foreach (Recipe recipe in recipes)
            {
                materials.Add(Materials(recipe));
                string station = recipe.m_craftingStation != null ? recipe.m_craftingStation.gameObject.name : "none";
                stations.Add($"{station} {Numbers.Format(recipe.m_minStationLevel)}");
            }
            cells.Add(recipes.Count == 0 ? "-" : string.Join(" | ", materials));
            cells.Add(recipes.Count == 0 ? "-" : string.Join(" | ", stations));
        }

        // "Bronze x8 +4/lvl, Wood x2"; a resource with no craft amount is marked upgrade-only; output amount when not 1.
        private static string Materials(Recipe recipe)
        {
            List<string> parts = new List<string>();
            foreach (Piece.Requirement req in recipe.m_resources ?? Array.Empty<Piece.Requirement>())
            {
                if (req?.m_resItem == null)
                {
                    continue;
                }
                string name = req.m_resItem.gameObject.name;
                string perLevel = req.m_amountPerLevel > 0 ? $" +{Numbers.Format(req.m_amountPerLevel)}/lvl" : "";
                parts.Add(req.m_amount > 0 ? $"{name} x{Numbers.Format(req.m_amount)}{perLevel}" : $"{name} upgrade-only{perLevel}");
            }
            string output = recipe.m_amount != 1 ? $" => x{Numbers.Format(recipe.m_amount)}" : "";
            return (parts.Count == 0 ? "(no resources)" : string.Join(", ", parts)) + output;
        }
    }

    /// <summary>
    /// The visuals column (prefabs.md section 4): renderers, how many of their materials have <c>_EmissionColor</c>
    /// (the property the stone tint writes), and <c>Light</c>, <c>ParticleSystem</c> and <c>LightLod</c> children.
    /// </summary>
    internal static class SurveyVisuals
    {
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        public static string Describe(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            int emissive = 0;
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    emissive += material != null && material.HasProperty(Emission) ? 1 : 0;
                }
            }
            int lights = go.GetComponentsInChildren<Light>(true).Length;
            int particles = go.GetComponentsInChildren<ParticleSystem>(true).Length;
            int lods = go.GetComponentsInChildren<LightLod>(true).Length;
            return $"renderers {renderers.Length}, emission {emissive}, lights {lights}, particles {particles}, lightlod {lods}";
        }
    }
}
