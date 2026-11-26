#if UNSQUARE_AUTOMATION
using System.Collections.Generic;
using Colossal.Entities;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Simulation;
using Game.Tools;
using Game.Zones;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Api;
using Unsquare.Core.Automation;
using Transform = Game.Objects.Transform;

namespace Unsquare.Automation.Commands
{
    /// <summary>
    /// Test-only changes to the city that the suite can't make through the edit session: zoning, letting the
    /// simulation run, a level-up and the game language. They change data the game's own systems then act on.
    /// </summary>
    internal sealed class CityCommands
    {
        private const string NoZone = "None";
        private readonly BridgeContext _context;
        private EntityQuery _blocks;
        private EntityQuery _zones;

        public CityCommands(BridgeContext context) => _context = context;

        private EntityManager Entities => _context.EntityManager;

        public void Register(RouteTable<BridgeHandler> routes)
        {
            routes.Add("GET buildings/{ref}/zone", Zone);
            routes.Add("POST zone/paint", Paint);
            routes.Add("POST game/simulate", Simulate);
            routes.Add("POST buildings/{ref}/levelUp", LevelUp);
            routes.Add("POST game/locale", Locale);
        }

        /// <summary>Zone cells under the building's lot and in the ring of one cell around it, counted by zone.</summary>
        private CommandRun Zone(BridgeArgs args)
        {
            if (!TryBuilding(args.String("ref"), out var building, out var fail))
                return fail;
            var transform = Entities.GetComponentData<Transform>(building);
            var half = LotHalfSize(building);
            var inside = new Dictionary<string, int>();
            var ring = new Dictionary<string, int>();
            ForEachCell((block, cells, index, world) =>
            {
                var local = BuildingSpace.PointToBuilding(transform, world);
                var outside = math.max(math.abs(local.x) - half.x, math.abs(local.z) - half.y);
                var counts = outside < 0 ? inside : outside < ZoneUtils.CELL_SIZE ? ring : null;
                if (counts == null || (cells[index].m_State & CellFlags.Visible) == 0)
                    return false;
                var name = ZoneName(cells[index].m_Zone);
                counts[name] = counts.TryGetValue(name, out var n) ? n + 1 : 1;
                return false;
            });
            return CommandRun.Done(new JObject { ["inside"] = JObject.FromObject(inside), ["ring"] = JObject.FromObject(ring) });
        }

        /// <summary>
        /// Sets the zone of every visible cell whose centre lies within radius (default 3 m) of one of the points.
        /// Body: zone (prefab name or "None"), points [[x, z], …] (building space with building, else world),
        /// radius, onlyEmpty (skip occupied cells).
        /// </summary>
        private CommandRun Paint(BridgeArgs args)
        {
            if (!BridgeContext.InGame)
                return CommandRun.Fail(409, "Not in a loaded city.");
            Transform? transform = null;
            if (args.Has("building"))
            {
                if (!TryBuilding(args.String("building"), out var building, out var fail))
                    return fail;
                transform = Entities.GetComponentData<Transform>(building);
            }
            var zoneName = args.String("zone");
            if (!TryZoneType(zoneName, out var zone))
                return CommandRun.Fail(400, $"Unknown zone '{zoneName}'.");
            var radius = args.Float("radius", 3f);
            var onlyEmpty = args.Bool("onlyEmpty", false);
            var points = new List<float2>();
            foreach (var point in args.Points("points"))
                points.Add(transform.HasValue
                    ? BuildingSpace.PointToWorld(transform.Value, new float3(point.x, 0, point.y)).xz
                    : point);

            int painted = 0;
            var touched = new HashSet<Entity>();
            ForEachCell((block, cells, index, world) =>
            {
                var cell = cells[index];
                if ((cell.m_State & CellFlags.Visible) == 0 || cell.m_Zone.Equals(zone) ||
                    (onlyEmpty && (cell.m_State & CellFlags.Occupied) != 0))
                    return false;
                foreach (var point in points)
                {
                    if (math.distance(point, world.xz) > radius)
                        continue;
                    cell.m_Zone = zone;
                    cells[index] = cell;
                    painted++;
                    touched.Add(block);
                    return true;
                }
                return false;
            });
            foreach (var block in touched)
                Entities.AddComponent<Updated>(block);
            Mod.Log.Info($"[Auto] Painted {painted} zone cells {zoneName} in {touched.Count} blocks");
            return CommandRun.Done(new JObject { ["cells"] = painted, ["blocks"] = touched.Count });
        }

        /// <summary>
        /// Runs the simulation for a number of frames (or days: 262144 frames each) at the given speed (default 4, the
        /// fastest), then pauses it. Body: frames | days, speed.
        /// </summary>
        private CommandRun Simulate(BridgeArgs args)
        {
            var simulation = _context.World.GetExistingSystemManaged<SimulationSystem>();
            if (simulation == null || !BridgeContext.InGame)
                return CommandRun.Fail(409, "Not in a loaded city.");
            var frames = args.Has("days") ? (uint)(args.Float("days") * TimeSystem.kTicksPerDay) : (uint)args.Int("frames");
            var start = simulation.frameIndex;
            simulation.selectedSpeed = args.Float("speed", 4f);
            return CommandRun.Pending(() =>
            {
                if (!BridgeContext.InGame)
                    return CommandRun.Fail(409, "The city was left while simulating.");
                if (simulation.frameIndex - start < frames)
                    return null;
                simulation.selectedSpeed = 0;
                return CommandRun.Done(new JObject { ["frames"] = simulation.frameIndex - start });
            });
        }

