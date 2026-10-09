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

        public bool TryGetCage(Entity building, CageScope? scope, out CageState state)
        {
            state = default;
            if (!_pipeline.TryDescribe(building, out var description))
                return false;

            var applied = description.Applied;
            bool requested = _pipeline.TryGetRequested(building, out var request);
            var current = requested ? request : applied;
            var view = description.Effective(scope ?? (current.IsIdentity ? applied.Scope : current.Scope));

            var appliedOffsets = OffsetsIn(applied, view);
            var targetOffsets = requested ? OffsetsIn(request, view) : appliedOffsets;
            var status = MapStatus(_pipeline.GetStatus(building), appliedOffsets);
            state = new CageState(building, view, description.BoxFor(view), appliedOffsets, targetOffsets, status);
            return true;
        }

        public DeformRequestResult SetOffsets(Entity building, CageScope scope, CageOffsets offsets)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            if (!_pipeline.TryDescribe(building, out var description))
                return DeformRequestResult.NotDeformable;

            var effective = description.Effective(scope);
            var box = description.BoxFor(effective);
            var clamped = CageValidator.Clamp(box, offsets, Options.Limits);
            if (CageValidator.Validate(box, clamped, Options.Limits) != CageValidity.Valid)
                return DeformRequestResult.Rejected;
            _pipeline.Request(building, new CageShape(effective, clamped));
            return clamped.Equals(offsets) ? DeformRequestResult.Accepted : DeformRequestResult.Clamped;
        }

        public DeformRequestResult MoveHandle(Entity building, CageScope scope, CageHandle handle, float3 delta)
        {
            if (!TryGetCage(building, scope, out var state))
                return DeformRequestResult.NotDeformable;
            return SetOffsets(building, state.Scope, state.TargetOffsets.Move(handle, delta));
        }

        public DeformRequestResult Reset(Entity building) =>
            SetOffsets(building, CageScope.Building, CageOffsets.Identity);

        public int ResetAll() => _pipeline.ResetAll();

        // A shape only has offsets in its own scope; seen from the other scope the cage is undeformed.
        private static CageOffsets OffsetsIn(CageShape shape, CageScope scope) =>
            shape.IsIdentity || shape.Scope != scope ? CageOffsets.Identity : shape.Offsets;

        private static DeformStatus MapStatus(VariantStatus status, CageOffsets applied) => status switch
        {
            VariantStatus.Pending => DeformStatus.Pending,
            VariantStatus.WaitingForPrefab => DeformStatus.Applying,
            VariantStatus.Failed => DeformStatus.Failed,
            _ => applied.IsIdentity ? DeformStatus.Undeformed : DeformStatus.Applied,
        };
    }
}
