using System.Collections.Generic;

namespace Burrow.Core
{
    /// <summary>A list of vocabulary entries; an item matches when any entry does. Blank entries are dropped,
    /// entries with a <see cref="ItemMatcher.Problem"/> are kept (they match nothing) so callers can report them.</summary>
    public sealed class ItemMatchSet
    {
        private readonly List<ItemMatcher> matchers;

        private ItemMatchSet(List<ItemMatcher> matchers)
        {
            this.matchers = matchers;
        }

        public static ItemMatchSet Empty { get; } = new ItemMatchSet(new List<ItemMatcher>());

        public bool IsEmpty => matchers.Count == 0;

        public IReadOnlyList<ItemMatcher> Matchers => matchers;

        public static ItemMatchSet Parse(IEnumerable<string> entries, ItemGroups groups)
        {
            List<ItemMatcher> parsed = new List<ItemMatcher>();
            if (entries != null)
            {
                foreach (string entry in entries)
                {
                    if (!string.IsNullOrWhiteSpace(entry))
                        parsed.Add(ItemMatcher.Parse(entry, groups));
                }
            }
            return parsed.Count == 0 ? Empty : new ItemMatchSet(parsed);
        }

        public bool Matches(ItemDrop.ItemData item)
        {
            return item != null && item.m_shared != null && Matches(ItemNames.PrefabName(item), item.m_shared);
        }

        public bool Matches(string prefabName, ItemDrop.ItemData.SharedData shared)
        {
            foreach (ItemMatcher matcher in matchers)
            {
                if (matcher.Matches(prefabName, shared))
                    return true;
            }
            return false;
        }

        public override string ToString() => string.Join(", ", matchers);
    }
}
