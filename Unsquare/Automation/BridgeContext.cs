#if UNSQUARE_AUTOMATION
using System.Collections.Generic;
using System.Globalization;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Tools;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Api;
using Unsquare.Core.Automation;
using Unsquare.Core.Naming;
using Unsquare.UI.Editing;
using Unsquare.UI.Tool;
using Transform = Game.Objects.Transform;

namespace Unsquare.Automation
{
    /// <summary>What every bridge command needs: the world's systems, the edit session and building lookups.</summary>
    internal sealed class BridgeContext
    {
        private readonly EntityQuery _buildings;

        public BridgeContext(World world, EntityQuery buildings)
        {
            World = world;
            _buildings = buildings;
            Session = world.GetOrCreateSystemManaged<CageEditorSystem>().Session;
            Tool = world.GetOrCreateSystemManaged<UnsquareToolSystem>();
            Prefabs = world.GetOrCreateSystemManaged<PrefabSystem>();
            Deform = UnsquareApi.GetDeformService(world);
        }

        public World World { get; }
        public EntityManager EntityManager => World.EntityManager;
        public CageEditSession Session { get; }
        public UnsquareToolSystem Tool { get; }
        public PrefabSystem Prefabs { get; }
        public IBuildingDeformService Deform { get; }

        public static bool InGame => GameManager.instance != null && GameManager.instance.gameMode.IsGame() &&
                                     !GameManager.instance.isGameLoading;

        public string PrefabName(Entity entity) =>
            EntityManager.HasComponent<PrefabRef>(entity) &&
            Prefabs.TryGetPrefab(EntityManager.GetComponentData<PrefabRef>(entity), out PrefabBase prefab) &&
            prefab != null
                ? prefab.name
                : null;

        public BuildingRef RefOf(Entity building)
        {
            // Refs name the base prefab, so a building keeps its ref while its variant changes.
            var position = EntityManager.GetComponentData<Transform>(building).m_Position;
            var name = PrefabName(building) ?? "?";
            return new BuildingRef(VariantNaming.TryParse(name, out var baseName) ? baseName : name, position.xz);
        }

        /// <summary>Every placed building (no previews, nothing deleted).</summary>
        public NativeArray<Entity> AllBuildings(Allocator allocator) => _buildings.ToEntityArray(allocator);

        /// <summary>
        /// Resolves "Prefab@x,z" (nearest within 2 m) or "#index:version" to a building; false with a message if none.
        /// </summary>
        public bool TryResolve(string text, out Entity building, out string error)
        {
            building = Entity.Null;
            error = null;
            if (text != null && text.StartsWith("#"))
            {
                var parts = text.Substring(1).Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out var index) && int.TryParse(parts[1], out var version))
                {
                    var entity = new Entity { Index = index, Version = version };
                    // Extensions (a car park's storey) are their own entities without Building; probes read them too.
                    if (EntityManager.Exists(entity) && (EntityManager.HasComponent<Building>(entity) ||
                                                         EntityManager.HasComponent<Extension>(entity)))
                    {
                        building = entity;
                        return true;
                    }
                }
                error = $"No building {text}.";
                return false;
            }
            if (!BuildingRef.TryParse(text, out var reference))
            {
                error = $"'{text}' is neither Prefab@x,z nor #index:version.";
                return false;
            }
            using var all = AllBuildings(Allocator.Temp);
            var candidates = new List<(string, float2)>(all.Length);
            foreach (var entity in all)
                candidates.Add((PrefabName(entity) ?? "", EntityManager.GetComponentData<Transform>(entity).m_Position.xz));
            var found = reference.FindIn(candidates);
            if (found < 0)
            {
                error = $"No {reference.Prefab} within {BuildingRef.DefaultTolerance} m of {reference}.";
                return false;
            }
            building = all[found];
            return true;
        }

        public static string Id(Entity entity) => $"#{entity.Index}:{entity.Version}";

        public static JArray Json(float3 v) => new(Round(v.x), Round(v.y), Round(v.z));

        public static float Round(float value) =>
            float.Parse(value.ToString("0.###", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        public static EntityQueryDesc BuildingQuery => new()
        {
            All = new[] { ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<Transform>() },
            None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() },
        };
    }
}
#endif
