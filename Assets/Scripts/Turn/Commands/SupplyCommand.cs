using UnityEngine;

public class SupplyCommand : ICommand
{
    public CommandType Type => CommandType.Supply;

    public bool CanExecute(CommandContext context)
    {
        if (context?.Unit == null)
            return false;

        var supplier = context.Unit.GetComponent<ISupplier>();
        if (supplier == null)
            return false;

        var mapRoot = context.MapRoot != null ? context.MapRoot : context.Unit.MapRoot;
        if (mapRoot == null)
            return false;

        if (context.Mode == CommandContext.ExecutionMode.AIImmediate && context.TargetUnit != null)
        {
            if (context.TargetUnit.OwnerFaction != context.Unit.OwnerFaction)
                return false;

            var dx = context.TargetUnit.GridCoord.x - context.Unit.GridCoord.x;
            var dy = context.TargetUnit.GridCoord.y - context.Unit.GridCoord.y;
            if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1)
                return false;
        }

        return HasAdjacentFriendlyUnit(context.Unit, mapRoot);
    }

    public void Execute(CommandContext context)
    {
        if (context?.Unit == null)
            return;

        if (context.Mode != CommandContext.ExecutionMode.AIImmediate)
        {
            if (context.TurnController == null)
                return;

            context.TurnController.EnterSupplyTargeting(context.Unit);
            return;
        }

        var supplier = context.Unit.GetComponent<ISupplier>();
        var mapRoot = context.MapRoot != null ? context.MapRoot : context.Unit.MapRoot;
        if (supplier == null || mapRoot == null)
            return;

        foreach (var coord in CommandGridUtils.EnumerateCardinalNeighbors(context.Unit.GridCoord))
        {
            if (!mapRoot.IsInBounds(coord))
                continue;

            var target = mapRoot.GetCellAt(coord)?.UnitController;
            if (target == null || target.OwnerFaction != context.Unit.OwnerFaction)
                continue;
            if (!NeedsSupply(target))
                continue;

            supplier.Supply(target);
        }

        context.Unit.HasActed = true;
    }

    private static bool HasAdjacentFriendlyUnit(UnitController source, MapRoot mapRoot)
    {
        if (source == null || mapRoot == null)
            return false;

        foreach (var coord in CommandGridUtils.EnumerateCardinalNeighbors(source.GridCoord))
        {
            if (!mapRoot.IsInBounds(coord))
                continue;

            var target = mapRoot.GetCellAt(coord)?.UnitController;
            if (target == null || target.OwnerFaction != source.OwnerFaction)
                continue;

            return true;
        }

        return false;
    }

    public static bool NeedsSupply(UnitController unit)
    {
        return SupplyRulesShared.NeedsRuntimeSupply((IUnitReadView)unit);
    }
}
