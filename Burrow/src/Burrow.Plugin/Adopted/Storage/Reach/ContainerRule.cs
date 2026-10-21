using Burrow.Core;

namespace Burrow.Reach
{
    /// <summary>One prefab's entry of the <c>containers:</c> map: enabled flag, optional range and the allow / deny lists.</summary>
    public sealed class ContainerRule
    {
        public ContainerRule(bool enabled, float? range, ItemMatchSet allow, ItemMatchSet deny)
        {
            Enabled = enabled;
            Range = range;
            Allow = allow ?? ItemMatchSet.Empty;
            Deny = deny ?? ItemMatchSet.Empty;
        }

        /// <summary>The rule of every prefab that is not listed: enabled, the cfg range, everything allowed.</summary>
        public static ContainerRule Default { get; } = new ContainerRule(true, null, ItemMatchSet.Empty, ItemMatchSet.Empty);

        public bool Enabled { get; }

        /// <summary>Metres from the player, replacing the file's or the cfg's range when set.</summary>
        public float? Range { get; }

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
