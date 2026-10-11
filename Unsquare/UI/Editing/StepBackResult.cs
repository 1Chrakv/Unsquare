namespace Unsquare.UI.Editing
{
    /// <summary>What one right-click undid in the edit session, from the innermost state outwards.</summary>
    public enum StepBackResult
    {
        /// <summary>Nothing left to undo; Unsquare mode stays on (only its toggle turns it off).</summary>
        None,
        DragCancelled,
        SelectionCleared,
        TargetCleared,
    }
}
