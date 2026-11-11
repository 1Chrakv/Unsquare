using Unsquare.Core.Cage;

namespace Unsquare.Integration.Import
{
    /// <summary>Outcome of taking a Placemaker building over: the Unsquare cage reproducing its shape.</summary>
    internal readonly struct PlacemakerAdoption
    {
        public PlacemakerAdoption(PlacemakerAdoptionStatus status, DeformSpec spec = null, bool approximated = false,
            float maxError = 0f)
        {
            Status = status;
            Spec = spec;
            Approximated = approximated;
            MaxError = maxError;
        }

        public PlacemakerAdoptionStatus Status { get; }

        /// <summary>Linked lot cage on the plain base; null unless <see cref="PlacemakerAdoptionStatus.Ready"/>.</summary>
        public DeformSpec Spec { get; }

        /// <summary>The cage was fitted (Placemaker's yard shift is not bilinear); <see cref="MaxError"/> in metres.</summary>
        public bool Approximated { get; }
        public float MaxError { get; }
    }
}
