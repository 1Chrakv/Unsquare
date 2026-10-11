using System.Collections.Generic;
using Colossal;
using Unsquare.Core.Deformation;

namespace Unsquare.UI.Input
{
    /// <summary>English texts for the options page and key bindings.</summary>
    internal sealed class LocaleEN : IDictionarySource
    {
        private const string NewDeformations =
            " Applies to buildings deformed from now on; already deformed ones pick it up after reloading the save.";

        private readonly UnsquareSettings _settings;

        public LocaleEN(UnsquareSettings settings)
        {
            _settings = settings;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            var entries = new Dictionary<string, string>
            {
                { _settings.GetSettingsLocaleID(), "Unsquare" },
                { _settings.GetOptionTabLocaleID(UnsquareSettings.MainTab), "Main" },
                { _settings.GetOptionTabLocaleID(UnsquareSettings.AdvancedTab), "Advanced" },
                { _settings.GetOptionGroupLocaleID(UnsquareSettings.HotkeyGroup), "Cage editing hotkeys" },
                { _settings.GetOptionGroupLocaleID(UnsquareSettings.EditingGroup), "Editing" },
                { _settings.GetOptionGroupLocaleID(UnsquareSettings.LimitsGroup), "Cage limits" },
                { _settings.GetOptionGroupLocaleID(UnsquareSettings.AttachmentsGroup), "Props and lot" },
                { _settings.GetOptionGroupLocaleID(UnsquareSettings.LotSyncGroup), "Lot sync" },
                { _settings.GetOptionGroupLocaleID(UnsquareSettings.OverlayGroup), "Cage overlay" },
                { _settings.GetOptionGroupLocaleID(UnsquareSettings.PerformanceGroup), "Performance and storage" },
                { _settings.GetOptionGroupLocaleID(UnsquareSettings.ResetGroup), "Reset" },
                { _settings.GetBindingMapLocaleID(), "Unsquare" },
                { "Unsquare.ModeButton.Tooltip", "Unsquare: deform buildings (Alt+U)" },
                { "Unsquare.ToolWindow.Corner", "Pick corners (Alt+1)" },
                { "Unsquare.ToolWindow.Edge", "Pick edges (Alt+2)" },
                { "Unsquare.ToolWindow.Face", "Pick faces (Alt+3)" },
                { "Unsquare.ToolWindow.ResetHandle", "Reset the selected corner, edge or face (Alt+R)" },
                { "Unsquare.ToolWindow.ResetBuilding", "Reset the whole building (Alt+Backspace)" },
                { _settings.GetEnumValueLocaleID(PlacementMode.Follow), "Follow (tilt with the building)" },
                { _settings.GetEnumValueLocaleID(PlacementMode.Upright), "Upright (turn only)" },
                { _settings.GetOptionWarningLocaleID(nameof(UnsquareSettings.ResetAdvanced)),
                    "Reset all advanced options to their defaults?" },
            };
            entries[_settings.GetOptionLabelLocaleID(nameof(UnsquareSettings.ToggleModeBinding))] =
                CageHotkeys.ToggleModeLabel;
            entries[_settings.GetOptionDescLocaleID(nameof(UnsquareSettings.ToggleModeBinding))] =
                "Turns Unsquare mode on and off, like the Unsquare button. The other hotkeys only work in the mode.";
            entries[_settings.GetBindingKeyLocaleID(CageHotkeys.ToggleMode)] = CageHotkeys.ToggleModeLabel;
            entries[_settings.GetOptionLabelLocaleID(nameof(UnsquareSettings.ExitModeBinding))] =
                CageHotkeys.ExitModeLabel;
            entries[_settings.GetOptionDescLocaleID(nameof(UnsquareSettings.ExitModeBinding))] =
                "Leaves Unsquare mode. Only active while the mode is on, so the key keeps its usual meaning otherwise.";
            entries[_settings.GetBindingKeyLocaleID(CageHotkeys.ExitMode)] = CageHotkeys.ExitModeLabel;
            foreach (var hotkey in CageHotkeys.All)
            {
                entries[_settings.GetOptionLabelLocaleID(hotkey.PropertyName)] = hotkey.Label;
                entries[_settings.GetOptionDescLocaleID(hotkey.PropertyName)] =
                    $"{hotkey.Label} on the building edited in Unsquare mode.";
                entries[_settings.GetBindingKeyLocaleID(hotkey.ActionName)] = hotkey.Label;
            }

            Option(entries, nameof(UnsquareSettings.SmallStep), "Small nudge step (m)",
                "Smallest step of the step-size hotkey. Snapped to 0.1 m.");
            Option(entries, nameof(UnsquareSettings.MediumStep), "Medium nudge step (m)",
                "Middle step of the step-size hotkey. Snapped to 0.1 m.");
            Option(entries, nameof(UnsquareSettings.LargeStep), "Large nudge step (m)",
                "Largest step of the step-size hotkey. Snapped to 0.1 m.");
            Option(entries, nameof(UnsquareSettings.PickRadius), "Mouse pick radius (px)",
                "How close the mouse must be to an edge, a corner or a gizmo arrow to grab it.");
            Option(entries, nameof(UnsquareSettings.FarClickMargin), "Far-click margin",
                "A click outside the selected building's cage grown by this share of its size steps back: " +
                "first the selected edge, face or corner is deselected, then the building.");
            Option(entries, nameof(UnsquareSettings.UndoSteps), "Undo steps",
                "How many cage edits (nudges, drags, resets) undo can take back, across all buildings.");

            Option(entries, nameof(UnsquareSettings.MaxOffsetFactor), "Max corner offset (× box size)",
                "How far a cage corner may move per axis, relative to the cage size on that axis.");
            Option(entries, nameof(UnsquareSettings.MinMovementRange), "Min movement range (m)",
                "Corners may always move at least this far, so thin buildings can still be edited.");
            Option(entries, nameof(UnsquareSettings.MinLocalVolume), "Min local volume",
                "Smallest share of its volume any part of the building may be squeezed to. " +
                "Stops cages that collapse or turn inside out.");

            Option(entries, nameof(UnsquareSettings.DeformProps), "Deform props",
                "Props, trees and attached sub-buildings follow the cage." + NewDeformations);
            Option(entries, nameof(UnsquareSettings.DeformAreas), "Deform ground areas",
                "Lot surfaces (grass, paving, …) follow the cage." + NewDeformations);
            Option(entries, nameof(UnsquareSettings.DeformLanes), "Deform lanes",
                "Paths and fences drawn as lanes follow the cage." + NewDeformations);
            Option(entries, nameof(UnsquareSettings.DeformNets), "Deform lot nets",
                "Paths and small roads on the lot follow the cage; connections to outside roads stay." +
                NewDeformations);
            Option(entries, nameof(UnsquareSettings.AttachedProps), "Props on the building",
                "How props attached to the building turn: tilt with it, or stay upright and only turn. " +
                "Props on the ground always stay upright." + NewDeformations);
            Option(entries, nameof(UnsquareSettings.FacadeReach), "Facade alignment reach (m)",
                "Props this close to a facade turn exactly with it instead of with the average deformation. " +
                "0 turns facade alignment off." + NewDeformations);

            Option(entries, nameof(UnsquareSettings.AreaMatchDistance), "Area match distance (m)",
                "Saved lot areas further from their expected outline count as edited by you and are not moved.");
            Option(entries, nameof(UnsquareSettings.NetMatchDistance), "Net match distance (m)",
                "Saved lot paths further from their expected course count as edited by you and are not moved.");

            Option(entries, nameof(UnsquareSettings.LineRadius), "Line thickness (m)",
                "Radius of the cage lines.");
            Option(entries, nameof(UnsquareSettings.HandleSize), "Hovered handle size (m)",
                "Diameter of the dot on a hovered corner or face.");
            Option(entries, nameof(UnsquareSettings.SelectedHandleSize), "Selected handle size (m)",
                "Diameter of the dot on the selected corner or face.");
            Option(entries, nameof(UnsquareSettings.AxisLength), "Min gizmo length (m)",
                "Shortest length of the gizmo arrows, used when the camera is close.");
            Option(entries, nameof(UnsquareSettings.GizmoSize), "Gizmo size on screen",
                "Length of the gizmo arrows as a share of the screen height.");

            Option(entries, nameof(UnsquareSettings.RebakeDelay), "Rebake delay (s)",
                "Quiet time after the last edit before the building is rebuilt. Higher values save work while dragging.");
            Option(entries, nameof(UnsquareSettings.PruneUnused), "Delete unused variants",
                "At startup, delete deformed building data that no save has used for a while.");
            Option(entries, nameof(UnsquareSettings.PruneAfterDays), "Delete after (days)",
                "Days a deformed variant may stay unused before it is deleted.");
            Option(entries, nameof(UnsquareSettings.ResetAdvanced), "Reset advanced options",
                "Restore the defaults of every option on this tab. Key bindings stay unchanged.");
            return entries;
        }

        public void Unload()
        {
        }

        private void Option(IDictionary<string, string> entries, string property, string label, string description)
        {
            entries[_settings.GetOptionLabelLocaleID(property)] = label;
            entries[_settings.GetOptionDescLocaleID(property)] = description;
        }
    }
}
