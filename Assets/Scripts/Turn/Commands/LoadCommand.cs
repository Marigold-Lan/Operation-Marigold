using UnityEngine;
using System.Collections.Generic;

public class LoadCommand : ICommand
{
    public CommandType Type => CommandType.Load;

    public bool CanExecute(CommandContext context)
    {
        if (context?.Unit == null)
            return false;

        var cargo = context.Unit;
        if (!CaptureRulesShared.IsRuntimeCapturer((IUnitReadView)cargo))
            return false;

        if (context.Mode == CommandContext.ExecutionMode.AIImmediate)
        {
            return TryResolveLoadTransporter(context, out _);
        }

        return FindLoadTarget(cargo, context.MapRoot, out _);
    }

    public void Execute(CommandContext context)
    {
        if (context?.Unit == null)
            return;

        if (context.Mode == CommandContext.ExecutionMode.AIImmediate)
        {
            if (TryResolveLoadTransporter(context, out var transporter) && transporter.Load(context.Unit))
                context.Unit.HasActed = true;
            return;
        }

        if (context.TurnController == null)
            return;

        context.TurnController.EnterLoadTargeting(context.Unit);
    }

    private static bool TryResolveLoadTransporter(CommandContext context, out ITransporter transporter)
    {
        transporter = null;
        if (context?.Unit == null)
            return false;

        var cargo = context.Unit;
        if (context.TargetUnit != null)
        {
            transporter = context.TargetUnit.GetComponent<ITransporter>();
            if (transporter == null || context.TargetUnit.OwnerFaction != cargo.OwnerFaction)
                return false;
            if (transporter.LoadedCount >= transporter.Capacity)
                return false;

            var dist = Mathf.Abs(context.TargetUnit.GridCoord.x - cargo.GridCoord.x) + Mathf.Abs(context.TargetUnit.GridCoord.y - cargo.GridCoord.y);
            return dist == 1;
        }

        return FindLoadTarget(cargo, context.MapRoot, out transporter);
    }

    public static bool FindLoadTarget(UnitController cargo, MapRoot mapRoot, out ITransporter transporter)
    {
        transporter = null;
        if (cargo == null || mapRoot == null)
            return false;

        foreach (var coord in CommandGridUtils.EnumerateCardinalNeighbors(cargo.GridCoord))
        {
            if (TryGetLoadTargetTransporterAtCoord(cargo, mapRoot, coord, out var candidate))
            {
                transporter = candidate;
                return true;
            }
        }

        return false;
    }

    public static void CollectLoadTargetCoords(UnitController cargo, MapRoot mapRoot, ICollection<Vector2Int> result)
    {
        if (result == null)
            return;
        result.Clear();
        if (cargo == null || mapRoot == null)
            return;

        foreach (var coord in CommandGridUtils.EnumerateCardinalNeighbors(cargo.GridCoord))
        {
            if (TryGetLoadTargetTransporterAtCoord(cargo, mapRoot, coord, out _))
                result.Add(coord);
        }
    }

    public static bool TryGetLoadTargetTransporterAtCoord(UnitController cargo, MapRoot mapRoot, Vector2Int coord, out ITransporter transporter)
    {
        transporter = null;
        if (cargo == null || mapRoot == null)
            return false;
        if (!mapRoot.IsInBounds(coord))
            return false;

        var cell = mapRoot.GetCellAt(coord);
        var targetUnit = cell != null ? cell.UnitController : null;
        if (targetUnit == null || targetUnit.OwnerFaction != cargo.OwnerFaction)
            return false;
        if (!IsApc(targetUnit))
            return false;

        var targetTransporter = targetUnit.GetComponent<ITransporter>();
        if (targetTransporter == null || targetTransporter.LoadedCount >= targetTransporter.Capacity)
            return false;

        transporter = targetTransporter;
        return true;
    }

    public static bool IsApc(UnitController unit)
    {
        return TransportRulesShared.IsRuntimeTransporter(unit);
    }
}
