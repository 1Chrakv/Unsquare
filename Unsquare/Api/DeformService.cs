using System;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;
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

        public bool TryGetCage(Entity building, out CageState state)
        {
            state = default;
            if (!_pipeline.TryDescribe(building, out var description))
                return false;
            var applied = description.AppliedOffsets;
            bool requested = _pipeline.TryGetRequested(building, out var target);
            var status = MapStatus(_pipeline.GetStatus(building), applied);
            state = new CageState(building, description.Box, applied, requested ? target : applied, status);
            return true;
        }

        public DeformRequestResult SetOffsets(Entity building, CageOffsets offsets)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            if (!_pipeline.TryDescribe(building, out var description))
                return DeformRequestResult.NotDeformable;

            var clamped = CageValidator.Clamp(description.Box, offsets);
            if (CageValidator.Validate(description.Box, clamped) != CageValidity.Valid)
                return DeformRequestResult.Rejected;
            _pipeline.Request(building, clamped);
            return clamped.Equals(offsets) ? DeformRequestResult.Accepted : DeformRequestResult.Clamped;
        }

        public DeformRequestResult MoveHandle(Entity building, CageHandle handle, float3 delta)
        {
            if (!TryGetCage(building, out var state))
                return DeformRequestResult.NotDeformable;
            return SetOffsets(building, state.TargetOffsets.Move(handle, delta));
        }

        public DeformRequestResult Reset(Entity building) => SetOffsets(building, CageOffsets.Identity);

        public int ResetAll() => _pipeline.ResetAll();

        private static DeformStatus MapStatus(VariantStatus status, CageOffsets applied) => status switch
        {
            VariantStatus.Pending => DeformStatus.Pending,
            VariantStatus.WaitingForPrefab => DeformStatus.Applying,
            VariantStatus.Failed => DeformStatus.Failed,
            _ => applied.IsIdentity ? DeformStatus.Undeformed : DeformStatus.Applied,
        };
    }
}
