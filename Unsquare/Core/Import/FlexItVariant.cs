namespace Unsquare.Core.Import
{
    /// <summary>
    /// A FlexIt variant prefab parsed from its name: base prefab name, mode and angles in whole degrees
    /// (taper: A left end, B right end, C front, D back).
    /// </summary>
    public readonly struct FlexItVariant
    {
        public FlexItVariant(string baseName, FlexItMode mode, int a, int b, int c, int d)
        {
            BaseName = baseName;
            Mode = mode;
            A = a;
            B = b;
            C = c;
            D = d;
        }

        public string BaseName { get; }
        public FlexItMode Mode { get; }
        public int A { get; }
        public int B { get; }
        public int C { get; }
        public int D { get; }

        /// <summary>Everything but a taper bends the plan, which a box cage cannot follow.</summary>
        public bool IsCurved => Mode != FlexItMode.Taper;

        public override string ToString() => $"{BaseName} {Mode} {A}/{B}/{C}/{D}";
    }
}
