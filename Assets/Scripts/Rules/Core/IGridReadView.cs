using UnityEngine;

public interface IGridReadView
{
    int Width { get; }
    int Height { get; }
    bool IsInBounds(Vector2Int coord);
    bool TryGetCell(Vector2Int coord, out ICellReadView cell);
}
