using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;
using Unsquare.Core.Editing;
using Unsquare.Core.Options;
using Unsquare.Core.Terrain;
using Unsquare.Integration.Entities;
using Unsquare.Integration.Systems;
using Transform = Game.Objects.Transform;

namespace Unsquare.Api
{
    /// <summary>Validates and clamps UI requests, then forwards them to the variant pipeline.</summary>
    internal sealed class DeformService : IBuildingDeformService
    {
        private const float DiagnosticMarginMeters = 50f;

        private readonly IVariantPipeline _pipeline;
        private readonly BaseTerrainSystem _terrain;
        private readonly NeighbourQuery _neighbours;
        private readonly EntityManager _entityManager;
        private readonly List<NeighbourEdge> _edges = new();
        private readonly List<Entity> _buildings = new();

        public DeformService(IVariantPipeline pipeline, BaseTerrainSystem terrain, NeighbourQuery neighbours,
            EntityManager entityManager)
        {
            _pipeline = pipeline;
            _terrain = terrain;
            _neighbours = neighbours;
            _entityManager = entityManager;
            _pipeline.BuildingChanged += building => CageChanged?.Invoke(building);
        }

        public event Action<Entity> CageChanged;

        public PipelineOptions Options => _pipeline.Options;

        public void Configure(PipelineOptions options) =>
            _pipeline.Options = options ?? throw new ArgumentNullException(nameof(options));

        public bool TryGetCage(Entity building, out CageState state)
        {
            state = default;
            if (!_pipeline.TryDescribe(building, out var description))
                return false;

            var applied = description.Applied;
            var target = _pipeline.TryGetRequested(building, out var requested) ? requested : applied;
            var status = MapStatus(_pipeline.GetStatus(building), applied);
            state = new CageState(building, description.LotBox, applied, target, status);
            return true;
        }

        public DeformRequestResult SetOffsets(Entity building, CageOffsets offsets)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            if (!TryGetCage(building, out var state))
                return DeformRequestResult.NotDeformable;
            return Apply(building, state.TargetSpec.WithOffsets(offsets));
        }

        public DeformRequestResult SetTerrainLift(Entity building, bool lift)
        {
            if (!TryGetCage(building, out var state))
                return DeformRequestResult.NotDeformable;
            if (!state.IsDeformed)
                return DeformRequestResult.Rejected;
            return Apply(building, state.TargetSpec.WithFlatTerrain(!lift));
        }

        public bool TryFitToGround(Entity building, GroundFitMode mode, Action<DeformSpec, DeformRequestResult> done)
        {
            if (done == null)
                throw new ArgumentNullException(nameof(done));
            if (!TryGetCage(building, out var state))
                return false;
            var points = GroundFit.GroundPoints(state.Box, state.TargetOffsets);
            return _terrain.TryRequest(building, points, heights =>
            {
                // The cage may have changed while the terrain was read; fit what it is now.
                if (heights == null || !TryGetCage(building, out var now))
                {
                    done(state.TargetSpec, DeformRequestResult.Rejected);
                    return;
                }
                var fitted = GroundFit.Fit(now.Box, now.TargetOffsets, heights, mode);
                done(now.TargetSpec, SetOffsets(building, fitted));
            });
        }

