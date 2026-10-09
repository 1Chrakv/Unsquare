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
            // Until the API exposes scopes, the cage is shown in the scope that is applied (or requested).
            var applied = description.Applied;
            var target = _pipeline.TryGetRequested(building, out var requested) ? requested : applied;
            var scope = description.Effective(target.IsIdentity ? applied.Scope : target.Scope);
            var appliedOffsets = applied.Scope == scope ? applied.Offsets ?? CageOffsets.Identity : CageOffsets.Identity;
            var status = MapStatus(_pipeline.GetStatus(building), appliedOffsets);
            state = new CageState(building, description.BoxFor(scope), appliedOffsets,
                target.Offsets ?? CageOffsets.Identity, status);
            return true;
        }

        public DeformRequestResult SetOffsets(Entity building, CageOffsets offsets)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            if (!_pipeline.TryDescribe(building, out var description))
                return DeformRequestResult.NotDeformable;

            var scope = description.Applied.Scope;
            var box = description.BoxFor(scope);
            var clamped = CageValidator.Clamp(box, offsets);
            if (CageValidator.Validate(box, clamped) != CageValidity.Valid)
                return DeformRequestResult.Rejected;
            _pipeline.Request(building, new CageShape(scope, clamped));
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
