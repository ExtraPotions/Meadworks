namespace Mantle.Core
{
    /// <summary>
    /// Whether an inventory changed since this watch last asked: its <c>m_onChanged</c>, which the game calls after every
    /// add, remove, move and stack change (<c>Inventory.Changed</c>), or a different inventory (a new body, another
    /// world). For a reader that would otherwise search the inventory every frame; each reader keeps its own watch.
    /// </summary>
    public sealed class InventoryWatch
    {
        private Inventory watched;
        private bool changed = true;

        /// <summary>True once after a change, the first time a new inventory is asked about, or after <see cref="Mark"/>.</summary>
        public bool Changed(Inventory inventory)
        {
            if (!ReferenceEquals(inventory, watched))
            {
                watched = inventory;
                if (inventory != null)
                    inventory.m_onChanged += Mark;
                changed = true;
            }
            bool was = changed;
            changed = false;
            return was;
        }

        /// <summary>Asks the next <see cref="Changed"/> to say yes (a setting or a list changed).</summary>
        public void Mark() => changed = true;
    }
}
