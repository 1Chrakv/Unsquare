namespace Unsquare.Api
{
    public enum DeformRequestResult
    {
        Accepted,

        /// <summary>Accepted after clamping offsets to the allowed range.</summary>
        Clamped,

        /// <summary>The cage would fold the building inside out; nothing was requested.</summary>
        Rejected,

        /// <summary>The entity is not a building with deformable geometry.</summary>
        NotDeformable,
    }
}
