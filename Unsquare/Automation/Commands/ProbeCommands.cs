#if UNSQUARE_AUTOMATION
using System.Collections.Generic;
using Colossal.Entities;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.UI;
using Newtonsoft.Json.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Api;
using Unsquare.Core.Automation;
using Unsquare.Integration.Saves;
using Unsquare.UI.Overlay;
using AreaNode = Game.Areas.Node;
using AreaSubArea = Game.Areas.SubArea;
using NetSubNet = Game.Net.SubNet;
using SubObject = Game.Objects.SubObject;
using Transform = Game.Objects.Transform;

namespace Unsquare.Automation.Commands
{
    /// <summary>
    /// Read-only views of what a deformation moved: props, ground areas, paths, terrain, the building's state. Positions
    /// are given in world space and in building space (x width, y up, z street side) so tests can compare with cages.
    /// </summary>
    internal sealed class ProbeCommands
    {
        private readonly BridgeContext _context;

        public ProbeCommands(BridgeContext context) => _context = context;

        private EntityManager Entities => _context.EntityManager;

        public void Register(RouteTable<BridgeHandler> routes)
        {
            routes.Add("GET buildings/{ref}/info", Info);
            routes.Add("GET buildings/{ref}/subobjects", SubObjects);
            routes.Add("GET buildings/{ref}/areas", Areas);
            routes.Add("GET buildings/{ref}/nets", Nets);
            routes.Add("GET buildings/{ref}/lot", Lot);
            routes.Add("POST terrain/heights", TerrainHeights);
            routes.Add("GET buildings/{ref}/meshes", Meshes);
            routes.Add("GET overlay", Overlay);
        }

        /// <summary>
        /// The render meshes of the building's prefab with their bounds and those of every LOD, so tests can tell
        /// deformed meshes (baked clones with other bounds) from the base's.
        /// </summary>
        private CommandRun Meshes(BridgeArgs args) => WithBuilding(args, building =>
        {
            var list = new JArray();
            if (_context.Prefabs.TryGetPrefab(Entities.GetComponentData<PrefabRef>(building),
                    out ObjectGeometryPrefab prefab) && prefab != null && prefab.m_Meshes != null)
                foreach (var info in prefab.m_Meshes)
                {
                    if (info == null || !(info.m_Mesh is RenderPrefab render))
                        continue;
                    var lods = new JArray();
                    var lod = render.GetComponent<LodProperties>();
                    if (lod != null && lod.m_LodMeshes != null)
                        foreach (var level in lod.m_LodMeshes)
                            if (level != null)
                                lods.Add(MeshJson(level));
                    var json = MeshJson(render);
                    json["lods"] = lods;
                    list.Add(json);
                }
            return new JObject { ["prefab"] = _context.PrefabName(building), ["meshes"] = list };
        });

        private static JObject MeshJson(RenderPrefab render) => new()
        {
            ["name"] = render.name,
            ["asset"] = render.geometryAsset != null ? render.geometryAsset.name : null,
            ["min"] = BridgeContext.Json(render.bounds.min),
            ["max"] = BridgeContext.Json(render.bounds.max),
            ["vertices"] = render.vertexCount,
        };

        /// <summary>Sizes the cage overlay draws with now (from the options).</summary>
        private CommandRun Overlay(BridgeArgs args)
        {
            var style = _context.World.GetOrCreateSystemManaged<CageOverlaySystem>().Style;
            return CommandRun.Done(new JObject
            {
                ["lineRadius"] = style.LineRadius,
                ["handleDiameter"] = style.HandleDiameter,
                ["selectedHandleDiameter"] = style.SelectedHandleDiameter,
                ["axisLength"] = style.AxisLength,
                ["gizmoScreenSize"] = style.GizmoScreenSize,
            });
        }

