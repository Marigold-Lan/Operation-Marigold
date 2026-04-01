using UnityEngine;

public class AttackCommand : ICommand
{
    private readonly UnitController _source;
    private readonly UnitController _target;
    private readonly Vector2Int _targetCoord;

    public AttackCommand(UnitController source, UnitController target, Vector2Int targetCoord)
    {
        _source = source;
        _target = target;
        _targetCoord = targetCoord;
    }

    public CommandType Type => CommandType.Fire;

    public bool CanExecute(CommandContext context)
    {
        if (_source == null || _target == null)
            return false;

        var session = context != null ? context.AttackTargetingSession : null;
        if (session != null && !session.Contains(_targetCoord))
            return false;

        if (_target.OwnerFaction == _source.OwnerFaction)
            return false;
        return CombatRulesShared.CanRuntimeAttack(_source, _target, out _);
    }

    public void Execute(CommandContext context)
    {
        if (_source != null && _target != null && _source.Combat != null)
        {
            if (_source.Combat.TryAttack(_target, out _))
                _source.HasActed = true;
        }

        context?.HighlightManager?.ClearMoveHighlights();
        context?.TurnController?.ExitAttackTargeting(clearHighlights: true);
        context?.SelectionManager?.ClearSelection();
    }
}
