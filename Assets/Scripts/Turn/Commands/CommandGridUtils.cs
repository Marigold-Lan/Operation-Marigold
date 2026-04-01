using System.Collections.Generic;
using UnityEngine;

public static class CommandGridUtils
{
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    public static IEnumerable<Vector2Int> EnumerateCardinalNeighbors(Vector2Int origin)
    {
        for (var i = 0; i < CardinalDirections.Length; i++)
            yield return origin + CardinalDirections[i];
    }
}
