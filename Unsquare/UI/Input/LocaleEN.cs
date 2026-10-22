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
                { _settings.GetOptionGroupLocaleID(UnsquareSettings.FlexItGroup), "Coming from FlexIt" },
                { _settings.GetBindingMapLocaleID(), "Unsquare" },
                { "Unsquare.ModeButton.Tooltip", "Unsquare: deform buildings (Alt+U)" },
                { "Unsquare.ToolWindow.Section.Pick", "Pick" },
                { "Unsquare.ToolWindow.Section.Terrain", "Terrain" },
                { "Unsquare.ToolWindow.Section.Align", "Align to neighbour" },
                { "Unsquare.ToolWindow.Section.Reset", "Reset" },
                { "Unsquare.ToolWindow.Corner", "Pick corners (Alt+1)" },
                { "Unsquare.ToolWindow.Edge", "Pick edges (Alt+2)" },
                { "Unsquare.ToolWindow.Face", "Pick faces (Alt+3)" },
                { "Unsquare.ToolWindow.ResetHandle", "Reset the selected corner, edge or face (Alt+R)" },
                { "Unsquare.ToolWindow.ResetBuilding", "Reset the whole building (Alt+Backspace)" },
                { "Unsquare.ToolWindow.TerrainLift", "Terrain follows the building (Alt+T)" },
                { "Unsquare.ToolWindow.TerrainFlat", "Keep a flat pad under the building (Alt+T)" },
                { "Unsquare.ToolWindow.FitToGround", "Fit the bottom corners to the original ground (Alt+G)" },
                { "Unsquare.ToolWindow.GroundFitModeBackOnly",
                    "Fit mode: back only. The back follows the original ground, the street side goes to road level " +
                    "(Alt+F)" },
                { "Unsquare.ToolWindow.GroundFitModeAllCorners",
                    "Fit mode: all corners. All four corners follow the original ground (Alt+F)" },
                { "Unsquare.ToolWindow.AlignLeft",
                    "Align the left side (seen from the street) to the nearest road or lot (Alt+Shift+Left)" },
                { "Unsquare.ToolWindow.AlignBack", "Align the back to the nearest road or lot (Alt+Shift+Up)" },
                { "Unsquare.ToolWindow.AlignRight",
                    "Align the right side (seen from the street) to the nearest road or lot (Alt+Shift+Right)" },
                { "Unsquare.ToolWindow.AlignGroundLeft",
                    "Level the left side (seen from the street) with the neighbour's ground (Ctrl+Alt+Left)" },
                { "Unsquare.ToolWindow.AlignGroundBack", "Level the back with the neighbour's ground (Ctrl+Alt+Up)" },
                { "Unsquare.ToolWindow.AlignGroundRight",
                    "Level the right side (seen from the street) with the neighbour's ground (Ctrl+Alt+Right)" },
                { "Unsquare.ToolWindow.AlignModeAngle",
                    "Align mode: angle. The corner nearer the neighbour stays, the face turns parallel (Alt+A)" },
                { "Unsquare.ToolWindow.AlignModeFlush",
                    "Align mode: flush. The face turns parallel and moves onto the neighbour's edge (Alt+A)" },
                { "Unsquare.ToolWindow.AlignModeFull",
                    "Align mode: full. The face takes the neighbour's edge and its corners (Alt+A)" },
                { _settings.GetEnumValueLocaleID(PlacementMode.Follow), "Follow (tilt with the building)" },
                { _settings.GetEnumValueLocaleID(PlacementMode.Upright), "Upright (turn only)" },
                { _settings.GetOptionWarningLocaleID(nameof(UnsquareSettings.ResetAdvanced)),
                    "Reset all advanced options to their defaults?" },
                { _settings.GetOptionWarningLocaleID(nameof(UnsquareSettings.ImportFlexIt)),
                    "Convert all FlexIt buildings of this city to Unsquare? Back up your save first. Tapered and " +
                    "skewed buildings keep their shape, corner-fitted ones get the closest straight shape, and " +
                    "arched or bent ones go back to their original shape. Afterwards, save the city; then FlexIt " +
                    "can be removed." },
            };
            entries[_settings.GetOptionLabelLocaleID(nameof(UnsquareSettings.ToggleModeBinding))] =
                CageHotkeys.ToggleModeLabel;
            entries[_settings.GetOptionDescLocaleID(nameof(UnsquareSettings.ToggleModeBinding))] =
                "Turns Unsquare mode on and off, like the Unsquare button. The other hotkeys only work in the mode.";
            entries[_settings.GetBindingKeyLocaleID(CageHotkeys.ToggleMode)] = CageHotkeys.ToggleModeLabel;
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
            Option(entries, nameof(UnsquareSettings.DragThreshold), "Drag threshold (px)",
                "How far the mouse must move after pressing on an edge, face or corner before it is dragged " +
                "sideways. Shorter presses only select it.");
            Option(entries, nameof(UnsquareSettings.FarClickMargin), "Far-click margin",
                "A click outside the selected building's cage grown by this share of its size steps back: " +
                "first the selected edge, face or corner is deselected, then the building.");
            Option(entries, nameof(UnsquareSettings.UndoSteps), "Undo steps",
                "How many cage edits (nudges, drags, resets) undo can take back, across all buildings.");
            Option(entries, nameof(UnsquareSettings.AlignReach), "Align reach (m)",
                "How far from a lot face a road or a neighbouring lot may be to align the face to it " +
                "(one zoning cell is 8 m).");
            Option(entries, nameof(UnsquareSettings.AlignMaxAngle), "Align max angle",
                "How far a road or neighbouring lot edge may be turned against the face to align to it.");

            Option(entries, nameof(UnsquareSettings.MaxOffsetFactor), "Max corner offset (× box size)",
                "How far a cage corner may move per axis, relative to the cage size on that axis. Up and down it is " +
                "relative to the longest side, so long lots can follow steep slopes.");
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
            Option(entries, nameof(UnsquareSettings.CoarseLodTwist), "Coarse model twist limit (m)",
                "Buildings whose cage bends more than this keep their medium-detail model at a distance, because " +
                "the coarsest one can't follow the bend. Lower: cleaner distant views, more triangles to draw." +
                NewDeformations);
            Option(entries, nameof(UnsquareSettings.PruneUnused), "Delete unused variants",
                "At startup, delete deformed building data that no save has used for a while.");
            Option(entries, nameof(UnsquareSettings.PruneAfterDays), "Delete after (days)",
                "Days a deformed variant may stay unused before it is deleted.");
            Option(entries, nameof(UnsquareSettings.ImportFlexIt), "Import FlexIt buildings",
                "Converts the buildings FlexIt deformed in the loaded city. Needs FlexIt to be active. Tapered and " +
                "skewed buildings become Unsquare cages with the same shape, and their props and lot follow too. " +
                "Unsquare can't curve buildings yet: corner-fitted ones get the closest straight shape (the corner " +
                "itself doesn't fold), arched and bent ones are reset to their original shape.");
            Option(entries, nameof(UnsquareSettings.ImportFlexItStatus), "Last import",
                "Result of the last FlexIt import; details are in the Unsquare log.");
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
