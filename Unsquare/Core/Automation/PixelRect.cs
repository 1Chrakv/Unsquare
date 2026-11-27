namespace Unsquare.Core.Automation
{
    /// <summary>A pixel rectangle on screen, top-left origin.</summary>
    public readonly struct PixelRect
    {
        public PixelRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
    }
}
