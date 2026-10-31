using System.Collections.Generic;
using System.Text;
using Colossal.Entities;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using AreaSubArea = Game.Areas.SubArea;
using NetSubLane = Game.Net.SubLane;
using NetSubNet = Game.Net.SubNet;
using Transform = Game.Objects.Transform;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// Upgrade extensions of a building (e.g. an extra car park storey): own entities on the building's lot that
    /// the game places once from their prefab position and never moves along with the building.
    /// </summary>
    internal static class InstalledExtensions
    {
        private const float PlacementToleranceMeters = 0.1f;
        private const float SameRotationDot = 0.9999f;

        // Stand-alone upgrade buildings have their own lot and cage, so only extensions are collected.
        public static void Collect(EntityManager entityManager, Entity building, List<Entity> result)
        {
            result.Clear();
            if (!entityManager.TryGetBuffer(building, true, out DynamicBuffer<InstalledUpgrade> upgrades))
                return;
            foreach (var upgrade in upgrades)
            {
                var entity = upgrade.m_Upgrade;
                if (entityManager.Exists(entity) && entityManager.HasComponent<Extension>(entity) &&
                    entityManager.HasComponent<PrefabRef>(entity) && !entityManager.HasComponent<Building>(entity) &&
                    !entityManager.HasComponent<Deleted>(entity))
                    result.Add(entity);
            }
        }

        /// <summary>Whether the extension still sits where its prefab puts it (not moved by hand, same rotation).</summary>
        public static bool IsAtPrefabPlacement(EntityManager entityManager, Entity building, Entity extension,
            float3 prefabPosition, out float3 localOffset)
        {
            localOffset = default;
            if (!entityManager.TryGetComponent(building, out Transform owner) ||
                !entityManager.TryGetComponent(extension, out Transform placed))
                return false;
            localOffset = math.mul(math.inverse(owner.m_Rotation), placed.m_Position - owner.m_Position);
            bool sameRotation = math.abs(math.dot(owner.m_Rotation.value, placed.m_Rotation.value)) >= SameRotationDot;
            return sameRotation && math.distance(localOffset.xz, prefabPosition.xz) <= PlacementToleranceMeters;
        }

        /// <summary>One line per installed upgrade, for the log; null if there are none.</summary>
        public static string Describe(EntityManager entityManager, PrefabSystem prefabSystem, Entity building)
        {
            if (!entityManager.TryGetBuffer(building, true, out DynamicBuffer<InstalledUpgrade> upgrades) ||
                upgrades.Length == 0 || !entityManager.TryGetComponent(building, out Transform owner))
                return null;
            var text = new StringBuilder();
            foreach (var upgrade in upgrades)
            {
                var entity = upgrade.m_Upgrade;
                if (!entityManager.Exists(entity) || !entityManager.TryGetComponent(entity, out PrefabRef prefabRef))
                    continue;
                string name = prefabSystem.TryGetPrefab(prefabRef, out PrefabBase prefab) && prefab != null
                    ? $"{prefab.GetType().Name}/{prefab.name}"
                    : prefabRef.m_Prefab.ToString();
                string kind = entityManager.HasComponent<Building>(entity) ? "building"
                    : entityManager.HasComponent<Extension>(entity) ? "extension" : "other";
                text.Append($"\n  {entity} {name} ({kind}");
                if (prefab is BuildingExtensionPrefab extension)
                    text.Append($", prefab position {extension.m_Position}, external {extension.m_ExternalLot}");
                if (entityManager.TryGetComponent(entity, out Transform placed))
                    text.Append($", at {math.mul(math.inverse(owner.m_Rotation), placed.m_Position - owner.m_Position)}");
                text.Append($", nets {Count<NetSubNet>(entityManager, entity)}, areas {Count<AreaSubArea>(entityManager, entity)}, " +
                            $"lanes {Count<NetSubLane>(entityManager, entity)})");
            }
            return text.ToString();
        }

        private static int Count<T>(EntityManager entityManager, Entity entity) where T : unmanaged, IBufferElementData =>
            entityManager.TryGetBuffer(entity, true, out DynamicBuffer<T> buffer) ? buffer.Length : 0;
    }
}
