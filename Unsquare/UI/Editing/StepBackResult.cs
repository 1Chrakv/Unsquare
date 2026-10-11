namespace Unsquare.UI.Editing
{
    /// <summary>What one right-click undid in the edit session, from the innermost state outwards.</summary>
    public enum StepBackResult
    {
        /// <summary>Nothing left to undo; the caller may leave Unsquare mode.</summary>
        None,
        DragCancelled,
        SelectionCleared,
        TargetCleared,
    }
}
