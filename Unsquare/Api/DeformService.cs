using System;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;
using Unsquare.Core.Options;
using Unsquare.Core.Terrain;
using Unsquare.Integration.Systems;

namespace Unsquare.Api
{
    /// <summary>Validates and clamps UI requests, then forwards them to the variant pipeline.</summary>
    internal sealed class DeformService : IBuildingDeformService
    {
        private readonly IVariantPipeline _pipeline;
        private readonly BaseTerrainSystem _terrain;

        public DeformService(IVariantPipeline pipeline, BaseTerrainSystem terrain)
        {
            _pipeline = pipeline;
            _terrain = terrain;
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

        public bool TryFitToGround(Entity building, Action<DeformSpec, DeformRequestResult> done)
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
                var fitted = GroundFit.Fit(now.Box, now.TargetOffsets, heights);
                done(now.TargetSpec, SetOffsets(building, fitted));
            });
        }

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
