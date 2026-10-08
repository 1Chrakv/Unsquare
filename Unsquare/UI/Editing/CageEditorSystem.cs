using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.Tools;
using Unity.Entities;
using Unsquare.Api;

namespace Unsquare.UI.Editing
{
    /// <summary>
    /// Owns the <see cref="CageEditSession"/> and chooses its target. For now the target follows the
    /// building selected in the info panel; a dedicated Unsquare tool will set it later.
    /// </summary>
    public partial class CageEditorSystem : GameSystemBase
    {
        private ToolSystem _toolSystem;

        public CageEditSession Session { get; private set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            _toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            Session = new CageEditSession(UnsquareApi.GetDeformService(World));
        }

        protected override void OnUpdate()
        {
            Entity selected = _toolSystem.selected;
            bool isBuilding = selected != Entity.Null && EntityManager.Exists(selected) &&
                              EntityManager.HasComponent<Building>(selected);
            Session.SetTarget(isBuilding ? selected : Entity.Null);
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            Session.SetTarget(Entity.Null); // entities of the previous city are gone
        }
    }
}
