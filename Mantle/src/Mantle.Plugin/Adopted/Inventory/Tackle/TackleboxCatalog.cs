using System;
using System.Collections.Generic;

namespace Mantle.Tackle
{
    /// <summary>
    /// Mantle's tackleboxes (the user's table, 2026-09-28): 1, 2, 6 and 8 cells, each recipe taking the box before it,
    /// so a box is upgraded rather than piled up. Their looks are the user's words: a plain wooden box covered in furs,
    /// fine wood in troll leather with bronze fittings, Yggdrasil wood coated in carapace, flametal covered in asksvin hide.
    /// The Deep North box (expDARE, 2026-10-19) adds 12 cells (three full rows of four) from Timberwood (the game's
    /// Frostwood), Seal Pelt and gold at the black forge level 4, as the Moosehide Pack; it wears the Flametal box's art
    /// with a frost tint (<see cref="Tinted"/>).
    /// These are the built-in defaults; <c>Mantle.Tackleboxes.yml</c> overrides any number of them.
    /// </summary>
    public static class TackleboxCatalog
    {
        private static readonly Dictionary<string, TackleboxKind> byName = new Dictionary<string, TackleboxKind>();
        private static readonly Dictionary<string, TackleboxKind> byId = new Dictionary<string, TackleboxKind>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<TackleboxKind> All { get; } = new List<TackleboxKind>
        {
            new TackleboxKind("Mantle_DriftwoodTacklebox", "driftwood", "Driftwood Tacklebox", "A plain wooden box covered in furs.", 2f,
                new TackleboxStats("piece_workbench", 2, "Wood:10, LeatherScraps:10, DeerHide:5", 1)),
            new TackleboxKind("Mantle_FinewoodTacklebox", "finewood", "Finewood Tacklebox", "A fine wood box wrapped in troll leather, with bronze corners and clasp.", 3f,
                new TackleboxStats("piece_workbench", 3, "Mantle_DriftwoodTacklebox:1, FineWood:10, TrollHide:10, Bronze:2", 2)),
            new TackleboxKind("Mantle_CarapaceTacklebox", "carapace", "Carapace Tacklebox", "A Yggdrasil wood box coated in carapace plates.", 3f,
                new TackleboxStats("blackforge", 1, "Mantle_FinewoodTacklebox:1, Carapace:10, YggdrasilWood:10", 6)),
            new TackleboxKind("Mantle_FlametalTacklebox", "flametal", "Flametal Tacklebox", "A flametal box covered in asksvin hide.", 4f,
                new TackleboxStats("blackforge", 3, "Mantle_CarapaceTacklebox:1, FlametalNew:5, AskHide:10", 8)),
            new TackleboxKind(TimberwoodId, "timberwood", "Timberwood Tacklebox", "A timberwood box lined with seal pelt, rimed in frost, with gold fittings.", 4f,
                new TackleboxStats("blackforge", 4, "Mantle_FlametalTacklebox:1, Frostwood:10, SealHide:8, Gold:2", 12)),
        };

        /// <summary>The Deep North box's item prefab (new in 2026-10-19; a world saved with it keeps this name).</summary>
        public const string TimberwoodId = "Mantle_TimberwoodTacklebox";

        /// <summary>
        /// Boxes without art of their own: the box whose model and icon they wear, and the frost tint laid over them
        /// (<see cref="TackleboxPrefab"/>). The art catalog has no entry for these ids.
        /// </summary>
        public static IReadOnlyDictionary<string, (string Art, float R, float G, float B)> Tinted { get; } =
            new Dictionary<string, (string, float, float, float)>(StringComparer.OrdinalIgnoreCase)
            {
                [TimberwoodId] = ("Mantle_FlametalTacklebox", 0.80f, 0.92f, 1f),
            };

        /// <summary>The kind of an item by its shared name ($mantle_tacklebox_...), or null for any other item.</summary>
        public static TackleboxKind Of(ItemDrop.ItemData item) => item != null && Tables().TryGetValue(item.m_shared.m_name, out TackleboxKind kind) ? kind : null;

        public static TackleboxKind ById(string id)
        {
            Tables();
            return id != null && byId.TryGetValue(id.Trim(), out TackleboxKind kind) ? kind : null;
        }

        private static Dictionary<string, TackleboxKind> Tables()
        {
            if (byName.Count == 0)
            {
                foreach (TackleboxKind kind in All)
                {
                    byName[kind.Token] = kind;
                    byId[kind.Id] = kind;
                }
            }
            return byName;
        }
    }
}
