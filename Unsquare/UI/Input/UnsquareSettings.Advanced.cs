using System;
using System.Reflection;
using Game.Settings;
using Game.UI;
using Unity.Mathematics;
using Unsquare.Core.Deformation;
using Unsquare.Core.Options;
using Unsquare.UI.Editing;
using Unsquare.UI.Overlay;

namespace Unsquare.UI.Input
{
    // Advanced tab: tunables of editing, cage limits, attachments, lot sync, overlay and housekeeping.
    // Defaults come from the option types; values are clamped to the slider ranges when mapped.
    public sealed partial class UnsquareSettings
    {
        public const string AdvancedTab = "Advanced";
        public const string EditingGroup = "Editing";
        public const string LimitsGroup = "Limits";
        public const string AttachmentsGroup = "Attachments";
        public const string LotSyncGroup = "LotSync";
        public const string OverlayGroup = "Overlay";
        public const string PerformanceGroup = "Performance";
        public const string ResetGroup = "Reset";

        [SettingsUISection(AdvancedTab, EditingGroup)]
        [SettingsUISlider(min = 0.1f, max = 10f, step = 0.1f, unit = Unit.kFloatSingleFraction)]
        public float SmallStep { get; set; }

        [SettingsUISection(AdvancedTab, EditingGroup)]
        [SettingsUISlider(min = 0.1f, max = 10f, step = 0.1f, unit = Unit.kFloatSingleFraction)]
        public float MediumStep { get; set; }

        [SettingsUISection(AdvancedTab, EditingGroup)]
        [SettingsUISlider(min = 0.1f, max = 10f, step = 0.1f, unit = Unit.kFloatSingleFraction)]
        public float LargeStep { get; set; }

        [SettingsUISection(AdvancedTab, EditingGroup)]
        [SettingsUISlider(min = 4f, max = 40f, step = 1f, unit = Unit.kInteger)]
        public float PickRadius { get; set; }

        [SettingsUISection(AdvancedTab, EditingGroup)]
        [SettingsUISlider(min = 0f, max = 50f, step = 1f, unit = Unit.kPercentage)]
        public float FarClickMargin { get; set; }

        [SettingsUISection(AdvancedTab, EditingGroup)]
        [SettingsUISlider(min = 10f, max = 500f, step = 10f, unit = Unit.kInteger)]
        public int UndoSteps { get; set; }

        [SettingsUISection(AdvancedTab, LimitsGroup)]
        [SettingsUISlider(min = 0.5f, max = 5f, step = 0.1f, unit = Unit.kFloatSingleFraction)]
        public float MaxOffsetFactor { get; set; }

        [SettingsUISection(AdvancedTab, LimitsGroup)]
        [SettingsUISlider(min = 1f, max = 30f, step = 0.5f, unit = Unit.kFloatSingleFraction)]
        public float MinMovementRange { get; set; }

        [SettingsUISection(AdvancedTab, LimitsGroup)]
        [SettingsUISlider(min = 1f, max = 50f, step = 1f, unit = Unit.kPercentage)]
        public float MinLocalVolume { get; set; }

        [SettingsUISection(AdvancedTab, AttachmentsGroup)]
        public bool DeformProps { get; set; }

        [SettingsUISection(AdvancedTab, AttachmentsGroup)]
        public bool DeformAreas { get; set; }

        [SettingsUISection(AdvancedTab, AttachmentsGroup)]
        public bool DeformLanes { get; set; }

        [SettingsUISection(AdvancedTab, AttachmentsGroup)]
        public bool DeformNets { get; set; }

        [SettingsUISection(AdvancedTab, AttachmentsGroup)]
        public PlacementMode AttachedProps { get; set; }

        [SettingsUISection(AdvancedTab, AttachmentsGroup)]
        [SettingsUISlider(min = 0f, max = 20f, step = 0.5f, unit = Unit.kFloatSingleFraction)]
        public float FacadeReach { get; set; }

        [SettingsUISection(AdvancedTab, LotSyncGroup)]
        [SettingsUISlider(min = 0.25f, max = 5f, step = 0.25f, unit = Unit.kFloatTwoFractions)]
        public float AreaMatchDistance { get; set; }

        [SettingsUISection(AdvancedTab, OverlayGroup)]
        [SettingsUISlider(min = 0.02f, max = 0.5f, step = 0.02f, unit = Unit.kFloatTwoFractions)]
        public float LineRadius { get; set; }

        [SettingsUISection(AdvancedTab, OverlayGroup)]
        [SettingsUISlider(min = 0.2f, max = 3f, step = 0.1f, unit = Unit.kFloatSingleFraction)]
        public float HandleSize { get; set; }

        [SettingsUISection(AdvancedTab, OverlayGroup)]
        [SettingsUISlider(min = 0.2f, max = 5f, step = 0.1f, unit = Unit.kFloatSingleFraction)]
        public float SelectedHandleSize { get; set; }

        [SettingsUISection(AdvancedTab, OverlayGroup)]
        [SettingsUISlider(min = 0f, max = 10f, step = 0.5f, unit = Unit.kFloatSingleFraction)]
        public float AxisLength { get; set; }

