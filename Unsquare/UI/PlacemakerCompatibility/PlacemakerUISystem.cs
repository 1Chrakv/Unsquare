using Colossal.UI.Binding;
using Game.UI;
using Unsquare.UI.Editing;
using Unsquare.UI.Tool;

namespace Unsquare.UI.PlacemakerCompatibility
{
    /// <summary>
    /// Bindings of the Placemaker dialog (placemaker-dialog.tsx), shown while a picked building waits for the
    /// <see cref="PlacemakerDecision"/>. Group and names must match the dialog.
    /// </summary>
    public partial class PlacemakerUISystem : UISystemBase
    {
        private const string Group = "Unsquare";

        protected override void OnCreate()
        {
            base.OnCreate();
            var tool = World.GetOrCreateSystemManaged<UnsquareToolSystem>();
            var decision = World.GetOrCreateSystemManaged<CageEditorSystem>().Placemaker;
            AddUpdateBinding(new GetterValueBinding<bool>(Group, "conversionPending",
                () => tool.IsActive && decision.IsOpen));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "conversionProblem",
                () => decision.Problem?.ToString() ?? string.Empty));
            AddUpdateBinding(new GetterValueBinding<int>(Group, "conversionRowSize", () => decision.RowSize));
            AddBinding(new TriggerBinding(Group, "confirmConversion", () => decision.Confirm()));
            AddBinding(new TriggerBinding(Group, "confirmRowConversion", () => decision.Confirm(wholeRow: true)));
            AddBinding(new TriggerBinding(Group, "declineConversion", () => decision.Close()));
        }
    }
}
