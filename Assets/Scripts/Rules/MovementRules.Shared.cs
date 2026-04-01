using System.Collections.Generic;
using UnityEngine;
using OperationMarigold.AI.Simulation;

public static class MovementRulesShared
{
    private static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1)
    };

    public static bool TryGetRuntimeTraversalCost(UnitController unit, Vector2Int coord, Vector2Int startCoord, Cell cell, out int cost)
    {
        return TryGetRuntimeTraversalCost((IUnitReadView)unit, coord, startCoord, (ICellReadView)cell, out cost);
    }

    /// <summary>
    /// 只读视图版本：运行时仅用于规则层分发入口。
    /// </summary>
    public static bool TryGetRuntimeTraversalCost(
        IUnitReadView unit,
        Vector2Int coord,
        Vector2Int startCoord,
        ICellReadView cell,
        out int cost)
    {
        cost = -1;
        if (unit is not UnitController unitController)
            return false;

        var cellImpl = cell as Cell;
        return MovementRules.TryGetTraversalCost(unitController, coord, startCoord, cellImpl, out cost);
    }

    public static bool TryGetSnapshotTraversalCost(
        AIBoardState board,
        AIUnitSnapshot unit,
        int movingUnitIndex,
        Vector2Int coord,
        Vector2Int startCoord,
        out int cost)
    {
        cost = -1;
        if (board == null || !board.IsInBounds(coord))
            return false;

        ref var cell = ref board.grid[coord.x, coord.y];
        if (cell.unitIndex >= 0 && coord != startCoord && cell.unitIndex != movingUnitIndex)
            return false;

        var terrainKind = (AITerrainKind)cell.terrainKind;
        cost = AIMovementCostProvider.GetCost(terrainKind, unit.movementType);
        return cost >= 0;
    }

    public static bool TryGetSnapshotMinTraversalCost(
        AIBoardState board,
        AIUnitSnapshot unit,
        int movingUnitIndex,
        Vector2Int from,
        Vector2Int to,
        out int minCost)
    {
        minCost = -1;
        if (board == null || !board.IsInBounds(from) || !board.IsInBounds(to))
            return false;
        if (from == to)
        {
            minCost = 0;
            return true;
        }

        var dist = new Dictionary<Vector2Int, int>(128);
        var frontier = new List<Vector2Int>(128);
        dist[from] = 0;
        frontier.Add(from);

        while (frontier.Count > 0)
        {
            int bestIndex = 0;
            int bestScore = int.MaxValue;
            for (int i = 0; i < frontier.Count; i++)
            {
                int d = dist[frontier[i]];
                if (d < bestScore)
                {
                    bestScore = d;
                    bestIndex = i;
                }
            }

            var cur = frontier[bestIndex];
            frontier.RemoveAt(bestIndex);
            var curCost = dist[cur];
            if (cur == to)
            {
                minCost = curCost;
                return true;
            }

            for (int d = 0; d < Dirs.Length; d++)
            {
                var next = cur + Dirs[d];
                if (!TryGetSnapshotTraversalCost(board, unit, movingUnitIndex, next, from, out var step))
                    continue;

                int newCost = curCost + step;
                if (dist.TryGetValue(next, out var oldCost) && oldCost <= newCost)
                    continue;

                dist[next] = newCost;
                if (!frontier.Contains(next))
                    frontier.Add(next);
            }
        }

        return false;
    }
}
