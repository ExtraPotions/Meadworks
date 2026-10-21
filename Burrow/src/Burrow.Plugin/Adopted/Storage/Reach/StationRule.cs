using Burrow.Core;

namespace Burrow.Reach
{
    /// <summary>One prefab's entry of the <c>stations:</c> map: enabled flag and the allow / deny lists for feeding.</summary>
    public sealed class StationRule
    {
        public StationRule(bool enabled, ItemMatchSet allow, ItemMatchSet deny)
        {
            Enabled = enabled;
            Allow = allow ?? ItemMatchSet.Empty;
            Deny = deny ?? ItemMatchSet.Empty;
        }

        /// <summary>The rule of every prefab that is not listed: enabled, everything allowed.</summary>
        public static StationRule Default { get; } = new StationRule(true, ItemMatchSet.Empty, ItemMatchSet.Empty);

        public bool Enabled { get; }

        public ItemMatchSet Allow { get; }

        public ItemMatchSet Deny { get; }

        /// <summary>Deny wins; an empty allow list allows everything.</summary>
        public bool Accepts(ItemDrop.ItemData item)
        {
            if (item == null || Deny.Matches(item))
                return false;
            return Allow.IsEmpty || Allow.Matches(item);
        }
    }
}
