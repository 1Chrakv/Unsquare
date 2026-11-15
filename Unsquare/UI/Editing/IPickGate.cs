using System;
using Unity.Entities;

namespace Unsquare.UI.Editing
{
    /// <summary>
    /// Lets a module claim a picked building before <see cref="CageEditSession"/> targets it, e.g. to ask the player
    /// first (Placemaker's buildings). While it is open, nothing else can be picked.
    /// </summary>
    public interface IPickGate
    {
        bool IsOpen { get; }

        /// <summary>Takes the building if it needs a decision first; the session then targets nothing.</summary>
        bool TryIntercept(Entity building);

        /// <summary>Drops the open decision; the building stays as it is.</summary>
        void Close();

        /// <summary>Raised when a decision hands its building to the session as the new target.</summary>
        event Action<Entity> Released;
    }
}
