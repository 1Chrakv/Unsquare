using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Common;
using Game.Prefabs;
using Game.Zones;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Naming;
using Unsquare.Core.Zoning;
using Transform = Game.Objects.Transform;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// Gives back the zoning that variants of earlier versions wiped: they overrode their lot cells to "no zone".
    /// Only overridden, unzoned cells under a deformed growable's own lot get its zone back. Main thread only.
    /// </summary>
    internal sealed class LotZoneRepair
    {
        private readonly EntityManager _entityManager;
        private readonly PrefabSystem _prefabSystem;
        private readonly HashSet<Entity> _changedBlocks = new();

        public LotZoneRepair(EntityManager entityManager, PrefabSystem prefabSystem)
        {
            _entityManager = entityManager;
            _prefabSystem = prefabSystem;
        }

        /// <summary>Repairs the lots of <paramref name="buildings"/>; returns the number of cells given back.</summary>
        public int Repair(NativeArray<Entity> buildings, NativeArray<Entity> blocks, out int repairedBuildings)
        {
            _changedBlocks.Clear();
            repairedBuildings = 0;
            int cells = 0;
            foreach (var building in buildings)
            {
                if (!TryGetLot(building, out var lot))
                    continue;
                int before = cells;
                foreach (var block in blocks)
                    cells += RepairBlock(block, lot);
                if (cells > before)
                    repairedBuildings++;
            }
            // Updated blocks are rechecked by CellCheckSystem, and their buildings by the zone check.
            foreach (var block in _changedBlocks)
                _entityManager.AddComponent<Updated>(block);
            return cells;
        }

        private bool TryGetLot(Entity building, out Lot lot)
        {
            lot = default;
            if (!_entityManager.TryGetComponent(building, out PrefabRef prefabRef) ||
                !_entityManager.TryGetComponent(building, out Transform transform) ||
                !_entityManager.TryGetComponent(prefabRef.m_Prefab, out SpawnableBuildingData spawnable) ||
                !_entityManager.TryGetComponent(prefabRef.m_Prefab, out BuildingData buildingData) ||
                !_entityManager.TryGetComponent(spawnable.m_ZonePrefab, out ZoneData zoneData) ||
                zoneData.m_ZoneType.Equals(ZoneType.None) || math.any(buildingData.m_LotSize < 1) ||
                !_prefabSystem.TryGetPrefab(prefabRef.m_Prefab, out PrefabBase prefab) || prefab == null ||
                !VariantNaming.IsVariantName(prefab.name))
                return false;
            lot = new Lot(transform.m_Position.xz, transform.m_Rotation, buildingData.m_LotSize, zoneData.m_ZoneType);
            return true;
        }

        private int RepairBlock(Entity entity, Lot lot)
        {
            var block = _entityManager.GetComponentData<Block>(entity);
            if (!MathUtils.Intersect(ZoneUtils.CalculateBounds(block), lot.Bounds))
                return 0;
            int4 area = _entityManager.GetComponentData<ValidArea>(entity).m_Area;
            var cells = _entityManager.GetBuffer<Cell>(entity);
            int repaired = 0;
            for (int y = 0; y < lot.Size.y; y++)
            for (int x = 0; x < lot.Size.x; x++)
            {
                float2 center = LotCells.CellCenter(lot.Position, lot.Rotation, lot.Size, new int2(x, y));
                int2 index = ZoneUtils.GetCellIndex(block, center);
                if (!math.all(index >= area.xz & index < area.yw))
                    continue;
                int i = index.y * block.m_Size.x + index.x;
                var cell = cells[i];
                if ((cell.m_State & CellFlags.Overridden) == 0 || !cell.m_Zone.Equals(ZoneType.None))
                    continue;
                // Cleared Overridden, or the next cell check would reset the zone to none again.
                cell.m_State &= ~CellFlags.Overridden;
                cell.m_Zone = lot.Zone;
                cells[i] = cell;
                repaired++;
            }
            if (repaired > 0)
                _changedBlocks.Add(entity);
            return repaired;
        }

        private readonly struct Lot
        {
            public Lot(float2 position, quaternion rotation, int2 size, ZoneType zone)
            {
                Position = position;
                Rotation = rotation;
                Size = size;
                Zone = zone;
                LotCells.Bounds(position, rotation, size, out var min, out var max);
                Bounds = new Bounds2(min, max);
            }

            public float2 Position { get; }
            public quaternion Rotation { get; }
            public int2 Size { get; }
            public ZoneType Zone { get; }
            public Bounds2 Bounds { get; }
        }
    }
}