        public DeformRequestResult AlignToNeighbour(Entity building, AlignSide side, AlignMode mode,
            NeighbourAlignOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (!TryGetCage(building, out var state) ||
                !BuildingSpace.TryGetTransform(_entityManager, building, out var transform))
                return DeformRequestResult.NotDeformable;

            var ground = GroundFit.GroundPoints(state.Box, state.TargetOffsets);
            var area = new Bounds2(float.MaxValue, float.MinValue);
            foreach (var point in ground)
                area |= ToWorld(transform, point);
            // A margin beyond the reach, so a rejection can tell how far the nearest edge actually is.
            area = MathUtils.Expand(area, options.SearchMeters + DiagnosticMarginMeters);

            _edges.Clear();
            _buildings.Clear();
            _neighbours.CollectRoadEdges(transform, area, _edges);
            int roadPieces = _edges.Count;
            _neighbours.CollectBuildings(building, area, _buildings);
            foreach (var other in _buildings)
                AddLotEdges(transform, other);
            if (!NeighbourAlign.TryFindNeighbour(state.Box, state.TargetOffsets, side, _edges, options,
                    out var neighbour, out float nearest))
            {
                string nearestText = float.IsPositiveInfinity(nearest) ? "none" : $"{nearest:0.0} m";
                Mod.Log.Info($"Align {side} of building {building}: no neighbour within {options.SearchMeters} m " +
                    $"({roadPieces} road pieces, {_buildings.Count} lots nearby; nearest parallel edge: {nearestText}).");
                return DeformRequestResult.Rejected;
            }
            Mod.Log.Info($"Aligning {side} of building {building} to {neighbour} ({mode}).");
            return SetOffsets(building, NeighbourAlign.Align(state.Box, state.TargetOffsets, side, neighbour, mode));
        }

        // Ground outline of another lot (corners 0, 1, 5, 4 in turn), in the target's building space.
        private void AddLotEdges(in Transform target, Entity other)
        {
            if (!TryGetCage(other, out var state) ||
                !BuildingSpace.TryGetTransform(_entityManager, other, out var transform))
                return;
            var ground = GroundFit.GroundPoints(state.Box, state.TargetOffsets);
            var outline = new[] { ground[0], ground[1], ground[3], ground[2] };
            for (int i = 0; i < outline.Length; i++)
            {
                float2 a = ToBuilding(target, ToWorld(transform, outline[i]));
                float2 b = ToBuilding(target, ToWorld(transform, outline[(i + 1) % outline.Length]));
                _edges.Add(new NeighbourEdge(a, b, false));
            }
        }

        private static float2 ToWorld(in Transform transform, float2 local) =>
            BuildingSpace.PointToWorld(transform, new float3(local.x, 0f, local.y)).xz;

        private static float2 ToBuilding(in Transform transform, float2 world) =>
            BuildingSpace.PointToBuilding(transform, new float3(world.x, transform.m_Position.y, world.y)).xz;

        public DeformRequestResult Apply(Entity building, DeformSpec spec)
        {
            if (spec == null)
                throw new ArgumentNullException(nameof(spec));
            if (!_pipeline.TryDescribe(building, out var description))
                return DeformRequestResult.NotDeformable;

            var box = description.LotBox;
            var clamped = CageValidator.Clamp(box, spec.Offsets, Options.Limits);
            if (CageValidator.Validate(box, clamped, Options.Limits) != CageValidity.Valid)
                return DeformRequestResult.Rejected;
            _pipeline.Request(building, spec.WithOffsets(clamped));
            return clamped.Equals(spec.Offsets) ? DeformRequestResult.Accepted : DeformRequestResult.Clamped;
        }

        public DeformRequestResult MoveHandle(Entity building, CageHandle handle, float3 delta)
        {
            if (!TryGetCage(building, out var state))
                return DeformRequestResult.NotDeformable;
            return SetOffsets(building, state.TargetOffsets.Move(handle, delta));
        }

        public DeformRequestResult Reset(Entity building) => Apply(building, DeformSpec.Identity);

        public int ResetAll() => _pipeline.ResetAll();

        private static DeformStatus MapStatus(VariantStatus status, DeformSpec applied) => status switch
        {
            VariantStatus.Pending => DeformStatus.Pending,
            VariantStatus.WaitingForPrefab => DeformStatus.Applying,
            VariantStatus.Failed => DeformStatus.Failed,
            _ => applied.Offsets.IsIdentity ? DeformStatus.Undeformed : DeformStatus.Applied,
        };
    }
}
