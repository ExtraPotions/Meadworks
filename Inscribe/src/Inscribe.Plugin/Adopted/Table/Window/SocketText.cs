// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Text;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Display;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Sockets;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>The Sockets tab's words: the chosen gear's description, what the chosen stone does on it, a socket's tooltip.</summary>
    internal static class SocketText
    {
        private const string Gold = "<color=#FFB75C>";

        public static string Describe(SocketChoice choice)
        {
            ItemDrop.ItemData item = choice.Item!;
            ItemState state = ItemState.Read(item);
            ClassInfo info = ItemClasses.Classify(item);
            int level = ItemTier.Of(item);
            var sb = new StringBuilder();
            sb.Append(Words.Localize("$ecf_table_gear_line", TableWords.ClassName(info), level.ToString(),
                TableWords.Biome(level)));
            string block = state.IsEmpty ? "" : DisplayCache.Block(state, item).Trim('\n');
            sb.Append("\n\n").Append(state.Sockets > 0 ? block : Words.Localize("$ecf_table_no_sockets"));
            // The stone's own description, then what it would do on this item (user 2026-10-07: no hover text on the gems
            // and the chisel, "add the description of what the item does above").
            string name = Words.Localize(choice.Def?.Name ?? "$ecf_stone_" + choice.Stone);
            string description = Words.Localize(choice.Def?.Description ?? "$ecf_stone_" + choice.Stone + "_desc");
            sb.Append("\n\n").Append(Gold).Append(name).Append("</color>: ").Append(description);
            sb.Append('\n').Append(Capitalized(Effect(choice, state, info, level)));
            return sb.ToString();
        }

        private static string Capitalized(string text) =>
            text.Length > 0 ? char.ToUpper(text[0]) + text.Substring(1) : text;

        /// <summary>The gem in socket <paramref name="socket"/> (null when it is empty or unreadable) and its tooltip.</summary>
        public static string? GemAt(ItemState state, int socket, out string tip)
        {
            for (int i = 0; i < state.Gems.Count; i++)
            {
                if (state.GemSocketAt(i) == socket)
                {
                    GemRoll gem = state.Gems[i];
                    tip = AffixLines.Sentence(gem.Roll.Id, gem.Roll.Value, state.GemDefinitionAt(i));
                    return gem.GemId;
                }
            }
            tip = Words.Localize(socket < state.FilledSockets ? "$ecf_ui_socket_unreadable" : "$ecf_ui_socket_empty");
            return null;
        }

        // What the chosen stone would do on this item.
        private static string Effect(SocketChoice choice, ItemState state, ClassInfo info, int level)
        {
            if (!StoneCatalog.IsGem(choice.Stone))
            {
                return state.Sockets > 0
                    ? Words.Localize("$ecf_table_chisel_has")
                    : Words.Localize("$ecf_table_chisel_cuts", SocketDrops.CutChance(state.RarityId, 1),
                        SocketDrops.CutChance(state.RarityId, 2), SocketDrops.CutChance(state.RarityId, 3));
            }
            string? stat = GemCatalog.StatFor(choice.Stone, GemCatalog.BaseOf(info));
            AffixDef? def = stat != null ? ActiveRules.Current.Affixes.Get(stat) : null;
            if (def == null || !def.Enabled || !GemRolls.Range(def, level, out int weakest, out int best))
            {
                return Words.Localize("$ecf_table_gem_no_fit");
            }
            string gives = Words.Localize("$ecf_table_gem_gives", DisplayWords.Name(def.Name, def.Id),
                def.ShownTier(weakest).ToString(), def.ShownTier(best).ToString());
            return gives + Where(choice, state);
        }

        // Which socket the gem would go into: none yet, the one picked (its gem lost), or a pick still to make.
        private static string Where(SocketChoice choice, ItemState state)
        {
            if (state.Sockets == 0)
            {
                return " " + Words.Localize("$ecf_table_gem_needs_socket");
            }
            if (choice.Socket >= 0)
            {
                return " " + Words.Localize("$ecf_table_gem_replaces", (choice.Socket + 1).ToString()) + " "
                    + Runtime.GemRecoveryRuntime.ChanceLine(state.RarityId);
            }
            return state.FreeSockets == 0 ? " " + Words.Localize("$ecf_table_gem_pick_socket") : "";
        }
    }
}