        private CommandRun Info(BridgeArgs args) => WithBuilding(args, building =>
        {
            var prefab = Entities.GetComponentData<PrefabRef>(building).m_Prefab;
            var upgrades = new JArray();
            if (Entities.TryGetBuffer<InstalledUpgrade>(building, true, out var installed))
                foreach (var upgrade in installed)
                    upgrades.Add(new JObject
                    {
                        ["id"] = BridgeContext.Id(upgrade.m_Upgrade),
                        ["prefab"] = _context.PrefabName(upgrade.m_Upgrade),
                    });
            // Upgrades the building's menu offers: a cloned upgrade must never show up here.
            var options = new JArray();
            if (Entities.TryGetBuffer<BuildingUpgradeElement>(prefab, true, out var offered))
                foreach (var option in offered)
                    options.Add(_context.Prefabs.TryGetPrefab(option.m_Upgrade, out PrefabBase upgradePrefab) &&
                                upgradePrefab != null
                        ? upgradePrefab.name
                        : BridgeContext.Id(option.m_Upgrade));
            return new JObject
            {
                ["prefab"] = _context.PrefabName(building),
                ["name"] = _context.World.GetOrCreateSystemManaged<NameSystem>().GetRenderedLabelName(building),
                ["upgradeOptions"] = options,
                ["level"] = Entities.TryGetComponent<SpawnableBuildingData>(prefab, out var spawnable)
                    ? spawnable.m_Level
                    : (int?)null,
                ["renters"] = Entities.TryGetBuffer<Renter>(building, true, out var renters) ? renters.Length : 0,
                ["condemned"] = Entities.HasComponent<Condemned>(building),
                ["abandoned"] = Entities.HasComponent<Abandoned>(building),
                ["underConstruction"] = Entities.HasComponent<Game.Objects.UnderConstruction>(building),
                ["savedCage"] = Entities.HasComponent<SavedCage>(building),
                ["upgrades"] = upgrades,
            };
        });

        /// <summary>Props and attached sub-buildings: prefab, position and the directions of their local axes.</summary>
        private CommandRun SubObjects(BridgeArgs args) => WithBuilding(args, building =>
        {
            var transform = Entities.GetComponentData<Transform>(building);
            var list = new JArray();
            if (Entities.TryGetBuffer<SubObject>(building, true, out var subObjects))
                foreach (var sub in subObjects)
                {
                    if (!Entities.TryGetComponent<Transform>(sub.m_SubObject, out var own))
                        continue;
                    list.Add(new JObject
                    {
                        ["id"] = BridgeContext.Id(sub.m_SubObject),
                        ["prefab"] = _context.PrefabName(sub.m_SubObject),
                        ["world"] = BridgeContext.Json(own.m_Position),
                        ["local"] = BridgeContext.Json(BuildingSpace.PointToBuilding(transform, own.m_Position)),
                        ["forward"] = BridgeContext.Json(BuildingSpace.DirectionToBuilding(transform,
                            math.rotate(own.m_Rotation, new float3(0, 0, 1)))),
                        ["up"] = BridgeContext.Json(BuildingSpace.DirectionToBuilding(transform,
                            math.rotate(own.m_Rotation, new float3(0, 1, 0)))),
                    });
                }
            return new JObject { ["count"] = list.Count, ["subObjects"] = list };
        });

        /// <summary>Ground areas (paving, grass, …) owned by the building, with their outline points.</summary>
        private CommandRun Areas(BridgeArgs args) => WithBuilding(args, building =>
        {
            var transform = Entities.GetComponentData<Transform>(building);
            var list = new JArray();
            if (Entities.TryGetBuffer<AreaSubArea>(building, true, out var areas))
                foreach (var area in areas)
                {
                    if (!Entities.TryGetBuffer<AreaNode>(area.m_Area, true, out var nodes))
                        continue;
                    var points = new JArray();
                    foreach (var node in nodes)
                        points.Add(BridgeContext.Json(BuildingSpace.PointToBuilding(transform, node.m_Position)));
                    list.Add(new JObject
                    {
                        ["id"] = BridgeContext.Id(area.m_Area),
                        ["prefab"] = _context.PrefabName(area.m_Area),
                        ["local"] = points,
                    });
                }
            return new JObject { ["count"] = list.Count, ["areas"] = list };
        });

