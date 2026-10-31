using System;
using Colossal.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unsquare.Core.Naming;
using BuildingServiceUpgrade = Game.Buildings.ServiceUpgrade;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// A new on-lot extension (e.g. another car park storey) placed on a deformed building is created from its plain
    /// prefab; hands the building to <see cref="VariantSystem"/> so the extension gets the building's cage at once.
    /// </summary>
    public partial class ExtensionInstallSystem : GameSystemBase
    {
        private PrefabSystem _prefabSystem;
        private VariantSystem _variants;
        private EntityQuery _newExtensions;

        protected override void OnCreate()
        {
            base.OnCreate();
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            _variants = World.GetOrCreateSystemManaged<VariantSystem>();
            _newExtensions = GetEntityQuery(ComponentType.ReadOnly<Extension>(),
                ComponentType.ReadOnly<BuildingServiceUpgrade>(), ComponentType.ReadOnly<Owner>(),
                ComponentType.ReadOnly<Created>(), ComponentType.Exclude<Temp>(), ComponentType.Exclude<Deleted>());
            RequireForUpdate(_newExtensions);
        }

        protected override void OnUpdate()
        {
            try
            {
                using var owners = _newExtensions.ToComponentDataArray<Owner>(Allocator.Temp);
                foreach (var owner in owners)
                {
                    if (IsOnVariant(owner.m_Owner))
                        _variants.RequestExtensionRefresh(owner.m_Owner);
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Handing new upgrade extensions to the variant pipeline failed.");
            }
        }

        private bool IsOnVariant(Entity building) =>
            EntityManager.Exists(building) && EntityManager.HasComponent<Building>(building) &&
            EntityManager.TryGetComponent(building, out PrefabRef prefabRef) &&
            _prefabSystem.TryGetPrefab(prefabRef, out PrefabBase prefab) && prefab != null &&
            VariantNaming.IsVariantName(prefab.name);
    }
}
