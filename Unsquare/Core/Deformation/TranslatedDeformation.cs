using System;
using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// A building's deformation seen from a part placed at <see cref="Offset"/> in building space (e.g. an upgrade
    /// extension): points are given and returned in the part's own frame, which shares the building's rotation.
    /// </summary>
    public sealed class TranslatedDeformation : IDeformation
    {
        private readonly IDeformation _inner;

        public TranslatedDeformation(IDeformation inner, float3 offset)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Offset = offset;
        }

        public float3 Offset { get; }

        public float3 Apply(float3 p, out float3x3 jacobian) => _inner.Apply(p + Offset, out jacobian) - Offset;
    }
}
