#if UNSQUARE_AUTOMATION
using Newtonsoft.Json.Linq;
using Unity.Entities;
using Unsquare.Api;
using Unsquare.Core.Cage;
using Unsquare.UI.Editing;

namespace Unsquare.Automation
{
    /// <summary>JSON snapshots of the edit session and of cage states, attached to every bridge reply.</summary>
    internal static class SessionJson
    {
        public static JObject Session(BridgeContext context)
        {
            var session = context.Session;
            var json = new JObject
            {
                ["mode"] = context.Tool.IsActive,
                ["enabled"] = session.IsEnabled,
                ["target"] = session.HasTarget ? Building(context, session.Target) : null,
                ["preview"] = session.PreviewBuilding != Entity.Null ? BridgeContext.Id(session.PreviewBuilding) : null,
                ["editMode"] = session.EditMode.ToString(),
                ["pickKind"] = session.Selection.PickKind.ToString(),
                ["handle"] = session.Selection.HasHandle ? session.Selection.Handle.ToString() : null,
                ["locked"] = session.IsLocked,
                ["step"] = session.Step.Meters,
                ["gizmo"] = session.GizmoMode.ToString(),
                ["showsRings"] = session.ShowsRings,
                ["hover"] = session.Hover.HasHandle ? session.Hover.Handle.ToString() : null,
                ["hoverPart"] = session.Hover.Axis,
                ["alignMode"] = session.AlignMode.ToString(),
                ["groundFitMode"] = session.GroundFitMode.ToString(),
                ["dragging"] = session.ActiveDrag != null,
                ["lastResult"] = session.LastResult?.ToString(),
                ["problem"] = session.Problem?.ToString(),
                ["canUndo"] = session.CanUndo,
                ["canRedo"] = session.CanRedo,
                ["decisionOpen"] = session.IsDecisionOpen,
            };
            if (session.TryGetCage(out var cage))
                json["cage"] = Cage(cage);
            return json;
        }

        public static JObject Building(BridgeContext context, Entity building) => new()
        {
            ["id"] = BridgeContext.Id(building),
            ["ref"] = context.EntityManager.Exists(building) ? context.RefOf(building).ToString() : null,
        };

        public static JObject Cage(in CageState cage)
        {
            var corners = new JArray();
            for (var i = 0; i < CageTopology.CornerCount; i++)
                corners.Add(BridgeContext.Json(cage.CornerPosition(i)));
            return new JObject
            {
                ["status"] = cage.Status.ToString(),
                ["settled"] = IsSettled(cage),
                ["target"] = cage.Target.ToString(),
                ["box"] = new JObject { ["min"] = BridgeContext.Json(cage.Box.Min), ["max"] = BridgeContext.Json(cage.Box.Max) },
                ["lotBox"] = new JObject { ["min"] = BridgeContext.Json(cage.LotBox.Min), ["max"] = BridgeContext.Json(cage.LotBox.Max) },
                ["corners"] = corners,
                ["targetOffsets"] = cage.TargetOffsets.ToString(),
                ["appliedOffsets"] = cage.AppliedOffsets.ToString(),
                ["targetSpec"] = cage.TargetSpec.ToString(),
                ["appliedSpec"] = cage.AppliedSpec.ToString(),
                ["deformed"] = cage.IsDeformed,
                ["lotDeformed"] = cage.LotDeformed,
                ["split"] = cage.IsSplit,
                ["blendMode"] = cage.BlendMode.ToString(),
                ["terrainLift"] = cage.TerrainLift,
                ["offGround"] = cage.IsOffGround,
                ["exceedsLot"] = cage.ExceedsLot,
                ["hasGhost"] = cage.HasGhost,
            };
        }

        /// <summary>The pipeline is done with the building: nothing pending and the applied shape is the requested one.</summary>
        public static bool IsSettled(in CageState cage) =>
            cage.Status == DeformStatus.Failed ||
            cage.Status is DeformStatus.Applied or DeformStatus.Undeformed && cage.AppliedSpec.Equals(cage.TargetSpec);
    }
}
#endif
