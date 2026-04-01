using UnityEngine;
using OperationMarigold.AI.Simulation;

public static class TurnEconomyRulesShared
{
    private const float BuildingRepairHpRate = 0.2f;
    private const float BuildingRepairCostRate = 0.2f;

    public static int ApplySnapshotEndTurn(AIBoardState state, int currentPlayerId)
    {
        if (state == null)
            return currentPlayerId;

        int nextPlayer = state.GetOpponentPlayerId(currentPlayerId);
        ResetTurnFlags(state, nextPlayer);
        ApplySnapshotTurnIncome(state, nextPlayer);
        ApplySnapshotTurnResupplyAndRepair(state, nextPlayer);
        state.currentPlayerId = nextPlayer;
        return nextPlayer;
    }

    public static void ApplyRuntimeTurnStart(UnitFaction faction, MapRoot preferredRoot = null)
    {
        if (faction == UnitFaction.None)
            return;
        TurnIncomeService.Instance.ApplyTurnIncome(faction, preferredRoot);
        TurnResupplyService.Instance.ApplyTurnStartResupply(faction, preferredRoot);
    }

    public static void ApplySnapshotTurnIncome(AIBoardState state, int factionId)
    {
        if (state == null || factionId < 0 || factionId >= state.funds.Length)
            return;

        for (int i = 0; i < state.buildings.Count; i++)
        {
            var b = state.buildings[i];
            if ((int)b.ownerFaction != factionId)
                continue;
            if (b.incomePerTurn <= 0)
                continue;
            state.funds[factionId] += b.incomePerTurn;
        }
    }

    public static void ApplySnapshotTurnResupplyAndRepair(AIBoardState state, int factionId)
    {
        if (state == null || factionId < 0 || factionId >= state.funds.Length)
            return;

        for (int x = 0; x < state.width; x++)
        {
            for (int y = 0; y < state.height; y++)
            {
                ref var cell = ref state.grid[x, y];
                if (cell.buildingIndex < 0 || cell.unitIndex < 0)
                    continue;
                if (cell.buildingIndex >= state.buildings.Count || cell.unitIndex >= state.units.Count)
                    continue;

                var building = state.buildings[cell.buildingIndex];
                if ((int)building.ownerFaction != factionId)
                    continue;

                var unit = state.units[cell.unitIndex];
                if (!unit.alive || (int)unit.faction != factionId)
                    continue;

                unit.fuel = unit.maxFuel;
                unit.ammo = unit.maxAmmo;

                if (unit.hp < unit.maxHp)
                {
                    int repairCost = Mathf.Max(0, Mathf.CeilToInt(unit.cost * BuildingRepairCostRate));
                    int repairHp = Mathf.Max(1, Mathf.CeilToInt(unit.maxHp * BuildingRepairHpRate));
                    if (repairCost > 0 && state.funds[factionId] >= repairCost)
                    {
                        state.funds[factionId] -= repairCost;
                        unit.hp = Mathf.Min(unit.maxHp, unit.hp + repairHp);
                    }
                }

                state.units[cell.unitIndex] = unit;
            }
        }
    }

    private static void ResetTurnFlags(AIBoardState state, int factionId)
    {
        for (int i = 0; i < state.units.Count; i++)
        {
            var unit = state.units[i];
            if (!unit.alive || (int)unit.faction != factionId)
                continue;
            unit.hasActed = false;
            unit.hasMovedThisTurn = false;
            state.units[i] = unit;
        }

        for (int i = 0; i < state.buildings.Count; i++)
        {
            var building = state.buildings[i];
            if ((int)building.ownerFaction != factionId)
                continue;
            building.hasSpawnedThisTurn = false;
            state.buildings[i] = building;
        }
    }
}
