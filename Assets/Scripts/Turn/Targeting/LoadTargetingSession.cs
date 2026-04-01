using System.Collections.Generic;
using UnityEngine;

public class LoadTargetingSession
{
    private readonly RangeLockSession _rangeLockSession = new RangeLockSession();
    private readonly HashSet<Vector2Int> _targetCoordsBuffer = new HashSet<Vector2Int>();

    public bool IsActive => _rangeLockSession.IsActive;
    public UnitController SourceUnit { get; private set; }
    public bool Contains(Vector2Int coord) => _rangeLockSession.Contains(coord);

    public bool TryEnter(UnitController unit, MapRoot mapRoot)
    {
        Exit(clearHighlights: false);

        if (unit == null)
            return false;

        var root = mapRoot != null ? mapRoot : unit.MapRoot;
        if (root == null)
            return false;

        LoadCommand.CollectLoadTargetCoords(unit, root, _targetCoordsBuffer);
        if (_targetCoordsBuffer.Count == 0)
            return false;

        if (!_rangeLockSession.TryEnter(_targetCoordsBuffer, snapToNearestAllowed: true))
            return false;

        SourceUnit = unit;
        HighlightManager.Instance?.ShowLoadTargetHighlights(unit, clearExisting: true);
        return true;
    }

    public void Exit(bool clearHighlights)
    {
        SourceUnit = null;
        _targetCoordsBuffer.Clear();
        _rangeLockSession.Exit();

        if (clearHighlights)
            HighlightManager.Instance?.ClearLoadHighlights();
    }
}
