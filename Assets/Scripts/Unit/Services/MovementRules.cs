using UnityEngine;

/// <summary>
/// 纯规则服务：统一单位占位与移动穿越规则，供 UnitMovement 和 Pathfinding 复用。
/// </summary>
public static class MovementRules
{
    public static bool CanOccupyDestination(Cell destination, GameObject selfUnit)
    {
        if (destination == null)
            return false;
        if (!destination.HasUnit)
            return true;
        return destination.Unit == selfUnit;
    }

    public static bool TryGetTraversalCost(UnitController unit, Vector2Int coord, Vector2Int startCoord, Cell cell, out int cost)
    {
        cost = 99;
        if (unit == null || unit.Data == null)
            return false;

        if (cell != null && cell.HasUnit && coord != startCoord)
            return false;

        cost = cell != null ? MovementCostProvider.GetCost(cell, unit.Data.movementType) : 99;
        return cost >= 0;
    }
}
