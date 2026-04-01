public static class CommandExecutor
{
    public static bool Execute(ICommand command, CommandContext context)
    {
        if (command == null || context == null)
            return false;

        if (!command.CanExecute(context))
            return false;

        command.Execute(context);
        return true;
    }
}
