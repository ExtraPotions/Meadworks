using Mantle.Core;

namespace Mantle.Tackle
{
    /// <summary>
    /// The tackleboxes' $mantle_tacklebox_ words: each box's name and its description, which says how many cells it
    /// gives (so it is written again when the tackleboxes file changes), and the messages of the Tacklebox slot.
    /// </summary>
    public static class TackleboxWords
    {
        /// <summary>The empty Tacklebox slot's caption.</summary>
        public static string Tacklebox { get; private set; }

        /// <summary>An empty tacklebox cell's caption.</summary>
        public static string Bait { get; private set; }

        public static string Full { get; private set; }

        public static void Register()
        {
            foreach (TackleboxKind kind in TackleboxCatalog.All)
                Language.Add(kind.Token.Substring(1), kind.Name);
            Tacklebox = Language.Add("mantle_tackle", "Tackle");
            Bait = Language.Add("mantle_bait", "Bait");
            Full = Language.Add("mantle_tacklebox_full", "The tacklebox is full");
            DescribeAll();
        }

        public static void DescribeAll()
        {
            foreach (TackleboxKind kind in TackleboxCatalog.All)
                Language.Add(kind.DescriptionToken.Substring(1), Describe(kind));
        }

        private static string Describe(TackleboxKind kind)
        {
            int cells = kind.Stats.Cells;
            string gives = cells == 1 ? "one cell" : $"{cells} cells";
            return $"{kind.Look} Put it in your Tacklebox slot and click it there to open it: {gives} for bait, which the fishing rod takes first. Right click a bait in it to fish with that one.";
        }
    }
}
