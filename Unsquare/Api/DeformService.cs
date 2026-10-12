using System;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;
using Unsquare.Core.Options;
using Unsquare.Integration.Systems;

namespace Unsquare.Api
{
    /// <summary>Validates and clamps UI requests, then forwards them to the variant pipeline.</summary>
    internal sealed class DeformService : IBuildingDeformService
    {
        private readonly IVariantPipeline _pipeline;

        public DeformService(IVariantPipeline pipeline)
        {
            _pipeline = pipeline;
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
