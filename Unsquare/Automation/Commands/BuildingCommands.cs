#if UNSQUARE_AUTOMATION
using System;
using Colossal.Entities;
using Game.Buildings;
using Game.Prefabs;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Automation;
using Unsquare.Core.Cage;
using Unsquare.Core.Naming;
using Transform = Game.Objects.Transform;

namespace Unsquare.Automation.Commands
{
    /// <summary>Finding buildings for tests: by prefab name, position and kind.</summary>
    internal sealed class BuildingCommands
    {
        private readonly BridgeContext _context;

        public BuildingCommands(BridgeContext context) => _context = context;

        public void Register(RouteTable<BridgeHandler> routes)
        {
            routes.Add("GET buildings", Find);
            routes.Add("GET buildings/{ref}", Describe);
        }

        /// <summary>
        /// Query: prefab (substring, case-insensitive), near "x,z" with radius (default 100 m), variant (true/false),
        /// upgrades (true: has installed upgrades), limit (default 50). Nearest first when near is given.
        /// </summary>
        private CommandRun Find(BridgeArgs args)
        {
            if (!BridgeContext.InGame)
                return CommandRun.Fail(409, "Not in a loaded city.");
            var prefabFilter = args.String("prefab", null);
            var near = ParseNear(args.String("near", null));
            var radius = args.Float("radius", 100f);
            var limit = args.Int("limit", 50);
            var variantFilter = args.Has("variant") ? args.Bool("variant") : (bool?)null;
            var upgradesFilter = args.Has("upgrades") ? args.Bool("upgrades") : (bool?)null;

            var found = new System.Collections.Generic.List<(float Distance, JObject Json)>();
            using var all = _context.AllBuildings(Allocator.Temp);
            foreach (var building in all)
            {
                var name = _context.PrefabName(building) ?? "";
                if (prefabFilter != null && name.IndexOf(prefabFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var isVariant = VariantNaming.IsVariantName(name);
                if (variantFilter.HasValue && variantFilter.Value != isVariant)
                    continue;
                var hasUpgrades = _context.EntityManager.TryGetBuffer<InstalledUpgrade>(building, true, out var upgrades)
                                  && upgrades.Length > 0;
                if (upgradesFilter.HasValue && upgradesFilter.Value != hasUpgrades)
                    continue;
                var position = _context.EntityManager.GetComponentData<Transform>(building).m_Position;
                var distance = near.HasValue ? math.distance(position.xz, near.Value) : 0f;
                if (near.HasValue && distance > radius)
                    continue;
                found.Add((distance, Summary(building, name, isVariant, hasUpgrades)));
            }
            found.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            var list = new JArray();
            for (var i = 0; i < found.Count && i < limit; i++)
                list.Add(found[i].Json);
            return CommandRun.Done(new JObject { ["count"] = found.Count, ["buildings"] = list });
        }

        private CommandRun Describe(BridgeArgs args)
        {
            if (!_context.TryResolve(args.String("ref"), out var building, out var error))
                return CommandRun.Fail(404, error);
            var name = _context.PrefabName(building) ?? "";
            var hasUpgrades = _context.EntityManager.TryGetBuffer<InstalledUpgrade>(building, true, out var upgrades)
                              && upgrades.Length > 0;
            var json = Summary(building, name, VariantNaming.IsVariantName(name), hasUpgrades);
            if (_context.Deform.TryGetCage(building, _context.Session.EditMode, out var cage))
                json["cage"] = SessionJson.Cage(cage);
            return CommandRun.Done(json);
        }

        private JObject Summary(Entity building, string prefab, bool isVariant, bool hasUpgrades)
        {
            var transform = _context.EntityManager.GetComponentData<Transform>(building);
            var forward = math.rotate(transform.m_Rotation, new float3(0, 0, 1));
            var lotSize = _context.EntityManager.TryGetComponent<PrefabRef>(building, out var prefabRef) &&
                          _context.EntityManager.TryGetComponent<BuildingData>(prefabRef.m_Prefab, out var data)
                ? new JArray(data.m_LotSize.x, data.m_LotSize.y)
                : null;
            return new JObject
            {
                ["id"] = BridgeContext.Id(building),
                ["ref"] = _context.RefOf(building).ToString(),
                ["prefab"] = prefab,
                ["basePrefab"] = VariantNaming.TryParse(prefab, out var baseName) ? baseName : prefab,
                ["variant"] = isVariant,
                ["position"] = BridgeContext.Json(transform.m_Position),
                ["yawDegrees"] = BridgeContext.Round(math.degrees(math.atan2(forward.x, forward.z))),
                ["lotSize"] = lotSize,
                ["upgrades"] = hasUpgrades,
                ["deformable"] = _context.Deform.TryGetCage(building, CageTarget.Lot, out _),
            };
        }

        private static float2? ParseNear(string text)
        {
            if (text == null)
                return null;
            return BuildingRef.TryParse("x@" + text, out var reference)
                ? reference.Position
                : throw new BridgeArgumentException($"'near' must be \"x,z\", not '{text}'.");
        }
    }
}
#endif
