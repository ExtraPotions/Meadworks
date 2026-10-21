using Burrow.Core;

namespace Burrow.Stacks
{
    /// <summary>One entry of the items: map of Burrow.Stacks.yml: a matcher with an absolute stack and/or weight.</summary>
    public sealed class StackRule
    {
        public StackRule(ItemMatcher matcher, int? stack, float? weight)
        {
            Matcher = matcher;
            Stack = stack;
            Weight = weight;
        }

        public ItemMatcher Matcher { get; }

        public int? Stack { get; }

        public float? Weight { get; }

        /// <summary>A plain prefab or token name: applied after every pattern, so the specific entry wins.</summary>
        public bool IsExact => Matcher.IsExactName;

        public bool Matches(string prefabName, ItemDrop.ItemData.SharedData shared) => Matcher.Matches(prefabName, shared);

        public override string ToString() => Matcher.ToString();
    }
}
