using UnityEngine;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
{
    /// <summary>
    /// A mod's box in the column: an id unique across mods (start it with the mod's name), a rank that places it
    /// among the others (the game's armour box is <see cref="Column.ArmorRank"/> and its weight box
    /// <see cref="Column.WeightRank"/>; lower sits higher), the icon it shows at its top, whether it keeps a line of text
    /// under the icon the way the game's boxes show a number, and the tooltip shown on hover. Tooltip words may be
    /// localization tokens; the game's tooltip localizes them.
    /// </summary>
    public sealed class PlateSpec
    {
        public PlateSpec(string id, int rank, Sprite? icon, bool withText, string topic, string tip)
        {
            Id = id;
            Rank = Mathf.Clamp(rank, 0, 9999);
            Icon = icon;
            WithText = withText;
            Topic = topic;
            Tip = tip;
        }

        public string Id { get; }

        public int Rank { get; }

        public Sprite? Icon { get; }

        public bool WithText { get; }

        public string Topic { get; }

        public string Tip { get; }
    }
}
