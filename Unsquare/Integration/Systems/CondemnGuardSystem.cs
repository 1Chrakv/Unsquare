using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Notifications;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unsquare.Core.Naming;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// Keeps deformed buildings from being condemned, e.g. when lot paths block zone cells or the lot was rezoned.
    /// Runs after ModificationEndBarrier, so simulation never sees <see cref="Condemned"/> on a variant.
    /// </summary>
    public partial class CondemnGuardSystem : GameSystemBase
    {
        private readonly Dictionary<Entity, bool> _isVariant = new();

        private PrefabSystem _prefabSystem;
        private IconCommandSystem _icons;
        private EntityQuery _condemned;
        private EntityQuery _buildingSettings;

        protected override void OnCreate()
        {
            base.OnCreate();
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            _icons = World.GetOrCreateSystemManaged<IconCommandSystem>();
            _condemned = GetEntityQuery(ComponentType.ReadOnly<Condemned>(), ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<PrefabRef>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            _buildingSettings = GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>());
            RequireForUpdate(_condemned);
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            // Prefab entities are created anew while loading.
            _isVariant.Clear();
        }

        protected override void OnUpdate()
        {
            try
            {
                Protect();
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Keeping deformed buildings from being condemned failed.");
            }
        }

        private void Protect()
        {
            using var entities = _condemned.ToEntityArray(Allocator.Temp);
            using var prefabRefs = _condemned.ToComponentDataArray<PrefabRef>(Allocator.Temp);
            using var protect = new NativeList<Entity>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                if (IsVariant(prefabRefs[i].m_Prefab))
                    protect.Add(entities[i]);
            }
            if (protect.Length == 0)
                return;

            EntityManager.RemoveComponent<Condemned>(protect.AsArray());
            // Written on the main thread, so the icon system needs no job handle.
            if (_buildingSettings.TryGetSingleton(out BuildingConfigurationData settings))
            {
                var icons = _icons.CreateCommandBuffer();
                foreach (var building in protect)
                    icons.Remove(building, settings.m_CondemnedNotification);
            }
            foreach (var building in protect)
                Mod.Log.Info($"Kept deformed building {building} from being condemned.");
        }

        private bool IsVariant(Entity prefab)
        {
            if (!_isVariant.TryGetValue(prefab, out bool isVariant))
            {
                isVariant = _prefabSystem.TryGetPrefab(prefab, out PrefabBase prefabBase) && prefabBase != null &&
                            VariantNaming.IsVariantName(prefabBase.name);
                _isVariant[prefab] = isVariant;
            }
            return isVariant;
        }
    }
}
