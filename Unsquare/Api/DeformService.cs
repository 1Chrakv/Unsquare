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
    /// <summary>
    /// Validates and clamps UI requests, then forwards them to the variant pipeline. The UI works on views of the two
    /// cages; the pipeline names and bakes both on the lot box, so edits are converted here (<see cref="SplitCageEdit"/>).
    /// </summary>
    internal sealed class DeformService : IBuildingDeformService
    {
        private const float DiagnosticMarginMeters = 50f;

        private readonly IVariantPipeline _pipeline;
        private readonly BaseTerrainSystem _terrain;
        private readonly NeighbourQuery _neighbours;
        private readonly EntityManager _entityManager;
        private readonly List<NeighbourEdge> _edges = new();
        private readonly List<Line3.Segment> _roadPieces = new();
        private readonly List<Entity> _buildings = new();
        private readonly Dictionary<Entity, EditOffsets> _editOffsets = new();

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

        public bool TryGetCage(Entity building, CageTarget target, out CageState state)
        {
            state = default;
            if (!_pipeline.TryDescribe(building, out var description))
                return false;

            target = EffectiveTarget(description, target);
            var applied = description.Applied;
            var requested = _pipeline.TryGetRequested(building, out var pending) ? pending : applied;
            var status = MapStatus(_pipeline.GetStatus(building), applied);
            if (_editOffsets.TryGetValue(building, out var cached) && !cached.Matches(description.EditBox, requested) &&
                !cached.Matches(description.EditBox, applied))
                _editOffsets.Remove(building);

            var ghostBox = target == CageTarget.Building ? description.LotBox : description.BuildingEditBox;
            var ghost = requested.IsLinked ? null : View(building, description, OtherOf(target), requested);
            state = new CageState(building, target, BoxOf(description, target),
                View(building, description, target, applied), View(building, description, target, requested),
                applied, requested, status, description.LotBox, ghostBox, ghost, description.HasOwnBuildingBox,
                BuildingSeat.IsOffGround(description.LotBox, description.BuildingBox, requested));
            return true;
        }

        // Flat or meshless lots (parks) have no building of their own: their only cage is the lot's.
        private static CageTarget EffectiveTarget(in BuildingDescription description, CageTarget target) =>
            description.HasOwnBuildingBox ? target : CageTarget.Both;

        // The latest request if one is in flight, otherwise the applied deformation; both cages on the lot box.
        private bool TryDescribeTarget(Entity building, out BuildingDescription description, out DeformSpec spec)
        {
            spec = null;
            if (!_pipeline.TryDescribe(building, out description))
                return false;
            spec = _pipeline.TryGetRequested(building, out var pending) ? pending : description.Applied;
            return true;
        }

        private static CageTarget OtherOf(CageTarget target) =>
            target == CageTarget.Building ? CageTarget.Lot : CageTarget.Building;

        private static CageBox BoxOf(in BuildingDescription description, CageTarget target) => target switch
        {
            CageTarget.Building => description.BuildingEditBox,
            CageTarget.Lot => description.LotBox,
            _ => description.EditBox,
        };

        // Both prefers the edit offsets last sent: converting the lot offsets back could swallow a one-step nudge.
        private CageOffsets View(Entity building, in BuildingDescription description, CageTarget target,
            DeformSpec spec)
        {
            switch (target)
            {
                case CageTarget.Building:
                    return SplitCageEdit.View(description.LotBox, spec.BuildingOffsets, description.BuildingEditBox);
                case CageTarget.Lot:
                    return spec.LotOffsets;
                default:
                    if (_editOffsets.TryGetValue(building, out var cached) && cached.Matches(description.EditBox, spec))
                        return cached.Edit;
                    return SplitCageEdit.View(description.LotBox, spec.LotOffsets, description.EditBox);
            }
        }

        public DeformRequestResult SetOffsets(Entity building, in CageState start, CageOffsets view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));
            if (!_pipeline.TryDescribe(building, out var description))
                return DeformRequestResult.NotDeformable;

            var lotBox = description.LotBox;
            var buildingBox = description.BuildingEditBox;
            switch (EffectiveTarget(description, start.Target))
            {
                case CageTarget.Building:
                    return Request(building, description,
                        SplitCageEdit.EditBuilding(lotBox, buildingBox, start.TargetSpec, start.TargetOffsets, view));
                case CageTarget.Lot:
                    return Request(building, description,
                        SplitCageEdit.EditLot(lotBox, buildingBox, start.TargetSpec, view));
                default:
                    var lot = ExtendedCage.Convert(description.EditBox, view, lotBox);
                    var result = Request(building, description,
                        SplitCageEdit.EditBoth(lotBox, buildingBox, start.TargetSpec, lot), out var requested);
                    if (!description.EditBox.Equals(lotBox) &&
                        (result == DeformRequestResult.Accepted || result == DeformRequestResult.Clamped))
                    {
                        var edit = requested.LotOffsets.Equals(lot)
                            ? view
                            : ExtendedCage.Convert(lotBox, requested.LotOffsets, description.EditBox);
                        _editOffsets[building] = new EditOffsets(description.EditBox, requested.LotOffsets, edit);
                    }
                    return result;
            }
        }

        public DeformRequestResult SetBlendMode(Entity building, BlendMode mode)
        {
            if (!TryDescribeTarget(building, out var description, out var spec))
                return DeformRequestResult.NotDeformable;
            if (spec.IsLinked)
                return DeformRequestResult.Rejected;
            return Request(building, description, spec.WithBlendMode(mode));
        }

        public DeformRequestResult SetTerrainLift(Entity building, bool lift)
        {
            if (!TryDescribeTarget(building, out var description, out var spec))
                return DeformRequestResult.NotDeformable;
            if (spec.LotOffsets.IsIdentity)
                return DeformRequestResult.Rejected;
            return Request(building, description, spec.WithFlatTerrain(!lift));
        }

        public bool TryFitToGround(Entity building, CageTarget target, GroundFitMode mode,
            Action<DeformSpec, DeformRequestResult> done)
        {
            if (done == null)
                throw new ArgumentNullException(nameof(done));
            if (!TryGetCage(building, target, out var state))
                return false;
            if (target == CageTarget.Building)
            {
                // Seats the building on its lot: the lot cage's ground under each column is the target.
                var heights = BuildingSeat.TargetHeights(state.LotBox, state.TargetSpec.LotOffsets, state.Box);
                var seated = GroundFit.Fit(state.Box, state.TargetOffsets, heights, GroundFitMode.AllCorners);
                done(state.TargetSpec, SetOffsets(building, state, seated));
                return true;
            }
            var points = GroundFit.GroundPoints(state.Box, state.TargetOffsets);
            return _terrain.TryRequest(building, points, heights =>
            {
                // The cage may have changed while the terrain was read; fit what it is now.
                if (heights == null || !TryGetCage(building, target, out var now))
                {
                    done(state.TargetSpec, DeformRequestResult.Rejected);
                    return;
                }
                var fitted = GroundFit.Fit(now.Box, now.TargetOffsets, heights, mode);
                done(now.TargetSpec, SetOffsets(building, now, fitted));
            });
        }

        public DeformRequestResult AlignToNeighbour(Entity building, CageTarget target, AlignSide side, AlignMode mode,
            NeighbourAlignOptions options)
        {
            if (!TryFindNeighbour(building, target, side, options, out var state, out var neighbour, out var result))
                return result;
            Mod.Log.Info($"Aligning {side} of building {building} ({target}) to {neighbour} ({mode}).");
            return SetOffsets(building, state,
                NeighbourAlign.Align(state.Box, state.TargetOffsets, side, neighbour, mode));
        }

        public DeformRequestResult AlignGroundToNeighbour(Entity building, CageTarget target, AlignSide side,
            NeighbourAlignOptions options)
        {
            if (!TryFindNeighbour(building, target, side, options, out var state, out var neighbour, out var result))
                return result;
            Mod.Log.Info($"Levelling {side} of building {building} ({target}) with {neighbour} " +
                $"({neighbour.HeightA:0.0} .. {neighbour.HeightB:0.0} m).");
            return SetOffsets(building, state,
                NeighbourAlign.FitHeight(state.Box, state.TargetOffsets, side, neighbour));
        }

        // The edge NeighbourAlign picks for the side among nearby roads and lots; logs why there is none.
        private bool TryFindNeighbour(Entity building, CageTarget target, AlignSide side,
            NeighbourAlignOptions options, out CageState state, out NeighbourEdge neighbour,
            out DeformRequestResult failure)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            neighbour = default;
            failure = DeformRequestResult.NotDeformable;
            if (!TryGetCage(building, target, out state) ||
                !BuildingSpace.TryGetTransform(_entityManager, building, out var transform))
                return false;

            var ground = GroundFit.GroundPoints(state.Box, state.TargetOffsets);
            var area = new Bounds2(float.MaxValue, float.MinValue);
            foreach (var point in ground)
                area |= BuildingSpace.GroundToWorld(transform, point);
            // A margin beyond the reach, so a rejection can tell how far the nearest edge actually is.
            area = MathUtils.Expand(area, options.SearchMeters + DiagnosticMarginMeters);

            _edges.Clear();
            _roadPieces.Clear();
            _buildings.Clear();
            _neighbours.CollectRoadEdges(area, _roadPieces);
            foreach (var piece in _roadPieces)
                _edges.Add(NeighbourEdge.Road(BuildingSpace.PointToBuilding(transform, piece.a),
                    BuildingSpace.PointToBuilding(transform, piece.b)));
            _neighbours.CollectBuildings(building, area, _buildings);
            foreach (var other in _buildings)
                AddNeighbourEdges(transform, other, target == CageTarget.Building);
            if (NeighbourAlign.TryFindNeighbour(state.Box, state.TargetOffsets, side, _edges, options,
                    out neighbour, out float nearest))
                return true;

            string nearestText = float.IsPositiveInfinity(nearest) ? "none" : $"{nearest:0.0} m";
            Mod.Log.Info($"Align {side} of building {building} ({target}): no neighbour within " +
                $"{options.SearchMeters} m ({_roadPieces.Count} road pieces, {_buildings.Count} lots nearby; " +
                $"nearest parallel edge: {nearestText}).");
            failure = DeformRequestResult.Rejected;
            return false;
        }

        // Ground outline of another building's cage (its building cage when aligning walls, else its lot), in the
        // target's building space with heights relative to the target's ground.
        private void AddNeighbourEdges(in Transform target, Entity other, bool walls)
        {
            if (!TryGetCage(other, CageTarget.Lot, out var state) ||
                !BuildingSpace.TryGetTransform(_entityManager, other, out var transform))
                return;
            if (walls && state.HasOwnBuildingBox && !TryGetCage(other, CageTarget.Building, out state))
                return;
            var ground = GroundFit.GroundPoints3(state.Box, state.TargetOffsets);
            for (int i = 0; i < ground.Length; i++)
                ground[i] = BuildingSpace.PointToBuilding(target, BuildingSpace.PointToWorld(transform, ground[i]));
            NeighbourEdge.AddLotOutline(ground, _edges);
        }

        /// <summary>Requests both cages as given on the lot box (what <see cref="CageState.TargetSpec"/> holds).</summary>
        public DeformRequestResult Apply(Entity building, DeformSpec spec)
        {
            if (spec == null)
                throw new ArgumentNullException(nameof(spec));
            if (!_pipeline.TryDescribe(building, out var description))
                return DeformRequestResult.NotDeformable;
            return Request(building, description, spec);
        }

        private DeformRequestResult Request(Entity building, in BuildingDescription description, DeformSpec spec) =>
            Request(building, description, spec, out _);

        // Clamps and validates each cage on its own box: the lot on the lot box, the building on the building's box.
        private DeformRequestResult Request(Entity building, in BuildingDescription description, DeformSpec spec,
            out DeformSpec requested)
        {
            var lotBox = description.LotBox;
            var limits = Options.Limits;
            var lot = CageValidator.Clamp(lotBox, spec.LotOffsets, limits);
            requested = spec.IsLinked
                ? new DeformSpec(lot, spec.FlatTerrain)
                : SplitCageEdit.ClampBuilding(lotBox, description.BuildingBox, spec.WithLotOffsets(lot), limits);
            if (CageValidator.ValidateSplit(lotBox, description.BuildingBox, requested, limits) != CageValidity.Valid)
                return DeformRequestResult.Rejected;
            requested = requested.Normalized();
            _pipeline.Request(building, requested);
            return requested.Equals(spec.Normalized()) ? DeformRequestResult.Accepted : DeformRequestResult.Clamped;
        }

        public DeformRequestResult MoveHandle(Entity building, CageTarget target, CageHandle handle, float3 delta)
        {
            if (!TryGetCage(building, target, out var state))
                return DeformRequestResult.NotDeformable;
            return SetOffsets(building, state, state.TargetOffsets.Move(handle, delta));
        }

        public DeformRequestResult Reset(Entity building, CageTarget target)
        {
            if (!TryDescribeTarget(building, out var description, out var spec))
                return DeformRequestResult.NotDeformable;
            _editOffsets.Remove(building);
            return Request(building, description, SplitCageEdit.Reset(description.LotBox,
                description.BuildingEditBox, spec, EffectiveTarget(description, target)));
        }

        public DeformRequestResult Link(Entity building)
        {
            if (!TryDescribeTarget(building, out var description, out var spec))
                return DeformRequestResult.NotDeformable;
            if (spec.IsLinked)
                return DeformRequestResult.Rejected;
            return Request(building, description, SplitCageEdit.Link(spec));
        }

        public int ResetAll() => _pipeline.ResetAll();

        public FlexItImportResult ImportFlexIt()
        {
            var batch = _pipeline.PrepareFlexItImport();
            int converted = 0, approximated = 0, clamped = 0, failed = batch.FailedCount;
            foreach (var (building, spec, source, isApproximation) in batch.Conversions)
            {
                // FlexIt conversions are lot-cage offsets.
                var result = _pipeline.TryDescribe(building, out var description)
                    ? Request(building, description, spec)
                    : DeformRequestResult.NotDeformable;
                if (result == DeformRequestResult.Accepted || result == DeformRequestResult.Clamped)
                {
                    converted++;
                    if (isApproximation)
                        approximated++;
                }
                else
                {
                    failed++;
                }
                if (result == DeformRequestResult.Clamped)
                    clamped++;
                if (result != DeformRequestResult.Accepted)
                    Mod.Log.Warn($"FlexIt import of building {building} ({source}): {result}.");
            }
            var summary = new FlexItImportResult(batch.NoCity, batch.FlexItMissing, converted, approximated,
                clamped, batch.ResetCount, failed);
            Mod.Log.Info($"FlexIt import done: {summary}.");
            return summary;
        }

        private readonly struct EditOffsets
        {
            public EditOffsets(CageBox editBox, CageOffsets lot, CageOffsets edit)
            {
                EditBox = editBox;
                Lot = lot;
                Edit = edit;
            }

            public CageBox EditBox { get; }
            public CageOffsets Lot { get; }
            public CageOffsets Edit { get; }

            public bool Matches(CageBox editBox, DeformSpec lotSpec) =>
                EditBox.Equals(editBox) && Lot.Equals(lotSpec.LotOffsets);
        }

        private static DeformStatus MapStatus(VariantStatus status, DeformSpec applied) => status switch
        {
            VariantStatus.Pending => DeformStatus.Pending,
            VariantStatus.WaitingForPrefab => DeformStatus.Applying,
            VariantStatus.Failed => DeformStatus.Failed,
            _ => applied.IsIdentity ? DeformStatus.Undeformed : DeformStatus.Applied,
        };
    }
}
