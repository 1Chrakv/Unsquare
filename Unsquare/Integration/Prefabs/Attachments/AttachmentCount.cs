namespace Unsquare.Integration.Prefabs.Attachments
{
    /// <summary>How many placements of one kind a variant has and how many were deformed (diagnostic log).</summary>
    internal readonly struct AttachmentCount
    {
        public AttachmentCount(string kind, int total, int deformed)
        {
            Kind = kind;
            Total = total;
            Deformed = deformed;
        }

        public string Kind { get; }
        public int Total { get; }
        public int Deformed { get; }

        public override string ToString() => $"{Kind} {Deformed}/{Total}";
    }
}
