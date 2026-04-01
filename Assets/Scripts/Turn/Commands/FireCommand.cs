using UnityEngine;

public class FireCommand : ICommand
{
    public CommandType Type => CommandType.Fire;

    public bool CanExecute(CommandContext context)
    {
        if (context == null || context.Unit == null)
            return false;

        var root = context.MapRoot != null ? context.MapRoot : context.Unit.MapRoot;
        return UnitActionValidator.CanStartAttackTargeting(context.Unit, root, context.SessionState);
    }

    public void Execute(CommandContext context)
    {
        if (context?.TurnController == null || context.Unit == null)
            return;

        context.TurnController.EnterAttackTargeting(context.Unit, context.ConsumeActionOnCancel);
    }
}
