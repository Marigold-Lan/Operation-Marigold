using UnityEngine;
using OperationMarigold.AI.Simulation;

public static class TransportRulesShared
{
    public static bool IsRuntimeTransporter(UnitController unit)
    {
        var t = unit != null ? unit.GetComponent<ITransporter>() : null;
        return t != null && t.Capacity > 0;
    }

    public static bool CanSnapshotLoad(AIBoardState board, int cargoIdx, int transporterIdx)
    {
        if (board == null ||
            cargoIdx < 0 || transporterIdx < 0 ||
            cargoIdx >= board.units.Count || transporterIdx >= board.units.Count)
        {
            return false;
        }

        var cargo = board.units[cargoIdx];
        var transporter = board.units[transporterIdx];
        if (!cargo.alive || !transporter.alive)
            return false;
        if (!cargo.IsOnMap || cargo.embarkedOnUnitIndex >= 0)
            return false;
        if (cargo.faction != transporter.faction)
            return false;
        if (!cargo.IsLoadableInfantry)
            return false;
        if (transporter.transportCapacity <= 0)
            return false;
        if (board.CountEmbarkedCargo(transporterIdx) >= transporter.transportCapacity)
            return false;
        if (board.ManhattanDistance(cargo.gridCoord, transporter.gridCoord) != 1)
            return false;

        return true;
    }

    public static bool CanSnapshotDrop(AIBoardState board, int transporterIdx, int cargoIdx, Vector2Int dropCoord)
    {
        if (board == null ||
            cargoIdx < 0 || transporterIdx < 0 ||
            cargoIdx >= board.units.Count || transporterIdx >= board.units.Count)
        {
            return false;
        }

        var cargo = board.units[cargoIdx];
        var transporter = board.units[transporterIdx];
        if (!cargo.alive || !transporter.alive)
            return false;
        if (cargo.embarkedOnUnitIndex != transporterIdx)
            return false;
        if (!board.IsInBounds(dropCoord))
            return false;
        if (board.ManhattanDistance(transporter.gridCoord, dropCoord) != 1)
            return false;
        return board.IsValidDropCell(dropCoord, cargo);
    }
}
