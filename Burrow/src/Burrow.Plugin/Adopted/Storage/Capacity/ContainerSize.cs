namespace Burrow.Capacity
{
    /// <summary>Width and height of a container grid.</summary>
    public readonly struct ContainerSize
    {
        public ContainerSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }

        public bool Equals(ContainerSize other) => Width == other.Width && Height == other.Height;

        public override string ToString() => Width + "x" + Height;
    }
}
