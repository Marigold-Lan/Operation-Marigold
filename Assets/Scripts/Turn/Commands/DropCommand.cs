using UnityEngine;
using System.Collections.Generic;

public class DropCommand : ICommand
{
    public CommandType Type => CommandType.Drop;

    public bool CanExecute(CommandContext context)
    {
        if (context?.Unit == null || !LoadCommand.IsApc(context.Unit))
            return false;

        var transporter = context.Unit.GetComponent<ITransporter>();
        if (transporter == null || transporter.LoadedCount <= 0)
            return false;

        if (context.Mode == CommandContext.ExecutionMode.AIImmediate && context.HasTargetCoord)
        {
            return IsValidDropCoord(context.Unit, context.MapRoot, context.TargetCoord, context.TargetUnit);
        }

        return TryFindDropTarget(context.Unit, context.MapRoot, out _);
    }

    public void Execute(CommandContext context)
    {
        if (context?.Unit == null)
            return;

        if (context.Mode == CommandContext.ExecutionMode.AIImmediate)
        {
            var transporter = context.Unit.GetComponent<ITransporter>();
            if (transporter == null || context.TargetUnit == null || !context.HasTargetCoord)
            {
                context.Unit.HasActed = true;
                return;
            }

            if (transporter.Drop(context.TargetUnit, context.TargetCoord))
            {
                context.TargetUnit.HasActed = true;
                context.Unit.HasActed = true;
            }
            else
            {
                context.Unit.HasActed = true;
            }
            return;
        }

        if (context.TurnController == null)
            return;

        context.TurnController.EnterDropTargeting(context.Unit);
    }

    public static bool TryFindDropTarget(UnitController apc, MapRoot mapRoot, out Vector2Int coord)
    {
        coord = default;
        if (apc == null || mapRoot == null)
            return false;
        if (!TryGetPrimaryCargo(apc, out var cargo))
            return false;

        foreach (var neighbor in CommandGridUtils.EnumerateCardinalNeighbors(apc.GridCoord))
        {
            if (IsValidDropCoord(apc, mapRoot, neighbor, cargo))
            {
                coord = neighbor;
                return true;
            }
        }

        return false;
    }

    public static void CollectDropTargetCoords(UnitController apc, MapRoot mapRoot, ICollection<Vector2Int> result)
    {
        if (result == null)
            return;
        result.Clear();
        if (apc == null || mapRoot == null)
            return;
        if (!TryGetPrimaryCargo(apc, out var cargo))
            return;

        foreach (var neighbor in CommandGridUtils.EnumerateCardinalNeighbors(apc.GridCoord))
        {
            if (IsValidDropCoord(apc, mapRoot, neighbor, cargo))
                result.Add(neighbor);
        }
    }

    public static bool IsValidDropCoord(UnitController apc, MapRoot mapRoot, Vector2Int coord, UnitController cargo = null)
    {
        if (apc == null || mapRoot == null)
            return false;
        if (!mapRoot.IsInBounds(coord))
            return false;

        var cell = mapRoot.GetCellAt(coord);
        if (cell == null || cell.HasUnit)
            return false;

        if (cargo == null)
            return true;
        if (cargo.Data == null)
            return false;

        // 卸载目标需要是货物单位可承载地形（不可通行地形禁止卸载）。
        var terrainCost = MovementCostProvider.GetCost(cell, cargo.Data.movementType);
        return terrainCost >= 0;
    }

    private static bool TryGetPrimaryCargo(UnitController apc, out UnitController cargo)
    {
        cargo = null;
        if (apc == null)
            return false;

        var transporter = apc.GetComponent<ITransporter>();
        if (transporter == null || transporter.LoadedCount <= 0 || transporter.LoadedUnits == null || transporter.LoadedUnits.Count == 0)
            return false;

        cargo = transporter.LoadedUnits[0];
        return cargo != null;
    }
}
