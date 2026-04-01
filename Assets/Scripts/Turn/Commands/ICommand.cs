public interface ICommand
{
    CommandType Type { get; }
    bool CanExecute(CommandContext context);
    void Execute(CommandContext context);
}
