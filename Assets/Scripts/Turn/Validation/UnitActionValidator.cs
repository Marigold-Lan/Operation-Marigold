using System.Collections.Generic;
using UnityEngine;

public static class UnitActionValidator
{
    public static bool TryGetMovePath(UnitController unit, Vector2Int targetCoord, GameSessionState sessionState, out List<Vector2Int> path)
    {
        path = null;
        if (unit == null || unit.Movement == null)
            return false;
        if (unit.HasActed)
            return false;

        var currentFaction = sessionState != null ? sessionState.CurrentFaction : UnitFaction.None;
        if (currentFaction != UnitFaction.None &&
            unit.OwnerFaction != currentFaction)
            return false;

        var pathfinding = PathfindingManager.Instance;
        if (pathfinding == null)
            return false;

        var reachable = pathfinding.GetReachableCells(unit);
        if (!reachable.Contains(targetCoord) || targetCoord == unit.GridCoord)
            return false;

        path = pathfinding.FindPath(unit, targetCoord);
        return path != null && path.Count >= 2;
    }

    public static bool CanStartAttackTargeting(UnitController unit, MapRoot mapRoot, GameSessionState sessionState)
    {
        if (unit == null || unit.Data == null || !unit.Data.HasAnyWeapon)
            return false;
        if (unit.HasActed)
            return false;

        var currentFaction = sessionState != null ? sessionState.CurrentFaction : UnitFaction.None;
        if (currentFaction != UnitFaction.None &&
            unit.OwnerFaction != currentFaction)
            return false;

        var root = mapRoot != null ? mapRoot : unit.MapRoot;
        if (root == null)
            return false;

        var maxRange = unit.Data.GetAvailableAttackRangeMax(unit.CurrentAmmo, unit.HasMovedThisTurn);
        if (maxRange < 1)
            return false;

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

                var cell = root.GetCellAt(coord);
                var target = cell != null ? cell.UnitController : null;
                if (target != null &&
                    target.OwnerFaction != unit.OwnerFaction &&
                    CombatRulesShared.CanRuntimeAttack(unit, target, out _))
                    return true;
            }
        }

        return false;
    }

    public static bool IsValidAttackTarget(UnitController source, UnitController target, Vector2Int targetCoord, HashSet<Vector2Int> allowedCoords)
    {
        if (source == null || target == null)
            return false;
        if (allowedCoords == null || !allowedCoords.Contains(targetCoord))
            return false;
        if (target.OwnerFaction == source.OwnerFaction)
            return false;
        return CombatRulesShared.CanRuntimeAttack(source, target, out _);
    }
}
