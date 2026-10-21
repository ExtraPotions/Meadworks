namespace Burrow.Stacks
{
    /// <summary>A maximum stack and a weight, as the game stores them in an item's shared data.</summary>
    public readonly struct ItemValue
    {
        public ItemValue(int stack, float weight)
        {
            Stack = stack;
            Weight = weight;
        }

        public int Stack { get; }

        public float Weight { get; }
    }
}
