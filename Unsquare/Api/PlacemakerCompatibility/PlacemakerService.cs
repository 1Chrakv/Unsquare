using System;
using System.Collections.Generic;
using Unity.Entities;
using Unsquare.Integration.PlacemakerCompatibility;
using Unsquare.Integration.Systems;

namespace Unsquare.Api.PlacemakerCompatibility
{
    /// <summary>
    /// <see cref="IPlacemakerService"/> on top of the deform service: an adopted building is described on Placemaker's
    /// plain base, so its conversion is an ordinary first request of the converted cage.
    /// </summary>
    internal sealed class PlacemakerService : IPlacemakerService
    {
        private readonly IBuildingDeformService _deform;
        private readonly PlacemakerAdoptions _adoptions;
        private readonly IVariantPipeline _pipeline;

        public PlacemakerService(IBuildingDeformService deform, PlacemakerAdoptions adoptions, IVariantPipeline pipeline)
        {
            _deform = deform ?? throw new ArgumentNullException(nameof(deform));
            _adoptions = adoptions ?? throw new ArgumentNullException(nameof(adoptions));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        public bool NeedsConversion(Entity building) => _adoptions.IsUndecided(building);

        public PlacemakerConversionResult Convert(Entity building)
        {
            var adoption = _adoptions.Prepare(building);
            return adoption.Status == PlacemakerAdoptionStatus.Ready
                ? RequestAdopted(building, adoption)
                : Refusal(adoption.Status);
        }

        public int RowSize(Entity building) => _adoptions.Row(building).Count;

        public PlacemakerConversionResult ConvertRow(Entity building)
        {
            var row = _adoptions.Row(building);
            if (row.Count < 2 || !Contains(row, building))
                return Convert(building);

            // All or nothing while Placemaker is busy: converting part of the row would let it re-plan the rest.
            var adoptions = new List<(Entity Building, PlacemakerAdoption Adoption)>(row.Count);
            foreach (var member in row)
            {
                var adoption = _adoptions.Prepare(member);
                adoptions.Add((member, adoption));
                if (adoption.Status is PlacemakerAdoptionStatus.PlacemakerBusy or
                    PlacemakerAdoptionStatus.PlacemakerMissing)
                {
                    foreach (var (prepared, _) in adoptions)
                        _adoptions.Release(prepared);
                    Mod.Log.Info($"Row conversion of building {building} refused: {adoption.Status} at {member}.");
                    return Refusal(adoption.Status);
                }
            }

            var result = PlacemakerConversionResult.Failed;
            var converted = new List<Entity>(row.Count);
            foreach (var (member, adoption) in adoptions)
            {
                var memberResult = adoption.Status == PlacemakerAdoptionStatus.Ready
                    ? RequestAdopted(member, adoption)
                    : Refusal(adoption.Status);
                if (member == building)
                    result = memberResult;
                if (memberResult is PlacemakerConversionResult.Converted or PlacemakerConversionResult.Approximated
                    or PlacemakerConversionResult.Clamped)
                    converted.Add(member);
            }
            _pipeline.SwapTogether(converted);
            foreach (var member in converted)
                _deform.BakeNow(member);
            Mod.Log.Info($"Row conversion of building {building}: {converted.Count} of {row.Count} converted.");
            return result;
        }

        private static bool Contains(IReadOnlyList<Entity> row, Entity building)
        {
            foreach (var member in row)
            {
                if (member == building)
                    return true;
            }
            return false;
        }

        private static PlacemakerConversionResult Refusal(PlacemakerAdoptionStatus status) => status switch
        {
            PlacemakerAdoptionStatus.PlacemakerBusy => PlacemakerConversionResult.PlacemakerBusy,
            PlacemakerAdoptionStatus.PlacemakerMissing => PlacemakerConversionResult.PlacemakerMissing,
            _ => PlacemakerConversionResult.Failed,
        };

        private PlacemakerConversionResult RequestAdopted(Entity building, PlacemakerAdoption adoption)
        {
            var result = _deform.Apply(building, adoption.Spec);
            Mod.Log.Info($"Placemaker conversion of building {building}: {result}.");
            switch (result)
            {
                case DeformRequestResult.Accepted:
                    return adoption.Approximated
                        ? PlacemakerConversionResult.Approximated
                        : PlacemakerConversionResult.Converted;
                case DeformRequestResult.Clamped:
                    return PlacemakerConversionResult.Clamped;
                default:
                    _adoptions.Release(building);
                    return PlacemakerConversionResult.Failed;
            }
        }
    }
}
