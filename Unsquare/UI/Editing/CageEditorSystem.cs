using Colossal.Serialization.Entities;
using Game;
using Unity.Entities;
using Unsquare.Api;
using Unsquare.UI.PlacemakerCompatibility;

namespace Unsquare.UI.Editing
{
    /// <summary>
    /// Owns the <see cref="CageEditSession"/>. The Unsquare tool enables it and chooses the target; this
    /// system only drops targets that no longer exist.
    /// </summary>
    public partial class CageEditorSystem : GameSystemBase
    {
        public CageEditSession Session { get; private set; }

        /// <summary>Asks before the session edits a building Placemaker has bent.</summary>
        public PlacemakerDecision Placemaker { get; private set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            Placemaker = new PlacemakerDecision(UnsquareApi.GetPlacemakerService(World));
            Session = new CageEditSession(UnsquareApi.GetDeformService(World), Placemaker);
        }

        protected override void OnUpdate()
        {
            if (Session.HasTarget && !EntityManager.Exists(Session.Target))
                Session.ClearTarget(); // e.g. bulldozed while selected
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            Session.ClearTarget(); // entities of the previous city are gone
            Session.ClearHistory();
        }
    }
}
