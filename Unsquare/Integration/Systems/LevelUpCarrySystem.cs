using System;
using Colossal.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// A levelling growable gets <see cref="UnderConstruction"/> naming a plain prefab one level up, and keeps its
    /// variant until the game swaps it (at least one frame later). Hands such buildings to <see cref="VariantSystem"/>.
    /// </summary>
    public partial class LevelUpCarrySystem : GameSystemBase
    {
        private VariantSystem _variants;
        private EntityQuery _levelling;

        protected override void OnCreate()
        {
            base.OnCreate();
            _variants = World.GetOrCreateSystemManaged<VariantSystem>();
            _levelling = GetEntityQuery(ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<UnderConstruction>(), ComponentType.Exclude<Destroyed>(),
                ComponentType.Exclude<Temp>(), ComponentType.Exclude<Deleted>());
            RequireForUpdate(_levelling);
        }

        protected override void OnUpdate()
        {
            try
            {
                using var buildings = _levelling.ToEntityArray(Allocator.Temp);
                using var prefabRefs = _levelling.ToComponentDataArray<PrefabRef>(Allocator.Temp);
                using var constructions = _levelling.ToComponentDataArray<UnderConstruction>(Allocator.Temp);
                for (int i = 0; i < buildings.Length; i++)
                {
                    Entity from = prefabRefs[i].m_Prefab;
                    Entity target = constructions[i].m_NewPrefab;
                    if (target == Entity.Null || target == from || !SameLot(EntityManager, from, target) ||
                        _variants.TryGetVariantCage(target, out _, out _) ||
                        !_variants.TryGetVariantCage(from, out var lotBox, out var spec))
                        continue;
                    _variants.PrepareLevelUp(buildings[i], from, target, lotBox, spec);
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Handing levelling buildings to the variant pipeline failed.");
            }
        }

        // The game only levels up within a lot size; anything else is not a level-up we understand.
        internal static bool SameLot(EntityManager entityManager, Entity from, Entity target) =>
            entityManager.TryGetComponent(from, out BuildingData fromData) &&
            entityManager.TryGetComponent(target, out BuildingData targetData) &&
            fromData.m_LotSize.Equals(targetData.m_LotSize);
    }
}
