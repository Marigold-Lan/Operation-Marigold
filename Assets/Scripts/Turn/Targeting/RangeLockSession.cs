using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用范围锁会话：将光标限制在指定坐标白名单内，仅负责坐标锁定状态。
/// </summary>
public class RangeLockSession
{
    private readonly HashSet<Vector2Int> _allowedCoords = new HashSet<Vector2Int>();

    public bool IsActive { get; private set; }
    public IReadOnlyCollection<Vector2Int> AllowedCoords => _allowedCoords;
    public bool Contains(Vector2Int coord) => _allowedCoords.Contains(coord);

    public bool TryEnter(ICollection<Vector2Int> allowedCoords, bool snapToNearestAllowed = true)
    {
        Exit();
        if (allowedCoords == null || allowedCoords.Count == 0)
            return false;

        foreach (var coord in allowedCoords)
            _allowedCoords.Add(coord);

        if (_allowedCoords.Count == 0)
            return false;

        IsActive = true;
        GridCursor.Instance?.SetAllowedCoords(_allowedCoords, snapToNearestAllowed);
        return true;
    }

    public void Exit()
    {
        IsActive = false;
        _allowedCoords.Clear();
        GridCursor.Instance?.ClearAllowedCoords();
    }
}
