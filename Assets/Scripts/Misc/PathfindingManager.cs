using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 提供单位可移动范围（BFS）和 A* 寻路。依赖 MovementCostProvider 计算地形消耗。
/// 供 UI 高亮、UnitMovement 校验、路径预览等使用。
/// </summary>
public class PathfindingManager : Singleton<PathfindingManager>
{
    private static readonly Vector2Int[] NeighborOffsets = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    /// <summary>
    /// 使用 A* 计算从单位到目标的路径（含起止点）。不可达或超出移动力返回 null。
    /// </summary>
    public List<Vector2Int> FindPath(UnitController unit, Vector2Int target)
    {
        if (unit == null || unit.MapRoot == null || unit.Data == null)
            return null;

        var reachable = GetReachableCells(unit);
        if (!reachable.Contains(target) || target == unit.GridCoord)
            return null;

        var mapRoot = unit.MapRoot;
        var start = unit.GridCoord;
        var range = Mathf.Min(unit.Data.movementRange, unit.CurrentFuel);

        var open = new List<Vector2Int> { start };
        var closed = new HashSet<Vector2Int>();
        var gScore = new Dictionary<Vector2Int, int> { [start] = 0 };
        var fScore = new Dictionary<Vector2Int, int> { [start] = Heuristic(start, target) };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        while (open.Count > 0)
        {
            var current = open.OrderBy(p => fScore.TryGetValue(p, out var f) ? f : int.MaxValue).First();
            if (current == target)
            {
                var path = new List<Vector2Int>();
                var node = target;
                while (cameFrom.TryGetValue(node, out var prev))
                {
                    path.Add(node);
                    node = prev;
                }
                path.Add(start);
                path.Reverse();
                return path;
            }

            open.Remove(current);
            closed.Add(current);
            var gCurrent = gScore[current];

            foreach (var neighbor in GetNeighbors(current))
            {
                if (!mapRoot.IsInBounds(neighbor) || closed.Contains(neighbor))
                    continue;

                var cell = mapRoot.GetCellAt(neighbor);
                if (!MovementRules.TryGetTraversalCost(unit, neighbor, start, cell, out var cost))
                    continue;

                var tentativeG = gCurrent + cost;
                if (tentativeG > range)
                    continue;

                if (!gScore.TryGetValue(neighbor, out var gNeighbor) || tentativeG < gNeighbor)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + Heuristic(neighbor, target);
                    if (!open.Contains(neighbor))
                        open.Add(neighbor);
                }
            }
        }

        return null;
    }

    private static int Heuristic(Vector2Int from, Vector2Int to)
    {
        return Math.Abs(from.x - to.x) + Math.Abs(from.y - to.y);
    }

    /// <summary>
    /// 获取指定单位当前可到达的格子坐标集合（基于 BFS）。
    /// </summary>
    public HashSet<Vector2Int> GetReachableCells(UnitController unit)
    {
        var result = new HashSet<Vector2Int>();
        if (unit == null || unit.MapRoot == null || unit.Data == null)
            return result;

        var mapRoot = unit.MapRoot;
        var start = unit.GridCoord;
        var range = Mathf.Min(unit.Data.movementRange, unit.CurrentFuel);

        var queue = new Queue<(Vector2Int pos, int remaining)>();
        var bestRemaining = new Dictionary<Vector2Int, int> { [start] = range };
        queue.Enqueue((start, range));
        result.Add(start);

        while (queue.Count > 0)
        {
            var (pos, remaining) = queue.Dequeue();
            if (remaining <= 0) continue;

            foreach (var neighbor in GetNeighbors(pos))
            {
                if (!mapRoot.IsInBounds(neighbor)) continue;

                var cell = mapRoot.GetCellAt(neighbor);
                if (!MovementRules.TryGetTraversalCost(unit, neighbor, start, cell, out var cost)) continue;

                var nextRemaining = remaining - cost;
                if (nextRemaining < 0) continue;

                if (bestRemaining.TryGetValue(neighbor, out var prev) && prev >= nextRemaining)
                    continue;

                bestRemaining[neighbor] = nextRemaining;
                result.Add(neighbor);
                queue.Enqueue((neighbor, nextRemaining));
            }
        }

        return result;
    }

    private static IEnumerable<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        foreach (var offset in NeighborOffsets)
            yield return pos + offset;
    }
}