        /// <summary>
        /// Makes a zoned building level up through the game's own path: its condition is raised to the leveling cost,
        /// the materials the game then orders count as delivered, and the game's level-up runs. Runs the simulation
        /// until the building is under construction for the next level, then pauses. Body: maxFrames.
        /// </summary>
        private CommandRun LevelUp(BridgeArgs args)
        {
            if (!TryBuilding(args.String("ref"), out var building, out var fail))
                return fail;
            if (!Entities.HasComponent<BuildingCondition>(building))
                return CommandRun.Fail(422, "The building has no condition (not a zoned building).");
            Entities.SetComponentData(building, new BuildingCondition { m_Condition = int.MaxValue / 4 });
            Mod.Log.Info($"[Auto] Levelling up building {building}");
            var simulation = _context.World.GetExistingSystemManaged<SimulationSystem>();
            simulation.selectedSpeed = 4f;
            var deadline = UnityEngine.Time.frameCount + args.Int("maxFrames", 36000);
            return CommandRun.Pending(() =>
            {
                if (!Entities.Exists(building))
                    return CommandRun.Fail(404, "The building is gone.");
                if (Entities.HasComponent<Game.Objects.UnderConstruction>(building))
                {
                    simulation.selectedSpeed = 0f;
                    return CommandRun.Done(new JObject { ["underConstruction"] = true });
                }
                if (Entities.TryGetBuffer<ResourceNeeding>(building, false, out var needs))
                    for (int i = 0; i < needs.Length; i++)
                    {
                        var need = needs[i];
                        need.m_Flags = ResourceNeedingFlags.Delivered;
                        needs[i] = need;
                    }
                if (UnityEngine.Time.frameCount <= deadline)
                    return null;
                simulation.selectedSpeed = 0f;
                return CommandRun.Fail(504, "The building did not start levelling up.");
            });
        }

        /// <summary>Switches the game language. Body: locale (e.g. "de-DE"); returns the previous one.</summary>
        private static CommandRun Locale(BridgeArgs args)
        {
            var localization = GameManager.instance?.localizationManager;
            if (localization == null)
                return CommandRun.Fail(409, "No localization manager.");
            var previous = localization.activeLocaleId;
            localization.SetActiveLocale(args.String("locale"));
            return CommandRun.Done(new JObject { ["previous"] = previous, ["locale"] = localization.activeLocaleId });
        }

        // Calls visit for each cell of each zone block with its world position; visit returns true if it changed it.
        private void ForEachCell(System.Func<Entity, DynamicBuffer<Cell>, int, float3, bool> visit)
        {
            if (_blocks == default)
                _blocks = Entities.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Block>(), ComponentType.ReadWrite<Cell>() },
                    None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() },
                });
            using var blocks = _blocks.ToEntityArray(Allocator.Temp);
            foreach (var entity in blocks)
            {
                var block = Entities.GetComponentData<Block>(entity);
                var cells = Entities.GetBuffer<Cell>(entity);
                for (int y = 0; y < block.m_Size.y; y++)
                for (int x = 0; x < block.m_Size.x; x++)
                {
                    var index = y * block.m_Size.x + x;
                    if (index < cells.Length)
                        visit(entity, cells, index, ZoneUtils.GetCellPosition(block, new int2(x, y)));
                }
            }
        }

        private bool TryZoneType(string name, out ZoneType zone)
        {
            zone = ZoneType.None;
            if (name == NoZone)
                return true;
            foreach (var (prefabName, type) in ZoneTypes())
                if (prefabName == name)
                {
                    zone = type;
                    return true;
                }
            return false;
        }

        private string ZoneName(ZoneType zone)
        {
            if (zone.Equals(ZoneType.None))
                return NoZone;
            foreach (var (name, type) in ZoneTypes())
                if (type.Equals(zone))
                    return name;
            return $"#{zone.m_Index}";
        }

        private IEnumerable<(string, ZoneType)> ZoneTypes()
        {
            if (_zones == default)
                _zones = Entities.CreateEntityQuery(ComponentType.ReadOnly<ZoneData>(), ComponentType.ReadOnly<PrefabData>());
            using var prefabs = _zones.ToEntityArray(Allocator.Temp);
            var list = new List<(string, ZoneType)>(prefabs.Length);
            foreach (var entity in prefabs)
                if (_context.Prefabs.TryGetPrefab(Entities.GetComponentData<PrefabData>(entity), out PrefabBase prefab) &&
                    prefab != null)
                    list.Add((prefab.name, Entities.GetComponentData<ZoneData>(entity).m_ZoneType));
            return list;
        }

        // Half the lot's width and depth (8 m cells) in building space.
        private float2 LotHalfSize(Entity building)
        {
            var prefab = Entities.GetComponentData<PrefabRef>(building).m_Prefab;
            return Entities.TryGetComponent<BuildingData>(prefab, out var data)
                ? (float2)data.m_LotSize * ZoneUtils.CELL_SIZE / 2f
                : float2.zero;
        }

        private bool TryBuilding(string text, out Entity building, out CommandRun fail)
        {
            fail = null;
            building = Entity.Null;
            if (!BridgeContext.InGame)
            {
                fail = CommandRun.Fail(409, "Not in a loaded city.");
                return false;
            }
            if (_context.TryResolve(text, out building, out var error))
                return true;
            fail = CommandRun.Fail(404, error);
            return false;
        }
    }
}
#endif
