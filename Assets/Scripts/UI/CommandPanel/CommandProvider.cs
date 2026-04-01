using System.Collections.Generic;
using UnityEngine;

public static class CommandProvider
{
    private const string AttackDebugTag = "[AttackDebug]";

    public static List<CommandOption> Build(CommandContext context)
    {
        var options = new List<CommandOption>();
        if (context == null || context.Unit == null)
            return options;

        if (CanShowFire(context))
            options.Add(CreateFireOption());

        if (CanShowCapture(context))
            options.Add(CreateCaptureOption());

        if (CanShowLoad(context))
            options.Add(CreateLoadOption());

        if (CanShowDrop(context))
            options.Add(CreateDropOption());

        if (CanShowSupply(context))
            options.Add(CreateSupplyOption());

        options.Add(CreateWaitOption());
        return options;
    }

    private static CommandOption CreateCaptureOption()
    {
        return new CommandOption
        {
            Type = CommandType.Capture,
            Label = "Capture",
            Interactable = true,
            Command = new CaptureCommand()
        };
    }

    private static CommandOption CreateLoadOption()
    {
        return new CommandOption
        {
            Type = CommandType.Load,
            Label = "Load",
            Interactable = true,
            Command = new LoadCommand()
        };
    }

    private static CommandOption CreateDropOption()
    {
        return new CommandOption
        {
            Type = CommandType.Drop,
            Label = "Drop",
            Interactable = true,
            Command = new DropCommand()
        };
    }

    private static CommandOption CreateSupplyOption()
    {
        return new CommandOption
        {
            Type = CommandType.Supply,
            Label = "Supply",
            Interactable = true,
            Command = new SupplyCommand()
        };
    }

    private static CommandOption CreateFireOption()
    {
        return new CommandOption
        {
            Type = CommandType.Fire,
            Label = "Fire",
            Interactable = true,
            Command = new FireCommand()
        };
    }

    private static CommandOption CreateWaitOption()
    {
        return new CommandOption
        {
            Type = CommandType.Wait,
            Label = "Wait",
            Interactable = true,
            Command = new WaitCommand()
        };
    }

    private static bool CanShowFire(CommandContext context)
    {
        var unit = context.Unit;
        if (unit == null || unit.Data == null)
            return false;
        if (!unit.Data.HasAnyWeapon)
            return false;
        var mapRoot = context.MapRoot != null ? context.MapRoot : unit.MapRoot;
        return UnitActionValidator.CanStartAttackTargeting(unit, mapRoot, context.SessionState);
    }

    private static bool CanShowCapture(CommandContext context)
    {
        var command = new CaptureCommand();
        return command.CanExecute(context);
    }

    private static bool CanShowLoad(CommandContext context)
    {
        var command = new LoadCommand();
        return command.CanExecute(context);
    }

    private static bool CanShowDrop(CommandContext context)
    {
        var command = new DropCommand();
        return command.CanExecute(context);
    }

    private static bool CanShowSupply(CommandContext context)
    {
        var command = new SupplyCommand();
        return command.CanExecute(context);
    }

}
