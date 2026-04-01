using System.Collections.Generic;
using UnityEngine;

public class AttackTargetingSession
{
    private readonly RangeLockSession _rangeLockSession = new RangeLockSession();
    private readonly HashSet<Vector2Int> _attackCoordsBuffer = new HashSet<Vector2Int>();

    public bool IsActive => _rangeLockSession.IsActive;
    public UnitController SourceUnit { get; private set; }
    public IReadOnlyCollection<Vector2Int> AllowedCoords => _rangeLockSession.AllowedCoords;
    public bool Contains(Vector2Int coord) => _rangeLockSession.Contains(coord);

    public bool TryEnter(UnitController unit, MapRoot mapRoot)
    {
        Exit(clearHighlights: false);

        if (unit == null || unit.Data == null || !unit.Data.HasAnyWeapon)
            return false;

        var root = mapRoot != null ? mapRoot : unit.MapRoot;
        if (root == null)
            return false;

        BuildAttackRangeCoords(unit, root, _attackCoordsBuffer);
        if (_attackCoordsBuffer.Count == 0)
            return false;

        if (!_rangeLockSession.TryEnter(_attackCoordsBuffer, snapToNearestAllowed: true))
            return false;

        SourceUnit = unit;
        HighlightManager.Instance?.ShowAttackRangeHighlights(unit, clearExisting: true);
        GridCursor.Instance?.SetAttackTargetingRotationBoost(true, unit.OwnerFaction);
        return true;
    }

    public void Exit(bool clearHighlights)
    {
        SourceUnit = null;
        _attackCoordsBuffer.Clear();
        _rangeLockSession.Exit();
        GridCursor.Instance?.SetAttackTargetingRotationBoost(false);

        if (clearHighlights)
            HighlightManager.Instance?.ClearAttackHighlights();
    }

    private static void BuildAttackRangeCoords(UnitController unit, MapRoot root, HashSet<Vector2Int> result)
    {
        result.Clear();
        if (unit == null || unit.Data == null || root == null) return;

        var maxRange = unit.Data.GetAvailableAttackRangeMax(unit.CurrentAmmo, unit.HasMovedThisTurn);
        if (maxRange < 1)
            return;

        var origin = unit.GridCoord;

        for (var dx = -maxRange; dx <= maxRange; dx++)
        {
            for (var dy = -maxRange; dy <= maxRange; dy++)
            {
                var distance = Mathf.Abs(dx) + Mathf.Abs(dy);
                if (distance < 1 || distance > maxRange) continue;
                if (!unit.Data.CanAttackAtDistance(distance, unit.CurrentAmmo, unit.HasMovedThisTurn)) continue;

                var coord = new Vector2Int(origin.x + dx, origin.y + dy);
                if (!root.IsInBounds(coord)) continue;
                result.Add(coord);
            }
        }
    }

}
