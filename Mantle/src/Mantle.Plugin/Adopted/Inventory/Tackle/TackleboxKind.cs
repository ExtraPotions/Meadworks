using Mantle.Crafting;

namespace Mantle.Tackle
{
    /// <summary>
    /// One of Mantle's tackleboxes (<see cref="CraftedKind"/>): its stats, the built-in defaults and the ones in use
    /// (the YAML over the defaults, <see cref="TackleboxesFile"/>). Never worn on the body: it only lies in the Tacklebox
    /// slot, so its item is all it has (<see cref="TackleboxPrefab"/>).
    /// </summary>
    public sealed class TackleboxKind : CraftedKind
    {
        public TackleboxKind(string id, string word, string name, string look, float weight, TackleboxStats defaults)
            : base(id, "tacklebox", word, name, look, weight)
        {
            Defaults = defaults;
            Stats = defaults;
        }

        public TackleboxStats Defaults { get; }

        public TackleboxStats Stats { get; internal set; }

        public override CraftStats Recipe => Stats;

        public override CraftStats DefaultRecipe => Defaults;

        public override bool Craftable => TackleboxSettings.Active;
    }
}
