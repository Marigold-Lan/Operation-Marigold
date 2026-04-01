public class WaitCommand : ICommand
{
    public CommandType Type => CommandType.Wait;

    public bool CanExecute(CommandContext context)
    {
        return context != null && context.Unit != null;
    }

    public void Execute(CommandContext context)
    {
        if (context?.Unit == null)
            return;

        context.Unit.HasActed = true;
        context.HighlightManager?.ClearMoveHighlights();
        context.HighlightManager?.ClearAttackHighlights();
        context.SelectionManager?.ClearSelection();
    }
}
