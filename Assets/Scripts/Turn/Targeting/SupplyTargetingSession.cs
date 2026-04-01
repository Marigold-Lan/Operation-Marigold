public class SupplyTargetingSession
{
    public bool IsActive { get; private set; }
    public UnitController SourceUnit { get; private set; }

    public bool TryEnter(UnitController unit, MapRoot mapRoot)
    {
        Exit(clearHighlights: false);

        if (unit == null)
            return false;

        var root = mapRoot != null ? mapRoot : unit.MapRoot;
        if (root == null)
            return false;

        SourceUnit = unit;
        IsActive = true;
        HighlightManager.Instance?.ShowSupplyTargetHighlights(unit, clearExisting: true);
        GridCursor.Instance?.SetVisualVisible(false);
        GridCursor.Instance?.SetExternalInputLocked(true);
        return true;
    }

    public void Exit(bool clearHighlights)
    {
        IsActive = false;
        SourceUnit = null;
        GridCursor.Instance?.SetVisualVisible(true);
        GridCursor.Instance?.SetExternalInputLocked(false);

        if (clearHighlights)
            HighlightManager.Instance?.ClearSupplyHighlights();
    }
}