        [SettingsUISection(AdvancedTab, OverlayGroup)]
        [SettingsUISlider(min = 2f, max = 30f, step = 1f, unit = Unit.kPercentage)]
        public float GizmoSize { get; set; }

        [SettingsUISection(AdvancedTab, PerformanceGroup)]
        [SettingsUISlider(min = 0.1f, max = 2f, step = 0.1f, unit = Unit.kFloatSingleFraction)]
        public float RebakeDelay { get; set; }

        [SettingsUISection(AdvancedTab, PerformanceGroup)]
        public bool PruneUnused { get; set; }

        [SettingsUISection(AdvancedTab, PerformanceGroup)]
        [SettingsUISlider(min = 1f, max = 180f, step = 1f, unit = Unit.kInteger)]
        [SettingsUIDisableByCondition(typeof(UnsquareSettings), nameof(IsPruneDisabled))]
        public int PruneAfterDays { get; set; }

        [SettingsUISection(AdvancedTab, ResetGroup)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        public bool ResetAdvanced
        {
            set
            {
                SetAdvancedDefaults();
                ApplyAndSave();
            }
        }

        public bool IsPruneDisabled() => !PruneUnused;

        public PipelineOptions ToPipelineOptions()
        {
            var limits = new CageLimits(Clamped(MaxOffsetFactor, nameof(MaxOffsetFactor)),
                Clamped(MinMovementRange, nameof(MinMovementRange)),
                Clamped(MinLocalVolume, nameof(MinLocalVolume)) / 100f);
            var attachments = new AttachmentOptions(DeformProps, DeformAreas, DeformLanes, DeformNets,
                Defined(AttachedProps, AttachmentOptions.Default.AttachedPropMode),
                Clamped(FacadeReach, nameof(FacadeReach)));
            return new PipelineOptions(limits, attachments, Clamped(RebakeDelay, nameof(RebakeDelay)), PruneUnused,
                (int)Clamped(PruneAfterDays, nameof(PruneAfterDays)),
                Clamped(AreaMatchDistance, nameof(AreaMatchDistance)));
        }

        public CageEditOptions ToEditOptions() => new(Clamped(SmallStep, nameof(SmallStep)),
            Clamped(MediumStep, nameof(MediumStep)), Clamped(LargeStep, nameof(LargeStep)),
            Clamped(PickRadius, nameof(PickRadius)), Clamped(FarClickMargin, nameof(FarClickMargin)) / 100f,
            (int)Clamped(UndoSteps, nameof(UndoSteps)));

        public CageOverlayStyle ToOverlayStyle() => new(Clamped(LineRadius, nameof(LineRadius)),
            Clamped(HandleSize, nameof(HandleSize)), Clamped(SelectedHandleSize, nameof(SelectedHandleSize)),
            Clamped(AxisLength, nameof(AxisLength)), Clamped(GizmoSize, nameof(GizmoSize)) / 100f);

        private void SetAdvancedDefaults()
        {
            var edit = CageEditOptions.Default;
            SmallStep = edit.SmallStep;
            MediumStep = edit.MediumStep;
            LargeStep = edit.LargeStep;
            PickRadius = edit.PickRadiusPixels;
            FarClickMargin = edit.FarClickMarginFraction * 100f;
            UndoSteps = edit.HistoryDepth;

            var pipeline = PipelineOptions.Default;
            MaxOffsetFactor = pipeline.Limits.MaxOffsetFactor;
            MinMovementRange = pipeline.Limits.MinOffsetLimitMeters;
            MinLocalVolume = pipeline.Limits.MinVolumeRatio * 100f;
            DeformProps = pipeline.Attachments.DeformProps;
            DeformAreas = pipeline.Attachments.DeformAreas;
            DeformLanes = pipeline.Attachments.DeformLanes;
            DeformNets = pipeline.Attachments.DeformNets;
            AttachedProps = pipeline.Attachments.AttachedPropMode;
            FacadeReach = pipeline.Attachments.FacadeReachMeters;
            AreaMatchDistance = pipeline.AreaMatchMeters;
            RebakeDelay = (float)pipeline.RebakeDelaySeconds;
            PruneUnused = pipeline.PruneUnused;
            PruneAfterDays = pipeline.PruneAfterDays;

            var style = CageOverlayStyle.Default;
            LineRadius = style.LineRadius;
            HandleSize = style.HandleDiameter;
            SelectedHandleSize = style.SelectedHandleDiameter;
            AxisLength = style.AxisLength;
            GizmoSize = style.GizmoScreenSize * 100f;
        }

        // Hand-edited settings files may hold anything; the slider attribute is the single source of the range.
        private static float Clamped(float value, string property)
        {
            var slider = typeof(UnsquareSettings).GetProperty(property)?.GetCustomAttribute<SettingsUISliderAttribute>();
            if (slider == null)
                throw new ArgumentException($"{property} has no slider.", nameof(property));
            return float.IsNaN(value) ? slider.min : math.clamp(value, slider.min, slider.max);
        }

        private static T Defined<T>(T value, T fallback) where T : Enum =>
            Enum.IsDefined(typeof(T), value) ? value : fallback;
    }
}
