// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; the biome essences.
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Display;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Stones;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>The Inscribe tab's right side: the chosen gear, the rune and essence rows, the cost and the button.</summary>
    internal static class InscribePane
    {
        public static void Fill(TableView view, InscribeChoice choice)
        {
            PanelParts parts = view.Parts;
            PaneText.Header(parts, choice.Item?.GetIcon(), choice.Item != null ? NameOf(choice.Item) : Words.Localize("$ecf_table_no_gear"),
                choice.Item);
            PaneText.Body(parts, choice.Item != null ? InscribeText.Describe(choice) : Words.Localize("$ecf_table_no_gear_desc"));
            PaneText.Labels(parts, "$ecf_table_label_rune", "$ecf_table_label_essence");
            FillRunes(view, choice);
            FillEssences(view, choice);
            FillPills(parts, choice);
            bool ready = FillCost(view, choice);
            PaneText.Button(parts, "$ecf_table_inscribe", ready && TableUse.Works(view, choice.Item, choice.Rune, choice.Steering));
            PaneText.Extra(parts, "$ecf_table_store_all", RuneStash.Carried(view.Inventory) > 0 && view.Store.Writable);
        }

        // The inscriptions the chosen essence can give with the Ascension Rune, as pills under the text.
        private static void FillPills(PanelParts parts, InscribeChoice choice)
        {
            Essence? essence = choice.Essence;
            if (choice.Item == null || essence == null || choice.Def == null || !TableIcons.Steers(choice.Def))
            {
                return;
            }
            PaneText.Pills(parts, EssencePills.Of(choice.Item, essence, choice.Def), essence.Color);
        }

        /// <summary>
        /// A slot's number: what the table holds, none shown at 0 (user 2026-10-07: the rows "should not show your
        /// inventory quantity"; what you carry still pays and keeps the slot lit).
        /// </summary>
        public static string Held(int stored) => stored > 0 ? stored.ToString() : "";

        /// <summary>Essence the table can spend: its pool (carried Essence is stored first, Ctrl + click or Store all).</summary>
        public static int Have(TableView view) => view.Store.Essence;

        /// <summary>The item's localized name in its rarity's colour (plain for Normal).</summary>
        public static string NameOf(ItemDrop.ItemData item)
        {
            string? topic = DisplayCache.Topic(ItemState.Read(item), item);
            return Words.Localize(topic ?? item.m_shared.m_name);
        }

        private static void FillRunes(TableView view, InscribeChoice choice)
        {
            SlotRow? row = view.Parts.Runes;
            if (row == null)
            {
                return;
            }
            row.Show(true);
            row.Fit(StoneCatalog.BuiltInIds.Length);
            for (int i = 0; i < row.Count && i < StoneCatalog.BuiltInIds.Length; i++)
            {
                string id = StoneCatalog.BuiltInIds[i];
                int stored = view.Store.Runes(id);
                StoneDef? def = TableIcons.Def(id);
                bool usable = StoneVerbs.IsUsable(def);
                row[i].Show(TableIcons.Rune(id), null, Held(stored), true, dim: !usable || stored == 0);
                row[i].Choose(id == choice.Rune, Items.StoneVisuals.Tint(id));
            }
        }

        private static void FillEssences(TableView view, InscribeChoice choice)
        {
            SlotRow? row = view.Parts.Essences;
            if (row == null)
            {
                return;
            }
            row.Show(true);
            row.Fit(Essences.Offered + 1);
            int have = Have(view);
            row[0].Show(TableIcons.EssenceItem(), null, Held(view.Store.Essence), true, dim: have == 0);
            row[0].Choose(false);
            bool steers = TableIcons.Steers(choice.Def);
            bool short_ = choice.Item != null && have < choice.EssenceCost;
            for (int i = 1; i < row.Count && i <= Essences.Offered; i++)
            {
                Essence essence = Essences.All[i - 1];
                // Greyscale until the Ascension Rune is picked; dimmed in colour when it is but the essence falls short.
                UnityEngine.Sprite? grey = steers ? null : TableIcons.EssenceGrey(essence);
                bool greyed = grey != null;
                row[i].Show(greyed ? grey : TableIcons.Essence(essence), null, "", true, dim: (!steers && !greyed) || short_);
                row[i].Choose(essence == choice.Essence && steers, essence.Color);
            }
        }

        // The rune, then the essence when one steers; true when the press can be paid.
        private static bool FillCost(TableView view, InscribeChoice choice)
        {
            CostRow? cost = view.Parts.Cost;
            cost?.Clear();
            if (choice.Item == null || cost == null)
            {
                return false;
            }
            int runes = view.Store.Runes(choice.Rune);
            bool enough = runes >= choice.RuneCost && StoneVerbs.IsUsable(choice.Def);
            cost.Add(TableIcons.Rune(choice.Rune), Words.Localize(choice.Def?.Name ?? ""), choice.RuneCost, runes >= choice.RuneCost);
            Essence? essence = choice.Steering;
            if (essence != null)
            {
                bool paid = Have(view) >= choice.EssenceCost;
                cost.Add(TableIcons.EssenceItem(), Words.Localize("$ecf_essence_item"), choice.EssenceCost, paid);
                enough &= paid;
            }
            return enough;
        }
    }
}
