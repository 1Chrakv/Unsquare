using System.Collections.Generic;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Terrain;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// Writes a <see cref="TerrainSlope"/> into a variant prefab's terraform data: pad height offset and a flat area
    /// collapsed to the lot centre. Remembers the prefab's own data, so applying twice never adds up.
    /// </summary>
    internal sealed class TerrainSlopeApplier
    {
        private readonly Dictionary<Entity, BuildingTerraformData> _originals = new();

        public bool Apply(EntityManager entityManager, Entity prefab, TerrainSlope slope, float2 lotCentre)
        {
            if (!TryGetOriginal(entityManager, prefab, out var data))
                return false;
            data.m_HeightOffset += slope.PadOffset;
            // A point-sized flat area lets the game interpolate the lot edge heights across the whole lot.
            data.m_FlatX0 = data.m_FlatX1 = new float3(lotCentre.x);
            data.m_FlatZ0 = data.m_FlatZ1 = new float3(lotCentre.y);
            entityManager.SetComponentData(prefab, data);
            return true;
        }

        public void Clear() => _originals.Clear();

        private bool TryGetOriginal(EntityManager entityManager, Entity prefab, out BuildingTerraformData original)
        {
            if (_originals.TryGetValue(prefab, out original))
                return true;
            if (!entityManager.Exists(prefab) || !entityManager.HasComponent<BuildingTerraformData>(prefab))
                return false;
            original = entityManager.GetComponentData<BuildingTerraformData>(prefab);
            _originals[prefab] = original;
            return true;
        }
    }
}
