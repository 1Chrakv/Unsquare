using Colossal.Entities;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Transform = Game.Objects.Transform;

namespace Unsquare.Api
{
    /// <summary>
    /// Converts between world space and a building's local space (x = width, y = up, z = depth),
    /// in which all cage data is expressed. Use it to place gizmos and to map drag deltas.
    /// </summary>
    public static class BuildingSpace
    {
        public static bool TryGetTransform(EntityManager entityManager, Entity building, out Transform transform) =>
            entityManager.TryGetComponent(building, out transform);

        public static float3 PointToWorld(in Transform transform, float3 local) =>
            transform.m_Position + math.rotate(transform.m_Rotation, local);

        public static float3 PointToBuilding(in Transform transform, float3 world) =>
            math.rotate(math.inverse(transform.m_Rotation), world - transform.m_Position);

        /// <summary>For drag deltas: rotates a world-space direction into building space.</summary>
        public static float3 DirectionToBuilding(in Transform transform, float3 world) =>
            math.rotate(math.inverse(transform.m_Rotation), world);

        public static float3 HandleWorldPosition(in Transform transform, in CageState state, CageHandle handle) =>
            PointToWorld(transform, state.HandlePosition(handle));
    }
}
