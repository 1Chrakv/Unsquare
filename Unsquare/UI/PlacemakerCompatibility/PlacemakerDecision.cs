using System;
using Unity.Entities;
using Unsquare.Api.PlacemakerCompatibility;
using Unsquare.UI.Editing;

namespace Unsquare.UI.PlacemakerCompatibility
{
    /// <summary>
    /// The question asked when the player picks a building Placemaker has bent: convert it (or its whole row) to an
    /// Unsquare cage of the same shape, or leave it to Placemaker. Shown by placemaker-dialog.tsx.
    /// </summary>
    public sealed class PlacemakerDecision : IPickGate
    {
        private readonly IPlacemakerService _service;

        public PlacemakerDecision(IPlacemakerService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public event Action<Entity> Released;

        /// <summary>The building waiting for the decision; Entity.Null while none is open.</summary>
        public Entity Pending { get; private set; } = Entity.Null;
        public bool IsOpen => Pending != Entity.Null;

        /// <summary>Houses in the pending building's row (itself included); 2 or more offers the whole row.</summary>
        public int RowSize { get; private set; }

        /// <summary>Why the last conversion attempt failed; the decision stays open so the player can retry.</summary>
        public PlacemakerConversionResult? Problem { get; private set; }

        public bool TryIntercept(Entity building)
        {
            if (IsOpen || !_service.NeedsConversion(building))
                return false;
            Pending = building;
            RowSize = _service.RowSize(building);
            Problem = null;
            Log($"Building {building} is shaped by Placemaker: asking to convert");
            return true;
        }

        /// <summary>
        /// Converts the pending building (with <paramref name="wholeRow"/> also the rest of its row) and hands it to the
        /// session. Not undoable; on failure the decision stays open with <see cref="Problem"/>.
        /// </summary>
        public PlacemakerConversionResult? Confirm(bool wholeRow = false)
        {
            if (!IsOpen)
                return null;
            var building = Pending;
            var result = wholeRow ? _service.ConvertRow(building) : _service.Convert(building);
            Log($"Convert Placemaker building {building}{(wholeRow ? " with its row" : "")} -> {result}");
            if (result is PlacemakerConversionResult.PlacemakerBusy or PlacemakerConversionResult.PlacemakerMissing
                or PlacemakerConversionResult.Failed)
            {
                Problem = result;
                return result;
            }
            Reset();
            Released?.Invoke(building);
            return result;
        }

        /// <summary>Drops the pending building: it stays unselected and keeps Placemaker's shape.</summary>
        public void Close()
        {
            if (!IsOpen)
                return;
            Log($"Building {Pending} stays with Placemaker");
            Reset();
        }

        private void Reset()
        {
            Pending = Entity.Null;
            RowSize = 0;
            Problem = null;
        }

        private static void Log(string message) => Mod.Log.Info($"[Edit] {message}");
    }
}
