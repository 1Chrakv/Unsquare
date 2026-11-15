using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// What a drag does to a handle since the press: a building-space translation, or a turn around a building axis
    /// through the handle's centre. Immutable.
    /// </summary>
    public readonly struct HandleMotion : IEquatable<HandleMotion>
    {
        private HandleMotion(bool isRotation, float3 translation, int axis, float angle)
        {
            IsRotation = isRotation;
            Translation = translation;
            Axis = axis;
            Angle = angle;
        }

        public bool IsRotation { get; }

        /// <summary>Delta in metres; zero for rotations.</summary>
        public float3 Translation { get; }

        /// <summary>Axis (0..2) of a rotation, or -1.</summary>
        public int Axis { get; }

        /// <summary>Radians, counter-clockwise seen from the positive axis.</summary>
        public float Angle { get; }

        public static HandleMotion Translate(float3 delta) => new(false, delta, -1, 0f);

        public static HandleMotion Rotate(int axis, float radians)
        {
            GizmoAxes.Direction(axis); // validates
            return new HandleMotion(true, float3.zero, axis, radians);
        }

        /// <summary>
        /// Offsets after the motion; the handle's corners are moved from <paramref name="start"/> and the result is
        /// quantized once, so a long drag never drifts.
        /// </summary>
        public CageOffsets Apply(CageBox box, CageOffsets start, CageHandle handle)
        {
            if (start == null)
                throw new ArgumentNullException(nameof(start));
            if (!IsRotation)
                return start.Move(handle, Translation);

            var rotation = quaternion.AxisAngle(GizmoAxes.Direction(Axis), Angle);
            float3 pivot = DeformedCage.HandlePosition(box, start, handle);
            var corners = CageTopology.CornersOf(handle);
            var offsets = start;
            for (int i = 0; i < corners.Count; i++)
            {
                int corner = corners[i];
                float3 turned = pivot + math.mul(rotation, DeformedCage.CornerPosition(box, start, corner) - pivot);
                offsets = offsets.WithCornerSteps(corner, CageOffsets.Quantize(turned - box.Corner(corner)));
            }
            return offsets;
        }

        public bool Equals(HandleMotion other) => IsRotation == other.IsRotation &&
                                                 Translation.Equals(other.Translation) && Axis == other.Axis &&
                                                 Angle.Equals(other.Angle);

        public override bool Equals(object obj) => obj is HandleMotion other && Equals(other);
        public override int GetHashCode() => (IsRotation, Translation, Axis, Angle).GetHashCode();

        public override string ToString() =>
            IsRotation ? $"rotate {math.degrees(Angle):0.#}° around {"XYZ"[Axis]}" : $"move {Translation}";
    }
}
