using System;
using UnityEngine;

public class MoveCommand : ICommand
{
    private readonly UnitController _unit;
    private readonly Vector2Int _targetCoord;
    private readonly Action _onMoveCompleted;

    public MoveCommand(UnitController unit, Vector2Int targetCoord, Action onMoveCompleted = null)
    {
        _unit = unit;
        _targetCoord = targetCoord;
        _onMoveCompleted = onMoveCompleted;
    }

    public CommandType Type => CommandType.Wait;

    public bool CanExecute(CommandContext context)
    {
        var unit = _unit != null ? _unit : context?.Unit;
        if (unit == null || unit.Movement == null)
            return false;
        if (unit.HasActed || unit.HasMovedThisTurn)
            return false;

        if (unit.GridCoord == _targetCoord)
            return false;

        var pathfinder = PathfindingManager.Instance;
        if (pathfinder == null)
            return false;

        var path = pathfinder.FindPath(unit, _targetCoord);
        return path != null && path.Count >= 2 && path[0] == unit.GridCoord;
    }

    public void Execute(CommandContext context)
    {
        var unit = _unit != null ? _unit : context?.Unit;
        if (unit == null || unit.Movement == null)
            return;

        var pathfinder = PathfindingManager.Instance;
        if (pathfinder == null)
            return;

        var path = pathfinder.FindPath(unit, _targetCoord);
        if (path == null || path.Count < 2 || path[0] != unit.GridCoord)
            return;

        unit.Movement.MoveAlongPath(path, _onMoveCompleted);
    }
}