        /// <summary>
        /// Paths and other nets owned by the building: nodes (with how many edges meet there, owned or not) and edges
        /// (their end nodes), so tests can check positions and that nothing came apart.
        /// </summary>
        private CommandRun Nets(BridgeArgs args) => WithBuilding(args, building =>
        {
            var transform = Entities.GetComponentData<Transform>(building);
            var nodes = new Dictionary<Entity, JObject>();
            var edges = new JArray();
            if (Entities.TryGetBuffer<NetSubNet>(building, true, out var subNets))
                foreach (var subNet in subNets)
                {
                    var net = subNet.m_SubNet;
                    if (Entities.TryGetComponent<Edge>(net, out var edge))
                    {
                        AddNode(edge.m_Start);
                        AddNode(edge.m_End);
                        edges.Add(new JObject
                        {
                            ["id"] = BridgeContext.Id(net),
                            ["prefab"] = _context.PrefabName(net),
                            ["start"] = BridgeContext.Id(edge.m_Start),
                            ["end"] = BridgeContext.Id(edge.m_End),
                            ["length"] = Entities.TryGetComponent<Curve>(net, out var curve)
                                ? BridgeContext.Round(curve.m_Length)
                                : (float?)null,
                        });
                    }
                    else if (Entities.HasComponent<Node>(net))
                        AddNode(net);
                }

            void AddNode(Entity node)
            {
                if (nodes.ContainsKey(node) || !Entities.TryGetComponent<Node>(node, out var data))
                    return;
                nodes[node] = new JObject
                {
                    ["id"] = BridgeContext.Id(node),
                    ["local"] = BridgeContext.Json(BuildingSpace.PointToBuilding(transform, data.m_Position)),
                    ["edges"] = Entities.TryGetBuffer<ConnectedEdge>(node, true, out var connected) ? connected.Length : 0,
                    ["deleted"] = Entities.HasComponent<Deleted>(node),
                };
            }

            return new JObject { ["nodes"] = new JArray(nodes.Values), ["edges"] = edges };
        });

        /// <summary>The lot's edge heights the game blends the terrain with (relative to the building).</summary>
        private CommandRun Lot(BridgeArgs args) => WithBuilding(args, building =>
            Entities.TryGetComponent<Lot>(building, out var lot)
                ? new JObject
                {
                    ["front"] = BridgeContext.Json(lot.m_FrontHeights),
                    ["right"] = BridgeContext.Json(lot.m_RightHeights),
                    ["back"] = BridgeContext.Json(lot.m_BackHeights),
                    ["left"] = BridgeContext.Json(lot.m_LeftHeights),
                }
                : new JObject());

        /// <summary>
        /// Current terrain heights (with lots and roads applied). Body: points [[x, z], …]; with building, points are
        /// in its space and heights are relative to its ground, otherwise world xz and world heights.
        /// </summary>
        private CommandRun TerrainHeights(BridgeArgs args)
        {
            Transform? transform = null;
            if (args.Has("building"))
            {
                if (!_context.TryResolve(args.String("building"), out var building, out var error))
                    return CommandRun.Fail(404, error);
                transform = Entities.GetComponentData<Transform>(building);
            }
            var points = args.Points("points");
            var terrain = _context.World.GetOrCreateSystemManaged<TerrainSystem>();
            var data = terrain.GetHeightData(true);
            var heights = new JArray();
            foreach (var point in points)
            {
                var world = transform.HasValue
                    ? BuildingSpace.PointToWorld(transform.Value, new float3(point.x, 0, point.y))
                    : new float3(point.x, 0, point.y);
                var height = TerrainUtils.SampleHeight(ref data, world);
                heights.Add(BridgeContext.Round(transform.HasValue ? height - transform.Value.m_Position.y : height));
            }
            return CommandRun.Done(new JObject { ["heights"] = heights });
        }

        private CommandRun WithBuilding(BridgeArgs args, System.Func<Entity, JToken> probe)
        {
            if (!BridgeContext.InGame)
                return CommandRun.Fail(409, "Not in a loaded city.");
            return _context.TryResolve(args.String("ref"), out var building, out var error)
                ? CommandRun.Done(probe(building))
                : CommandRun.Fail(404, error);
        }
    }
}
#endif
